namespace QuickJump.VSCode;

/// <summary>
/// 根列表的 Fallback 命中项。在 Command Palette 根搜索框直接敲项目名时，
/// 不必先进入 "VSCode" 页面。骨架见 <see cref="FallbackItem"/>，这里只实现按命中数分发。
///
/// <para><b>唯一命中就直接打开，不进页面。</b>只有一条命中时回车即打开目标 —— 否则用户会
/// 「选中 → 回车 → 进页面 → 再选一次 → 再回车」，白白多两步。这与微软自己的 Indexer 扩展一致
/// （0 条清空、1 条直接给目标命令、多条才给进列表页的入口）。</para>
///
/// <para>标题形态对齐 EverythingExtension 等第三方扩展：多条时写成
/// 「模块名 + 查询词」，图标用模块自己的应用图标（<see cref="VSCodeIcons.App"/>）。</para>
/// </summary>
internal sealed partial class VSCodeFallbackItem : FallbackItem
{
    private readonly VSCodeHistory _history;
    private readonly QuickJumpSettings _settings;

    /// <summary>列表页由提供者持有并复用 —— 这里只引用它，避免多份设置订阅。</summary>
    private readonly VSCodeListPage _listPage;

    public VSCodeFallbackItem(
        QuickJumpSettings settings,
        VSCodeHistory history,
        VSCodeListPage listPage)
        : base("打开 VSCode 最近项目", "QuickJump.vscode.fallback", VSCodeIcons.App)
    {
        _settings = settings;
        _history = history;
        _listPage = listPage;
    }

    protected override Hit? Resolve(string query)
    {
        // 非阻塞：首次读取还没完成时不搜索，否则每敲一个字都会在渲染线程上同步扫描。
        // 后台读完会经 RaiseItemsChanged 让宿主重来。
        if (_history.TryGetCached() is not { } snapshot)
        {
            return null;
        }

        // 设置可能在面板里刚被改过，每次按当前值过滤
        var matched = VSCodeHistory.Search(snapshot.Items, query, _settings.ShowFiles.Value);
        if (matched.Count == 0)
        {
            return null;
        }

        if (matched.Count == 1)
        {
            // 唯一命中：回车直接打开，不绕道列表页
            var single = matched[0];
            return new Hit(
                $"在 VSCode 中打开 “{single.Title}”",
                $"{single.TypeLabel} · {single.Path}",
                new OpenInVSCodeCommand(single.Target, _settings));
        }

        // 多条命中：进列表页挑。预筛词同时填进筛选框，用户接着改也顺。
        _listPage.SetFixedQuery(query);
        return new Hit(
            $"在 VSCode 中打开 “{query}”",
            $"命中 {matched.Count} 个最近项目",
            _listPage);
    }
}
