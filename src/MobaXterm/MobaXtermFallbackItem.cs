namespace QuickJump.MobaXterm;

/// <summary>
/// 根列表的 Fallback 命中项。在 Command Palette 根搜索框直接敲 session 名时，
/// 不必先进入 "MobaXterm Sessions" 页面。骨架见 <see cref="FallbackItem"/>，这里只实现按命中数分发。
///
/// <para><b>唯一命中就直接连接，不进页面。</b>只有一条命中时回车即连接 —— 与 VSCode 回退项一致。</para>
/// </summary>
internal sealed partial class MobaXtermFallbackItem : FallbackItem
{
    private readonly MobaXtermSessions _sessions;
    private readonly QuickJumpSettings _settings;

    /// <summary>列表页由提供者持有并复用 —— 这里只引用它，避免多份设置订阅。</summary>
    private readonly MobaXtermSessionListPage _listPage;

    public MobaXtermFallbackItem(
        QuickJumpSettings settings,
        MobaXtermSessions sessions,
        MobaXtermSessionListPage listPage)
        : base("打开 MobaXterm 会话", "QuickJump.mobaxterm.fallback", MobaXtermIcons.App)
    {
        _settings = settings;
        _sessions = sessions;
        _listPage = listPage;
    }

    protected override Hit? Resolve(string query)
    {
        var snapshot = _sessions.TryGetCached();

        // 冷态（还没读到过）：不搜索，也不发起刷新 —— 列表页构造时已在后台读过。
        if (snapshot is null)
        {
            return null;
        }

        // 快照记的覆盖路径与当前设置不一致（比如用户刚改过 MobaXterm.ini 路径）：
        // 这份快照不作数。列表页此时会后台重读，这里照做，否则回退项会一直静默到
        // 用户碰巧打开一次列表页。刷新写完快照后回调 Requery 立刻给出结果。
        if (snapshot.IniOverride != _settings.MobaXtermIniPath.Value)
        {
            _sessions.RefreshInBackground(_settings.MobaXtermIniPath.Value, Requery);
            return null;
        }

        // 与列表页同一套匹配规则（名字 / 目录 / host 子串），不另写一份。
        var matched = MobaXtermSessions.Search(snapshot.Items, query);
        if (matched.Count == 0)
        {
            return null;
        }

        if (matched.Count == 1)
        {
            // 唯一命中：回车直接连接，不绕道列表页
            var single = matched[0];
            return new Hit(
                $"在 MobaXterm 中连接 “{single.Name}”",
                single.Subtitle,
                new OpenInMobaXtermCommand(single, _settings));
        }

        // 多条命中：进列表页挑。预筛词同时填进筛选框，用户接着改也顺。
        _listPage.SetFixedQuery(query);
        return new Hit(
            $"在 MobaXterm 中连接 “{query}”",
            $"命中 {matched.Count} 个 session",
            _listPage);
    }
}
