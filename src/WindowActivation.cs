using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QuickJump;

/// <summary>
/// 把外部程序的窗口切到前台。
///
/// <para><b>为什么需要它。</b>本扩展是进程外 COM 服务器，启动 VSCode / MobaXterm 时自身不在
/// 前台。Windows 的前台锁定（foreground lock）默认禁止后台进程把别的窗口抢到前台，于是表现为
/// 「程序打开了，但开在背后」。此时直接 <c>SetForegroundWindow</c> 会被拒（已实测返回 false），
/// <c>SwitchToThisWindow</c> 同样无效。</para>
///
/// <para><b>可靠做法：先最小化再还原。</b>这两步会让该窗口重新获得「可被置前」的资格，随后的
/// <c>SetForegroundWindow</c> 才会成功。这是本机实测结论（详见 README 实现要点）。</para>
///
/// <para>调用方先在启动外部程序**之前**取一次快照，启动后把快照交给
/// <see cref="BringToForeground"/>，以便优先置前「这次新出现的窗口」。</para>
/// </summary>
internal static partial class WindowActivation
{
    private const int SwMinimize = 6;
    private const int SwRestore = 9;
    private const uint GwOwner = 4;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint command);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 「启动外部程序 + 按需置前」的公共流程：先取窗口快照，再启动，最后（若
    /// <paramref name="activate"/>）在后台线程把新/已有窗口置前。两个打开命令共用它，
    /// 只需各自提供 <paramref name="launch"/>。
    /// </summary>
    /// <param name="activateExisting">
    /// 复用窗口的程序（MobaXterm）传 true：主窗口已在，直接置前已有的；
    /// 可能开新窗口的程序（VSCode）传 false：只认新窗口，避免置前到无关旧窗口。
    /// </param>
    public static void LaunchAndActivate(
        string processName,
        bool activateExisting,
        bool activate,
        Action launch)
    {
        var before = activate ? Snapshot(processName) : [];

        launch();

        if (activate)
        {
            // 后台线程置前：调用方（面板）立即返回，不被「等窗口 + 最小化还原」阻塞。
            Task.Run(() => BringToForeground(
                processName, before, activateExisting, TimeSpan.FromSeconds(10)));
        }
    }

    /// <summary>此刻该进程可见的顶层窗口（排除有 owner 的弹出窗口）。启动外部程序前先取。</summary>
    public static HashSet<IntPtr> Snapshot(string processName)
    {
        var wanted = new HashSet<uint>();
        foreach (var process in Processes(processName))
        {
            wanted.Add((uint)process.Id);
        }

        var found = new HashSet<IntPtr>();
        if (wanted.Count == 0)
        {
            return found;
        }

        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if (wanted.Contains(pid) && IsWindowVisible(hWnd) && GetWindow(hWnd, GwOwner) == IntPtr.Zero)
            {
                found.Add(hWnd);
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    /// <summary>把窗口切到前台。先最小化再还原以获得置前资格，然后置前。</summary>
    public static bool Activate(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        ShowWindow(hWnd, SwMinimize);
        ShowWindow(hWnd, SwRestore);
        return SetForegroundWindow(hWnd);
    }

    /// <summary>
    /// 把目标进程的窗口切到前台。
    ///
    /// <para><b>不要在「窗口已存在」时去等新窗口。</b>MobaXterm 只有一个主窗口，复用实例时
    /// <c>-bookmark</c> 只在其中开新标签，<b>永远不会出现新窗口</b>；若一律轮询等新窗口，
    /// 每次打开都要空转满超时才回退，表现为「点一下卡住两秒」。所以只要进程原本就在跑且
    /// <paramref name="activateExisting"/> 为真，就直接置前已有窗口。</para>
    ///
    /// <para>只有两种情形需要等：首启（<paramref name="before"/> 为空）等第一个窗口出现；
    /// VSCode（<paramref name="activateExisting"/> 为 false）只认新窗口，等不到就不动 ——
    /// 复用窗口时交给 VSCode 自己聚焦，避免置前到无关的旧窗口。</para>
    /// </summary>
    public static void BringToForeground(
        string processName,
        HashSet<IntPtr> before,
        bool activateExisting,
        TimeSpan timeout)
    {
        // 复用已有实例：主窗口已经在，直接置前，不必等新窗口。
        if (before.Count > 0 && activateExisting)
        {
            ActivateExisting(processName);
            return;
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var fresh = Snapshot(processName).Where(h => !before.Contains(h)).ToList();
            if (fresh.Count > 0 && Activate(fresh[^1]))
            {
                return;
            }

            Thread.Sleep(150);
        }

        // 首启但一直没等到窗口：若允许，最后置前已有窗口兜底。
        if (activateExisting)
        {
            ActivateExisting(processName);
        }
    }

    private static void ActivateExisting(string processName)
    {
        var existing = Snapshot(processName);
        if (existing.Count > 0)
        {
            Activate(existing.First());
        }
    }

    private static IEnumerable<Process> Processes(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WindowActivation.Processes({processName}) error: {ex.Message}");
            return [];
        }
    }
}
