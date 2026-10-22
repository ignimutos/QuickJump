using System.Diagnostics;

using QuickJump.VSCode;
namespace QuickJump.MobaXterm;

/// <summary>
/// 读取 MobaXterm session 列表。
///
/// <para><b>与 <see cref="VSCodeHistory"/> 同一骨架</b>（30s 缓存 + 后台单飞刷新 +
/// 快照整体替换），但**不套 <see cref="IVSCodeHistorySource"/>** —— 那套接口的
/// <c>SourceRead</c> 承载 <c>VSCodeItem</c>（含 MRU Order、去重、WSL 目标），而 session
/// 是「一个配置文件里的树」，两者语义不同，硬套会把不存在的概念带进来。这里只保留
/// 「失败是返回值的一部分」这条设计原则。</para>
///
/// <para><b>本类不认识设置</b>（与 <see cref="VSCodeHistory"/> 一致）—— 路径覆盖由
/// 调用方通过参数传入，因此只依赖 BCL，测试里可直接引用而不会牵连 Toolkit。</para>
///
/// <para>位置探测在 <see cref="MobaXtermInstall"/>，解析在 <see cref="MobaXtermIni"/>。</para>
/// </summary>
internal sealed class MobaXtermSessions
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly MobaXtermInstall _install;
    private readonly object _gate = new();

    /// <summary>一次加载的结果. <c>Load</c> 与"找不到 ini"两条路径共用.</summary>
    private readonly record struct LoadResult(
        IReadOnlyList<MobaXtermSession> Items,
        bool Succeeded,
        string? Failure);

    /// <summary>
    /// 快照里记下当时用的覆盖路径: 覆盖变了就作废缓存, 不必等 TTL.
    ///
    /// <para><paramref name="Fingerprint"/> 是 ini 的源文件戳 (见 <see cref="SourceFingerprint"/>).
    /// TTL 到期后重读时先比它: 一样就只续期 <paramref name="At"/>, <paramref name="Items"/>
    /// 保持同一个列表 -- 页面的行缓存以它为键, 打字过程中不会因为一次空转重读而整体重建.</para>
    /// </summary>
    internal sealed record Snapshot(
        IReadOnlyList<MobaXtermSession> Items,
        bool Succeeded,
        string? Failure,
        string? IniOverride,
        DateTime At,
        string Fingerprint);

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

            // 候选路径每次都重新找 (几笔 File.Exists), 所以 ini 后来出现或消失都能察觉.
            var iniPath = ResolveIniPath(iniOverride, out var pathFailure);
            var fingerprint = iniPath is null ? string.Empty : SourceFingerprint.Of(iniPath);

            // ini 一个字节都没动过: 只续期, 不重解析. 读失败的不走这条 -- 可能只是文件
            // 一时被占用, 要给它每 TTL 重试的机会.
            if (iniPath is not null &&
                _snapshot is { Succeeded: true } previous &&
                previous.IniOverride == iniOverride &&
                previous.Fingerprint == fingerprint)
            {
                var renewed = previous with { At = DateTime.UtcNow };
                _snapshot = renewed;
                return renewed;
            }

            var result = iniPath is null
                ? new LoadResult([], false, pathFailure)
                : Load(iniPath);

            var snapshot = new Snapshot(
                result.Items, result.Succeeded, result.Failure, iniOverride, DateTime.UtcNow, fingerprint);
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
    /// <see cref="VSCodeHistory.RefreshInBackground"/> 的同名说明。
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

    /// <summary>第一个存在的 ini 路径; 找不到时给出失败原因.</summary>
    private string? ResolveIniPath(string? iniOverride, out string? failure)
    {
        try
        {
            var path = _install.IniPath(iniOverride);
            failure = path is null ? "未找到 MobaXterm.ini" : null;
            return path;
        }
        catch (Exception ex)
        {
            failure = ex.Message;
            return null;
        }
    }

    private static LoadResult Load(string iniPath)
    {
        try
        {
            var parsed = MobaXtermIni.ParseBookmarks(File.ReadAllText(iniPath), out var error);
            if (parsed is null)
            {
                return new LoadResult([], false, error ?? "解析失败");
            }

            return new LoadResult([.. parsed.OrderBy(s => s.Order)], true, null);
        }
        catch (Exception ex)
        {
            return new LoadResult([], false, ex.Message);
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
    /// 该显示哪些目录行（去重、稳定顺序）。
    ///
    /// <para>空查询是浏览视图，列出命中结果里出现过的全部目录。有查询词时**只在查询词本身
    /// 命中目录名**时才给目录行 —— 敲的是 session 名却插一条它所在目录的行，只会把真正想连的
    /// 那条挤下去，还得多按一次回车。</para>
    /// </summary>
    public static IReadOnlyList<string> FolderRows(
        IReadOnlyList<MobaXtermSession> matched,
        string? query)
    {
        var folders = matched
            .Select(s => s.Folder)
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [.. folders];
        }

        var term = query.Trim();
        return [.. folders.Where(f => f.Contains(term, StringComparison.OrdinalIgnoreCase))];
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
