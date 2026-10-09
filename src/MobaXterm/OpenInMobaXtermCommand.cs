using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

using QuickJump.VSCode;
namespace QuickJump.MobaXterm;

/// <summary>
/// 用 MobaXterm 打开一个 session（<c>-bookmark "User sessions\...\name"</c>），
/// MobaXterm 会据此自动登入。
///
/// <para><b>直接启动 exe，不套 <c>cmd.exe</c>。</b><see cref="OpenInVSCodeCommand"/> 包一层
/// cmd 是因为 PATH 上的 <c>code</c> 是 <c>.cmd</c> 批处理；MobaXterm.exe 是真正的
/// GUI 程序，套 cmd 反而让 <c>%</c> 被展开、并多一层窗口风险。</para>
/// </summary>
internal sealed partial class OpenInMobaXtermCommand : InvokableCommand
{
    /// <summary>MobaXterm 的进程名，用于把窗口切到前台。</summary>
    private const string ProcessName = "MobaXterm";

    private readonly MobaXtermSession _session;
    private readonly MobaXtermInstall _install;
    private readonly QuickJumpSettings _settings;

    public OpenInMobaXtermCommand(
        MobaXtermSession session,
        QuickJumpSettings settings,
        MobaXtermInstall? install = null)
    {
        _session = session;
        _settings = settings;
        _install = install ?? MobaXtermInstall.Shared;
        Name = "Open in MobaXterm";

        // 不设 Icon：宿主在列表项没给图标时会回退到 Command.Icon。
    }

    public override ICommandResult Invoke()
    {
        try
        {
            var exe = _install.Executable(_settings.MobaXtermExePath.Value)
                ?? throw new InvalidOperationException("找不到 MobaXterm.exe，可在扩展设置里指定路径");

            // MobaXterm 只有一个主窗口，复用实例时只在其中开新标签，不会有新窗口 ——
            // 所以 activateExisting: true，直接置前已有窗口。
            WindowActivation.LaunchAndActivate(
                ProcessName,
                activateExisting: true,
                activate: _settings.ActivateOnOpen.Value,
                launch: () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = _session.BookmarkArguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenInMobaXterm error: {ex.Message}");
            return CommandResult.ShowToast($"打开失败: {ex.Message}");
        }

        return CommandResult.Hide();
    }
}
