using System.Diagnostics;

namespace QuickJump.VSCode;

/// <summary>
/// 读取 VSCode 最近打开记录。
///
/// 只做三件事：枚举数据源、合并去重排序、缓存结果。位置探测在
/// <see cref="VSCodeInstall"/>，URI 编解码在 <see cref="VSCodeUri"/>，
/// JSON 解析在 <see cref="ParseHistoryKey"/>。
///
/// <para><b>这个类不认识设置。</b>「要不要显示文件」是调用方的事，通过
/// <see cref="Search"/> / <see cref="Filter"/> 的参数传进来，不落在模块状态上。</para>
///
/// <para><b>已知缺口。</b>ssh-remote / dev-container 等非 WSL 远程的记录会被
/// <see cref="VSCodeUri"/> 判为无法本地打开而丢弃，界面上完全看不到它们，
/// 诊断信息里也不体现。要修得先有能表达这类位置的模型。</para>
/// </summary>
internal sealed class VSCodeHistory
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private static VSCodeHistory? _shared;
    private static readonly object SharedGate = new();

    /// <summary>页面和内联搜索共用一个实例，从而共用缓存。</summary>
    public static VSCodeHistory Shared
    {
        get
        {
            lock (SharedGate)
            {
                return _shared ??= new VSCodeHistory();
            }
        }
    }

    private readonly VSCodeInstall _install;
    private readonly object _gate = new();

    /// <summary>
    /// 读结果快照。整体替换，不做原地修改，读侧无锁取一次引用即可。
    ///
    /// <para><paramref name="Fingerprint"/> 是读这份数据时的源文件戳 (见
    /// <see cref="SourceFingerprint"/>). TTL 到期后重读时先比它: 一样就只续期
    /// <paramref name="At"/>, <paramref name="Items"/> 保持同一个列表 -- 页面的行缓存
    /// 以它为键, 于是用户打字过程中不会因为一次"什么都没变"的重读而整体重建.</para>
    /// </summary>
    internal sealed record Snapshot(
        IReadOnlyList<VSCodeItem> Items,
        bool Succeeded,
        string? Failure,
        DateTime At,
        string Fingerprint);

    private volatile Snapshot? _snapshot;

    /// <summary>0 = 空闲，1 = 有后台刷新在跑。用 Interlocked 保证同一时刻只跑一个。</summary>
    private int _refreshing;

    public VSCodeHistory(VSCodeInstall? install = null) => _install = install ?? VSCodeInstall.Shared;

    /// <summary>最近记录。默认走缓存（VSCode 刚打开的项目最迟一个 TTL 后出现）。</summary>
    public (IReadOnlyList<VSCodeItem> Items, bool Succeeded, string? Failure) Read(bool forceRefresh = false)
    {
        // 列表页和 Fallback 内联搜索会并发调用，缓存读写要串行化
        lock (_gate)
        {
            if (!forceRefresh && _snapshot is { } cached && DateTime.UtcNow - cached.At < CacheTtl)
            {
                return (cached.Items, cached.Succeeded, cached.Failure);
            }

            // 源文件一个都没动过: 只续期, 不重读.
            //
            // 判定只看 Succeeded, 不看 Failure. SourceRead.Combine 是"有一个来源读成功就算
            // 成功", 同时仍带着拼接的失败信息 -- 若还要求 Failure 为 null, 只要有一个
            // *.vscdb 长期读不了, 就会每 TTL 全量重读并换掉 Items, 行缓存跟着作废,
            // 正是本次要消除的周期性卡顿.
            //
            // 整份读失败 (Succeeded 为 false) 不续期: 那可能只是一时锁库, 要给它每 TTL
            // 重试的机会, 不能把一次失败永久固化.
            var sources = Sources();
            var fingerprint = GetFingerprint(sources);
            if (_snapshot is { Succeeded: true } previous && previous.Fingerprint == fingerprint)
            {
                var renewed = previous with { At = DateTime.UtcNow };
                _snapshot = renewed;
                return (renewed.Items, renewed.Succeeded, renewed.Failure);
            }

            var reads = sources.Select(source => source.Read()).ToList();
            var merged = SourceRead.Combine(reads);

            // 排序要按来源分别做 —— 每个来源的 Order 各自从 0 开始，见 SortAndDedupe
            var items = SortAndDedupe(reads);

            var snapshot = new Snapshot(items, merged.Succeeded, merged.Failure, DateTime.UtcNow, fingerprint);
            _snapshot = snapshot;
            return (snapshot.Items, snapshot.Succeeded, snapshot.Failure);
        }
    }

    /// <summary>
    /// 取缓存快照，不触发任何读取。冷（还没读到过）时为 null ——
    /// 调用方据此先给占位再等后台刷新，从而让首屏不被同步扫描挡住。
    /// </summary>
    public Snapshot? TryGetCached() => _snapshot;

    /// <summary>快照是否已过期（或还没有）。页面据此决定要不要再唤起一次后台刷新。</summary>
    public bool IsStale =>
        _snapshot is not { } snapshot || DateTime.UtcNow - snapshot.At >= CacheTtl;

    /// <summary>
    /// 后台读一次并预热图标关联表，完成后回调。
    ///
    /// <para><b>同一时刻只会跑一个，且本方法可能不回调。</b>已有刷新在跑时直接返回
    /// （不排队、不调用 <paramref name="onCompleted"/>）—— 避免连续输入堆任务。
    /// 调用方必须把回调当作「状态有变，去看看缓存」的提示，而不是「这次调用完成了」；
    /// 会在跑的那次结束后统一通知，所以状态不会漏。</para>
    ///
    /// <para><b>图标表先于快照发布。</b>先加载 <see cref="MaterialIconTheme.Shared"/> 再
    /// <see cref="Read"/>，保证快照一变可见，图标表就已经在缓存里 —— 渲染线程随后首次
    /// <c>IconFor</c> 不会阻塞在 <see cref="MaterialIconTheme"/> 的加载锁上。</para>
    /// </summary>
    public void RefreshInBackground(Action onCompleted)
    {
        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                _ = MaterialIconTheme.Shared;
                Read(forceRefresh: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshInBackground error: {ex.Message}");
            }

            // 先复位守卫再回调：回调里回读缓存时，状态已就绪。也避免 callback 抛异常
            // 把守卫永久卡在 1 上（那会让加载态再也收不起来）。
            Interlocked.Exchange(ref _refreshing, 0);
            onCompleted();
        });
    }

    /// <summary>
    /// 按标题 / 路径做子串匹配, 再按相关度分档排序. 列表页和根列表的 Fallback 内联搜索
    /// 共用同一套规则, 免得两边结果对不上. 纯函数, 便于脱离缓存测试.
    ///
    /// <para><b>为什么还要排序.</b>只过滤的话结果保持调用方传入的顺序, 于是一个只是路径里含
    /// 查询词的项目会排在名字完全匹配的项目前面. 分档见 <see cref="Relevance"/>: 名字命中越强
    /// 越靠前, 仅路径命中垫底. 同一档内 <c>OrderBy</c> 是稳定排序, 自动保留传入的列表顺序 --
    /// 那是 <see cref="SortAndDedupe"/> 排好的结果 (来源内按 Order, 跨来源按来源优先级).</para>
    /// </summary>
    public static IReadOnlyList<VSCodeItem> Search(IReadOnlyList<VSCodeItem> items, string? query, bool includeFiles)
    {
        var filtered = Filter(items, includeFiles);
        if (string.IsNullOrWhiteSpace(query))
        {
            return filtered;
        }

        return
        [
            .. filtered
                .Where(item =>
                    item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => Relevance(item, query)),
        ];
    }

    /// <summary>
    /// 相关度分档, 越小越靠前. 只看查询词命中标题的强度; 走到最后一档的就是「仅路径命中」.
    /// </summary>
    private static int Relevance(VSCodeItem item, string query)
    {
        if (item.Title.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (item.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ? 2 : 3;
    }

    /// <summary>设置里的「显示文件」过滤。纯函数，取值随调用进来。</summary>
    public static IReadOnlyList<VSCodeItem> Filter(IReadOnlyList<VSCodeItem> items, bool includeFiles) =>
        includeFiles ? items : [.. items.Where(x => x.Kind.IsProject())];

    /// <summary>
    /// 每个位置各起一个来源：目录内的 *.vscdb 逐个一条，storage.json 一条。
    /// 返回空说明一个来源都没有 —— 调用方据此显示诊断信息。
    ///
    /// <para>一次物化完成: 指纹与读取必须针对同一份来源列表, 分两次枚举既慢又可能不一致.</para>
    /// </summary>
    private IReadOnlyList<IVSCodeHistorySource> Sources()
    {
        var sources = new List<IVSCodeHistorySource>();

        foreach (var dbPath in _install.SharedStoragePaths())
        {
            if (File.Exists(dbPath))
            {
                sources.Add(new SharedStorageRecordSource(dbPath));
            }
        }

        foreach (var dir in _install.GlobalStorageDirs())
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            List<string> files;
            try
            {
                files = [.. Directory.GetFiles(dir, "*.vscdb", SearchOption.AllDirectories)];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Sources({dir}) error: {ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                sources.Add(new GlobalStorageDatabaseSource(file));
            }

            var storageJson = Path.Combine(dir, "storage.json");
            if (File.Exists(storageJson))
            {
                sources.Add(new StorageJsonSource(storageJson));
            }
        }

        return sources;
    }

    /// <summary>
    /// 源文件的指纹: 每个来源的路径与文件戳(<see cref="SourceFingerprint.Of"/>), 排序后拼成一个串.
    ///
    /// <para><b>为什么不需要再看目录.</b>来源列表本身就是每次现枚举的
    /// (<c>Directory.GetFiles(*.vscdb, AllDirectories)</c>), 新出现的库会多出一个来源、
    /// 消失的会少一个 -- 拼出来的指纹自然变. 目录 mtime 反而会被无关文件误触发.</para>
    ///
    /// <para>排序是为了稳定: 同一批来源换个枚举顺序, 指纹不该跟着变.</para>
    ///
    /// <para>省下的是开 SQLite 库、解析 JSON、排序去重 -- 目录枚举照做, 但比读内容便宜.</para>
    /// </summary>
    private static string GetFingerprint(IReadOnlyList<IVSCodeHistorySource> sources) =>
        string.Join(
            '|',
            sources
                .Select(source => SourceFingerprint.Of(source.SourcePath))
                .OrderBy(part => part, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// 按来源优先级拼接、来源内按最近顺序排，再按路径去重。
    ///
    /// <para><b>排序语义：Order 升序即「最后打开时间倒序」，不按类型分组。</b>
    /// VSCode 写出的 entries 本身就是一个 MRU 列表，Order 与类型无关 —— 工作区与文件夹
    /// 在同一段里按时间交错，文件是紧接着的第二段（VSCode 存的时候先展开 workspaces
    /// 再展开 files）。以前先按 <see cref="ItemKind.Rank"/> 排，把列表切成了
    /// 工作区 / 文件夹 / 文件三个连成一片的类型块，于是**比某个文件夹更早打开的工作区
    /// 依然排在它前面** —— 时间顺序就没了。类型信息在展示层用（见
    /// <see cref="ItemGroups"/>），不该参与排序。</para>
    ///
    /// <para>类型只在 Order 并列时兜底：storage.json 的 workspaces 与 folders 是
    /// 两个数组，各自从 0 开始计数，单看 Order 分不出谁更近。</para>
    ///
    /// <para><b>为什么不用跨来源的全局 Order。</b>Order 只在单个数据源内有意义，
    /// 两个来源都从 0 开始；跨来源全局排会把 sharedStorage 与 storage.json 的记录
    /// 乱序交错。来源的枚举顺序就是优先级（见 <see cref="Sources"/>），先出现的保留，
    /// 后出现的同路径条目丢掉 —— 同一个项目常同时存在于多个来源里。</para>
    /// </summary>
    internal static IReadOnlyList<VSCodeItem> SortAndDedupe(IReadOnlyList<SourceRead> reads)
    {
        var deduped = new List<VSCodeItem>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var read in reads)
        {
            foreach (var item in read.Items.OrderBy(x => x.Order).ThenBy(x => x.Kind.Rank()))
            {
                if (seenPaths.Add(item.Path))
                {
                    deduped.Add(item);
                }
            }
        }

        return deduped;
    }

    /// <summary>
    /// 探测结果转成诊断文案：✓/✗ 是渲染，本类之外还给了
    /// <see cref="VSCodeInstall.Probe"/> 的结构化数据。
    /// </summary>
    public IReadOnlyList<string> DescribeProbedPaths()
    {
        var lines = _install.Probe()
            .Select(p => $"[{(p.Exists ? "✓" : "✗")}] {KindLabel(p.Kind)}: {p.Path}")
            .ToList();

        if (!lines.Any(l => l.Contains(VSCodeStorageKind.PortableDataDir.ToString())))
        {
            lines.Add("[✗] 未找到便携版 VSCode 的 data 目录");
        }

        lines.Add($"PATH 上的 code: {_install.CodeExecutable() ?? "未找到"}");
        lines.Add($"环境变量 {VSCodeInstall.PortableDataEnvVar}: " +
                  $"{Environment.GetEnvironmentVariable(VSCodeInstall.PortableDataEnvVar) ?? "(未设置)"}");

        return lines;
    }

    private static string KindLabel(VSCodeStorageKind kind) => kind switch
    {
        VSCodeStorageKind.SharedStorageDb => "共享存储",
        VSCodeStorageKind.GlobalStorageDir => "globalStorage",
        VSCodeStorageKind.PortableDataDir => "便携版 data",
        _ => kind.ToString(),
    };
}
