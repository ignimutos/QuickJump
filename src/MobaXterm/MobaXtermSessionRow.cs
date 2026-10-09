using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickJump.MobaXterm;

/// <summary>
/// session 行的构造。主页与目录子页（<see cref="MobaXtermFolderPage"/>）用同一份，
/// 避免两处各写一份、随协议/图标改动漂移。
/// </summary>
internal static class MobaXtermSessionRow
{
    /// <summary>一条 session：标题是名字，副标题带目录与 host，图标按协议区分，回车打开。</summary>
    public static ListItem Create(MobaXtermSession session, QuickJumpSettings settings) =>
        new(new OpenInMobaXtermCommand(session, settings))
        {
            Title = session.Name,
            Subtitle = session.Subtitle,
            Icon = IconHelpers.FromRelativePath(session.Protocol.IconPath()),
        };
}
