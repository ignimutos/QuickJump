namespace VSCodeRecent.MobaXterm;

/// <summary>
/// MobaXterm 列表用的图标（包内相对路径，喂给 <c>IconHelpers.FromRelativePath</c>）。
/// 图标来自 Tabler Icons（MIT），生成方式见 <c>Assets\MobaIcons\NOTICE.md</c>。
///
/// <para>选哪个图标由 <see cref="MobaProtocolExtensions.IconPath"/> 决定 —— 协议与图标的
/// 对应收在那里，本类只持有路径常量。</para>
/// </summary>
internal static class MobaXtermIcons
{
    /// <summary>SSH 会话。</summary>
    public const string Ssh = "Assets\\MobaIcons\\ssh.png";

    /// <summary>WSL 会话。</summary>
    public const string Wsl = "Assets\\MobaIcons\\wsl.png";

    /// <summary>其它协议（RDP / VNC / SFTP…）。</summary>
    public const string Session = "Assets\\MobaIcons\\session.png";

    /// <summary>目录行。</summary>
    public const string Folder = "Assets\\MobaIcons\\folder.png";
}
