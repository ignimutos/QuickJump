namespace VSCodeRecent;

/// <summary>
/// PATH 环境变量的解析 —— 「POSIX 路径列表 → 目录」与「Scoop shims 目录 → 其 root」
/// 这两段逻辑原本在 <c>VSCodeInstall</c> 与 <c>MobaXtermInstall</c> 各写一份。
/// 与 README「清单只有一份，避免两处漂移」同一原则，收到这里共用。
/// </summary>
internal static class PathEnvironment
{
    /// <summary>PATH 环境变量拆成目录列表（已去引号、去空项）。</summary>
    public static IEnumerable<string> Directories()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var rawDir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var dir = rawDir.Trim().Trim('"');
            if (dir.Length > 0)
            {
                yield return dir;
            }
        }
    }

    /// <summary>
    /// PATH 里名字是 <c>shims</c> 的目录（Scoop 的 shim 目录），返回其上一级即
    /// Scoop root（<c>&lt;root&gt;\shims</c> → <c>&lt;root&gt;</c>）。已去重。
    /// </summary>
    public static IEnumerable<string> ScoopRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in Directories())
        {
            if (!Path.GetFileName(dir).Equals("shims", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Path.GetDirectoryName(dir) is { Length: > 0 } root && seen.Add(root))
            {
                yield return root;
            }
        }
    }
}
