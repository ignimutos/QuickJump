using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickJump.MobaXterm;
using QuickJump.VSCode;

namespace QuickJump;

/// <summary>
/// 命令提供者：QuickJump 扩展的入口，暴露**一个模块一行**的顶层命令
/// （VSCode / MobaXterm）。注意：不要手写 ICommandProvider 接口 —— 必须继承 Toolkit 的
/// CommandProvider 基类，由基类实现 ICommandProvider / INotifyItemsChanged /
/// IDisposable 的全部成员，这里只需 override TopLevelCommands()。
///
/// 列表页在这里构造一次，顶层命令与 Fallback 共用同一个实例（因而共用设置订阅与
/// 缓存），不再是「两处各建一个页面」。
///
/// <para><b>命名层次</b>：QuickJump 是产品（扩展 + 设置 + 顶层入口），VSCode / MobaXterm
/// 是模块。模块的界面文案只提自己，不提 QuickJump。</para>
/// </summary>
public partial class QuickJumpCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly IFallbackCommandItem[] _fallbacks;
    private readonly QuickJumpSettings _settings = new();

    public QuickJumpCommandsProvider()
    {
        DisplayName = "QuickJump";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

        var history = VSCodeHistory.Shared;
        var listPage = new VSCodeListPage(_settings, history);

        _fallbacks = [new VSCodeFallbackItem(_settings, history, listPage)];

        var mobaSessions = new MobaXtermSessions();

        _commands =
        [
            new CommandItem(listPage)
            {
                Title = "VSCode",
                Subtitle = "打开最近项目",
                Icon = IconHelpers.FromRelativePath(VSCodeIcons.App),
            },
            new CommandItem(new MobaXtermSessionListPage(_settings, mobaSessions))
            {
                Title = "MobaXterm",
                Subtitle = "搜索并连接 session",
                Icon = IconHelpers.FromRelativePath(MobaXtermIcons.App),
            },
        ];

        Settings = _settings.Settings;
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    /// <summary>根列表内联搜索：直接敲项目名即可，不必先进入列表页。</summary>
    public override IFallbackCommandItem[] FallbackCommands() => _fallbacks;
}
