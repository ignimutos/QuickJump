using System.Diagnostics;

using VSCodeRecent.VSCode;
namespace VSCodeRecent.MobaXterm;

/// <summary>
/// 读取 MobaXterm session 列表。
///
/// <para><b>与 <see cref="VSCodeRecentHistory"/> 同一骨架</b>（30s 缓存 + 后台单飞刷新 +
/// 快照整体替换），但**不套 <see cref="IVSCodeHistorySource"/>** —— 那套接口的
/// <c>SourceRead</c> 承载 <c>VSCodeItem</c>（含 MRU Order、去重、WSL 目标），而 session
/// 是「一个配置文件里的树」，两者语义不同，硬套会把不存在的概念带进来。这里只保留
/// 「失败是返回值的一部分」这条设计原则。</para>
///
/// <para><b>本类不认识设置</b>（与 <see cref="VSCodeRecentHistory"/> 一致）—— 路径覆盖由
/// 调用方通过参数传入，因此只依赖 BCL，测试里可直接引用而不会牵连 Toolkit。</para>
///
/// <para>位置探测在 <see cref="MobaXtermInstall"/>，解析在 <see cref="MobaXtermIni"/>。</para>
/// </summary>
internal sealed class MobaXtermSessions
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly MobaXtermInstall _install;
    private readonly object _gate = new();

    /// <summary>快照里记下当时用的覆盖路径：覆盖变了就作废缓存，不必等 TTL。</summary>
    internal sealed record Snapshot(
        IReadOnlyList<MobaXtermSession> Items,
        bool Succeeded,
        string? Failure,
        string? IniOverride,
        DateTime At);

    private volatile Snapshot? _snapshot;

    /// <summary>0 = 空闲，1 = 有后台刷新在跑。用 Interlocked 保证同一时刻只跑一个。</summary>
    private int _refreshing;

    public MobaXtermSessions(MobaXtermInstall? install = null) => _install = install ?? MobaXtermInstall.Shared;

    /// <summary>读取并返回当前快照。默认走缓存（30s TTL）。</summary>
    public Snapshot Read(string? iniOverride = null, bool forceRefresh = false)
    {
        lock (_gate)
        {
            if (!forceRefresh &&
                _snapshot is { } cached &&
                cached.IniOverride == iniOverride &&
                DateTime.UtcNow - cached.At < CacheTtl)
            {
                return cached;
            }

            var (items, succeeded, failure) = Load(iniOverride);
            var snapshot = new Snapshot(items, succeeded, failure, iniOverride, DateTime.UtcNow);
            _snapshot = snapshot;
            return snapshot;
        }
    }

    /// <summary>取缓存快照，不触发读取。冷（还没读到过）时为 null。</summary>
    public Snapshot? TryGetCached() => _snapshot;

    /// <summary>快照是否过期（或还没有）。</summary>
    public bool IsStale =>
        _snapshot is not { } snapshot || DateTime.UtcNow - snapshot.At >= CacheTtl;

    /// <summary>
    /// 后台读一次，完成后回调。同一时刻只跑一个，且已有刷新在跑时**不回调** ——
    /// 调用方要把回调当作「状态有变，去看看缓存」的提示。见
    /// <see cref="VSCodeRecentHistory.RefreshInBackground"/> 的同名说明。
    /// </summary>
    public void RefreshInBackground(string? iniOverride, Action onCompleted)
    {
        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                Read(iniOverride, forceRefresh: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MobaXtermSessions.RefreshInBackground error: {ex.Message}");
            }

            Interlocked.Exchange(ref _refreshing, 0);
            onCompleted();
        });
    }

    private (IReadOnlyList<MobaXtermSession> Items, bool Succeeded, string? Failure) Load(string? iniOverride)
    {
        string? iniPath;
        try
        {
            iniPath = _install.IniPath(iniOverride);
        }
        catch (Exception ex)
        {
            return ([], false, ex.Message);
        }

        if (iniPath is null)
        {
            return ([], false, "未找到 MobaXterm.ini");
        }

        try
        {
            var parsed = MobaXtermIni.ParseBookmarks(File.ReadAllText(iniPath), out var error);
            if (parsed is null)
            {
                return ([], false, error ?? "解析失败");
            }

            return ([.. parsed.OrderBy(s => s.Order)], true, null);
        }
        catch (Exception ex)
        {
            return ([], false, ex.Message);
        }
    }

    /// <summary>
    /// 按 session 名 / 目录 / host 做子串匹配。纯函数，便于脱离缓存测试。
    /// </summary>
    public static IReadOnlyList<MobaXtermSession> Search(IReadOnlyList<MobaXtermSession> items, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return items;
        }

        return
        [
            .. items.Where(item =>
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Folder.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (item.Host?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)),
        ];
    }

    /// <summary>
    /// 只保留目录恰好等于 <paramref name="folder"/> 的 session（区分大小写不敏感）。
    /// 目录行点击用它 —— 子串匹配会把 <c>prod-backup</c> 之类也带上，语义不对。
    /// </summary>
    public static IReadOnlyList<MobaXtermSession> InFolder(
        IReadOnlyList<MobaXtermSession> items,
        string folder) =>
        [.. items.Where(item => item.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))];

    /// <summary>探测结果转成诊断文案（✓/✗ 由界面决定）。</summary>
    public IReadOnlyList<string> DescribeProbedPaths(string? iniOverride = null, string? exeOverride = null) =>
        [.. _install
            .Probe(iniOverride, exeOverride)
            .Select(p => $"[{(p.Exists ? "✓" : "✗")}] {p.Kind}: {p.Path}")];
}
