using QuickJump.VSCode;
namespace QuickJump.MobaXterm;

/// <summary>MobaXterm session 的协议类型。只用来决定副标题文案与字段取法。</summary>
internal enum MobaProtocol
{
    /// <summary>#109# —— SSH</summary>
    Ssh,

    /// <summary>#149# —— 本地 WSL</summary>
    Wsl,

    /// <summary>其它（RDP / VNC / SFTP 等，未逐个确认字段布局）</summary>
    Other,
}

/// <summary>
/// 协议相关的展示映射。类型专属的东西都收在这里 —— 加一个协议只改这一处，
/// 不再散在副标题与图标两个 switch 里。
/// </summary>
internal static class MobaProtocolExtensions
{
    /// <summary>列表页副标题用，与 <see cref="ItemKindExtensions.Label"/> 同风格。</summary>
    public static string Label(this MobaProtocol protocol) => protocol switch
    {
        MobaProtocol.Ssh => "SSH",
        MobaProtocol.Wsl => "WSL",
        _ => "会话",
    };

    /// <summary>列表页行图标（包内相对路径）。见 <c>Assets\MobaIcons\NOTICE.md</c>。</summary>
    public static string IconPath(this MobaProtocol protocol) => protocol switch
    {
        MobaProtocol.Ssh => MobaXtermIcons.Ssh,
        MobaProtocol.Wsl => MobaXtermIcons.Wsl,
        _ => MobaXtermIcons.Session,
    };
}

/// <summary>
/// 一个 MobaXterm session。值来自 <c>MobaXterm.ini</c> 的 <c>[Bookmarks*]</c> 段。
///
/// <para><b>打开只依赖 <see cref="SessionPath"/>。</b>Host/Port/User 只是从
/// session 串里尽力解出的展示字段 —— 解错或解不出只会让副标题难看，不影响能否打开。
/// 这样字段布局随版本变化时，最坏情况也只是显示退化。</para>
/// </summary>
internal sealed record MobaXtermSession
{
    /// <summary>叶子名，即 ini 里的键名，如 <c>gateway</c>。</summary>
    public required string Name { get; init; }

    /// <summary>所属目录链（<c>SubRep</c> 的值），根段为空串，可为多级如 <c>a\b</c>。</summary>
    public required string Folder { get; init; }

    /// <summary>协议类型（决定副标题文案与行图标）。</summary>
    public required MobaProtocol Protocol { get; init; }

    /// <summary>喂给 <c>-bookmark</c> 的完整路径，如 <c>User sessions\ld\gateway</c>。</summary>
    public required string SessionPath { get; init; }

    /// <summary>主机名（SSH）/ 发行版名（WSL）；解析不出时为 null。</summary>
    public string? Host { get; init; }

    /// <summary>端口，仅 SSH 有；解析不出时为 null。</summary>
    public string? Port { get; init; }

    /// <summary>登录用户名，仅 SSH 有；解析不出时为 null。</summary>
    public string? User { get; init; }

    /// <summary>ini 内出现顺序，仅用于稳定排序。</summary>
    public required int Order { get; init; }

    /// <summary>协议显示名（副标题无 host 时的兜底）。</summary>
    public string TypeLabel => Protocol.Label();

    /// <summary>
    /// MobaXterm.exe 的启动参数。纯函数，便于单测（不必加载 Toolkit）。
    ///
    /// <para><b>只用 <c>-bookmark</c>，不加 <c>-newtab</c>。</b>实测：<c>-bookmark</c>
    /// 单独用时，MobaXterm 已在运行则复用它、在窗口里开新标签；未运行则自行启动。
    /// 而 <c>-newtab</c> 的语义是「在新标签里执行后跟的命令」（文档 <c>-newtab ["&lt;Command&gt;"]</c>），
    /// 把 <c>-bookmark ...</c> 拼在它后面会被当成一条 shell 命令丢进去执行，
    /// 表现为终端里跑一堆 <c>set -o</c> 然后 <c>/bin/bash: -c: option requires an argument</c>、会话即关。</para>
    /// </summary>
    public string BookmarkArguments => MobaXtermIni.BookmarkArguments(SessionPath);

    /// <summary>副标题：目录 · [user@]host[:port]，都没有时退化为协议名。</summary>
    public string Subtitle
    {
        get
        {
            var parts = new List<string>();

            if (Folder.Length > 0)
            {
                parts.Add(Folder);
            }

            if (Host is { Length: > 0 } host)
            {
                if (Port is { Length: > 0 } port)
                {
                    host = $"{host}:{port}";
                }

                parts.Add(User is { Length: > 0 } user ? $"{user}@{host}" : host);
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : TypeLabel;
        }
    }
}
