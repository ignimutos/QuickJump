using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickJump.MobaXterm;

/// <summary>
/// MobaXterm session 列表页。根搜索入口 "QuickJump MobaXterm" 回车后进入这里。
///
/// <para>结构与 <c>VSCodeListPage</c> 一致：<see cref="DynamicListPage"/> +
/// 页内子串筛选 + 加载态 + 后台刷新 + 诊断项。区别在于数据源、每行的图标，
/// 以及「目录行」—— 点目录推入 <see cref="MobaXtermFolderPage"/> 子页，只列该目录下的会话。</para>
/// </summary>
internal sealed partial class MobaXtermSessionListPage : DynamicListPage
{
    private readonly QuickJumpSettings _settings;
    private readonly MobaXtermSessions _sessions;

    /// <summary>
    /// 按快照复用 session 行, 见 <see cref="RowCache{T}"/>.
    ///
    /// <para><b>目录行不进缓存.</b>每个目录行里嵌着一个 <see cref="MobaXtermFolderPage"/>
    /// 实例; 若整行跨按键复用, 用户第二次进同一个目录时, 那个页面还带着上一次的
    /// <c>SearchText</c>. 目录行本身只有几条, 每次重建的成本可以忽略.</para>
    /// </summary>
    private readonly RowCache<IListItem> _sessionRows = new();

    public MobaXtermSessionListPage(QuickJumpSettings settings, MobaXtermSessions sessions)
    {
        _settings = settings;
        _sessions = sessions;

        Icon = IconHelpers.FromRelativePath(MobaXtermIcons.App);

        // 固定 Id: 宿主拿页面的 Command.Id 当顶层命令 Id. 留空则退回宿主自己算的哈希
        // (提供者 Id + 标题 + 副标题), 改这几个字就会换 Id -- 别名、热键、首页固定项全部失效.
        // 页头 Title 以后可以随便改.
        Id = "QuickJump.mobaxterm.list";

        Title = "MobaXterm Sessions";
        Name = "Open";

        settings.Settings.SettingsChanged += (_, _) => RaiseItemsChanged();

        // 构造早于用户打开面板，先把最慢的一次读取放后台跑，等真进页面时通常已命中缓存。
        IsLoading = true;
        _sessions.RefreshInBackground(IniOverride, OnReadCompleted);
    }

    /// <summary>设置里的 ini 路径覆盖（可能随面板设置改动）。</summary>
    private string? IniOverride => _settings.MobaXtermIniPath.Value;

    /// <summary>设置里的 exe 路径覆盖。</summary>
    private string? ExeOverride => _settings.MobaXtermExePath.Value;

    private void OnReadCompleted()
    {
        IsLoading = false;
        RaiseItemsChanged();
    }

    /// <summary>宿主送来的每次输入。查询词已在 SearchText 上，这里只负责刷列表。</summary>
    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    /// <summary>
    /// 根列表的 Fallback 内联搜索复用同一个页面实例：把预筛词写进
    /// <see cref="ListPage.SearchText"/>，宿主进入本页时会把筛选框也填成这个词。
    ///
    /// <para>理由与 <c>VSCodeListPage.SetFixedQuery</c> 相同：另存一份「固定关键词」并优先采用，
    /// 会让用户进页面后清空搜索框、列表却还是旧结果。这里不自己 RaiseItemsChanged ——
    /// <c>SearchText</c> 的 setter（DynamicListPage）会转手调 <see cref="UpdateSearchText"/>。</para>
    /// </summary>
    public void SetFixedQuery(string? query) => SearchText = query?.Trim() ?? string.Empty;

    public override IListItem[] GetItems()
    {
        var query = SearchText?.Trim();
        var tooShort = QueryGate.IsTooShort(query, _settings.MinQueryLengthValue);

        var overridePath = IniOverride;
        var snapshot = _sessions.TryGetCached();

        if (snapshot is null)
        {
            // 冷态: 唤起一次后台刷新. 单飞守卫会拒掉重复调用.
            _sessions.RefreshInBackground(overridePath, OnReadCompleted);
            return [LoadingItem()];
        }

        // 覆盖路径可能刚被改过: 与快照记录不一致时, 这份快照就不作数.
        var current = snapshot.IniOverride == overridePath;

        // 覆盖变了或 TTL 到期: 后台补一次新数据, 先把旧快照给用户看.
        if (!current || _sessions.IsStale)
        {
            _sessions.RefreshInBackground(overridePath, OnReadCompleted);
        }

        var all = snapshot.Items;

        if (all.Count == 0)
        {
            // 覆盖路径刚改过: 旧快照是空的, 说明新路径还没读. 给加载占位, 别报"没有匹配"
            // 也别报诊断 -- 两者都会让用户以为新路径里没有 session.
            if (!current)
            {
                return [LoadingItem()];
            }

            return [DiagnosticItem(snapshot.Succeeded, snapshot.Failure)];
        }

        return Present(all, query, tooShort);
    }

    /// <summary>
    /// 命中结果 → 行. 按快照复用: 空查询与短查询的有效查询词都是 null, 共用同一份.
    ///
    /// <para>有查询词且一条都没命中时不建缓存项, 无匹配文案每次现算.</para>
    /// </summary>
    private IListItem[] Present(IReadOnlyList<MobaXtermSession> all, string? query, bool tooShort)
    {
        var effective = tooShort ? null : query;
        var key = effective ?? string.Empty;

        // 搜索本身只是几趟子串比较, 目录行的条数也靠它, 所以每次都算.
        var matched = MobaXtermSessions.Search(all, effective);
        if (matched.Count == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = $"没有匹配 “{query}” 的 MobaXterm session",
                    Subtitle = $"共 {all.Count} 条 session",
                    Icon = new IconInfo(""), // SearchAndApps / 放大镜
                },
            ];
        }

        // 只读一次设置: 两次读之间翻转会让目录行与 session 行不同步.
        var showFolders = _settings.MobaXtermShowFolders.Value;

        // 缓存里是共享实例, 发布后一律不改. session 行与「显示目录」无关, 故 variant 为空.
        if (!_sessionRows.TryGet(all, string.Empty, key, out var cached))
        {
            cached = matched.Select(s => MobaXtermSessionRow.Create(s, _settings)).ToArray();
            _sessionRows.Put(all, string.Empty, key, cached);
        }

        if (!showFolders)
        {
            return cached;
        }

        return
        [
            .. MobaXtermSessions.FolderRows(matched, effective)
                .Select(name => FolderItem(name, matched.Count(s => s.Folder == name))),
            .. cached,
        ];
    }

    /// <summary>
    /// 目录行：点它推入 <see cref="MobaXtermFolderPage"/> 子页，只列该目录下的会话。
    /// 命令是 <c>IPage</c>，宿主据此导航并显示返回箭头 —— 不在这里改筛选框
    /// （那会把目录名塞进搜索框，Backspace 退化成删字，见会话记录里的取舍）。
    /// </summary>
    private ListItem FolderItem(string folder, int count) =>
        new(new MobaXtermFolderPage(_settings, _sessions, folder))
        {
            Title = folder,
            Subtitle = $"目录 · {count} 个会话",
            Icon = IconCache.From(MobaXtermIcons.Folder),
        };

    /// <summary>后台首次读取还没完成时的占位项。</summary>
    private static ListItem LoadingItem() =>
        new(new NoOpCommand())
        {
            Title = "正在读取 MobaXterm session…",
            Subtitle = "首次读取可能需要几秒",
        };

    /// <summary>一条都读不到时的提示，并列出实际探测过的位置。</summary>
    private ListItem DiagnosticItem(bool succeeded, string? failure)
    {
        var moreCommands = new List<IContextItem>
        {
            new CommandContextItem(new NoOpCommand())
            {
                Title = "可手动指定 MobaXterm.ini 路径",
                Subtitle = "在扩展设置里填写，或设置环境变量 " + MobaXtermInstall.IniEnvVar,
            },
        };

        moreCommands.AddRange(
            _sessions.DescribeProbedPaths(IniOverride, ExeOverride)
                .Select(line => new CommandContextItem(new CopyTextCommand(line))
                {
                    Title = line,
                    Subtitle = "复制路径",
                }));

        return new ListItem(new NoOpCommand())
        {
            Title = succeeded ? "没有读到 MobaXterm session" : "读取 MobaXterm session 失败",
            Subtitle = succeeded
                ? "找不到 MobaXterm.ini；展开下方命令查看探测过的位置"
                : failure ?? "读取失败；展开下方命令查看探测过的位置",
            Icon = new IconInfo(""), // Warning
            MoreCommands = [.. moreCommands],
        };
    }
}
