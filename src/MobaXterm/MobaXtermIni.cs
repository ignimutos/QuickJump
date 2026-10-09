
using QuickJump.VSCode;
namespace QuickJump.MobaXterm;

/// <summary>
/// <c>MobaXterm.ini</c> 的 <c>[Bookmarks*]</c> 段解析。只吃字符串、不碰文件系统，
/// 因此可以脱离 MobaXterm 直接测试 —— 与 <see cref="ParseHistoryKey"/> 同一约定。
///
/// <para><b>层级只用 <c>SubRep</c> 的字符串内容还原。</b><c>[Bookmarks_N]</c> 的编号
/// 只在不同段之间区分，不参与深度计算（不假设编号连续或与层级一一对应）。</para>
///
/// <para><b>逐行容错。</b>单条 session 解析失败只跳过该条，不整文件失败；只有当
/// 有候选条目、且全部无法解析时才硬失败 —— 那通常意味着格式随版本变了，
/// 不能静默显示成「没有 session」。</para>
/// </summary>
internal static class MobaXtermIni
{
    /// <summary><c>-bookmark</c> 参数里 session 路径的固定前缀。</summary>
    internal const string SessionsRoot = "User sessions";

    /// <summary><c>;</c> 在 ini 里被编码成这个 token（值内不能直接用 <c>;</c>）。</summary>
    private const string EscapedSemicolon = "__PTVIRG__";

    /// <summary>
    /// <c>-bookmark</c> 的完整参数。路径**整体带引号**：名字含空格（<c>User sessions</c>），
    /// 不加引号会被按空格切开，报 <c>no bookmark folder "User"</c>。
    /// </summary>
    public static string BookmarkArguments(string sessionPath) => $"-bookmark \"{sessionPath}\"";

    /// <summary>
    /// 解析全部书签 session。<paramref name="error"/> 非 null 表示硬失败（返回 null）。
    /// 没有 <c>[Bookmarks*]</c> 段或段内没有条目都算正常（空列表，非失败）。
    /// </summary>
    public static IReadOnlyList<MobaXtermSession>? ParseBookmarks(string iniText, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(iniText))
        {
            return [];
        }

        var sessions = new List<MobaXtermSession>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? section = null;
        var folder = string.Empty;
        var order = 0;
        var unparsable = 0;

        foreach (var rawLine in iniText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] is ';' or '#')
            {
                continue;
            }

            if (line[0] == '[')
            {
                var close = line.IndexOf(']');
                section = close > 1 ? line[1..close].Trim() : null;
                folder = string.Empty;
                continue;
            }

            if (!IsBookmarksSection(section))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            if (key.Equals("SubRep", StringComparison.OrdinalIgnoreCase))
            {
                folder = value;
            }
            else if (key.Equals("ImgNum", StringComparison.OrdinalIgnoreCase))
            {
                // 保留键，不是 session
            }
            else if (TryParseSession(key, folder, value, order, out var session))
            {
                if (seen.Add(session.SessionPath))
                {
                    sessions.Add(session);
                    order++;
                }
            }
            else
            {
                unparsable++;
            }
        }

        if (sessions.Count == 0 && unparsable > 0)
        {
            error = $"{unparsable} 条 session 全部无法解析，格式可能已改变";
            return null;
        }

        return sessions;
    }

    /// <summary>是否是书签段：<c>[Bookmarks]</c> 或 <c>[Bookmarks_N]</c>。</summary>
    private static bool IsBookmarksSection(string? section) =>
        section is not null &&
        (section.Equals("Bookmarks", StringComparison.OrdinalIgnoreCase) ||
         section.StartsWith("Bookmarks_", StringComparison.OrdinalIgnoreCase));

    private static bool TryParseSession(
        string name,
        string folder,
        string value,
        int order,
        out MobaXtermSession session)
    {
        session = null!;

        // 值形如 #<type>#<ver>%f1%f2%...
        if (value.Length == 0 || value[0] != '#')
        {
            return false;
        }

        var parts = value.Split('%');
        if (!TryParseProtocol(parts[0], out var protocol))
        {
            return false;
        }

        string? host = null;
        string? port = null;
        string? user = null;

        if (protocol == MobaProtocol.Ssh && parts.Length >= 4)
        {
            // SSH：host、port、user 是紧邻的三段
            host = Field(parts, 1);
            port = Field(parts, 2);
            user = Field(parts, 3);
        }
        else if (protocol == MobaProtocol.Wsl)
        {
            // WSL：第一段是发行版名，没有 host/port/user
            host = Field(parts, 1);
        }

        session = new MobaXtermSession
        {
            Name = name,
            Folder = folder,
            Protocol = protocol,
            SessionPath = BuildSessionPath(folder, name),
            Host = host,
            Port = port,
            User = user,
            Order = order,
        };
        return true;
    }

    /// <summary>取第 <paramref name="index"/> 段并反转义；空段返回 null。</summary>
    private static string? Field(string[] parts, int index)
    {
        if (index >= parts.Length)
        {
            return null;
        }

        var raw = parts[index];
        return raw.Length == 0 ? null : raw.Replace(EscapedSemicolon, ";");
    }

    /// <summary>首段 <c>#109#0</c> —— 取两个 <c>#</c> 之间的数字定类型。</summary>
    private static bool TryParseProtocol(string firstField, out MobaProtocol protocol)
    {
        protocol = MobaProtocol.Other;

        if (firstField.Length < 2 || firstField[0] != '#')
        {
            return false;
        }

        var close = firstField.IndexOf('#', 1);
        if (close < 0)
        {
            return false;
        }

        var number = firstField[1..close];
        switch (number)
        {
            case "109":
                protocol = MobaProtocol.Ssh;
                return true;
            case "149":
                protocol = MobaProtocol.Wsl;
                return true;
            default:
                // 其它数字类型（RDP/VNC/...）仍产出 session —— 有路径就能打开。
                // 数字之外的内容说明不是 session 串。
                return int.TryParse(number, out _);
        }
    }

    /// <summary><c>User sessions\&lt;目录&gt;\&lt;名字&gt;</c>；根段没有目录那一层。</summary>
    internal static string BuildSessionPath(string folder, string name)
    {
        var trimmed = folder.Trim().Trim('\\');
        return trimmed.Length == 0
            ? $@"{SessionsRoot}\{name}"
            : $@"{SessionsRoot}\{trimmed}\{name}";
    }
}
