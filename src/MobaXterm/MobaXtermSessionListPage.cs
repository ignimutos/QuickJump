using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickJump.MobaXterm;

/// <summary>
/// MobaXterm session 列表页。顶层命令 "MobaXterm Sessions" 回车后进入这里。
///
/// <para>结构与 <c>VSCodeListPage</c> 一致：<see cref="DynamicListPage"/> +
/// 页内子串筛选 + 加载态 + 后台刷新 + 诊断项。区别在于数据源、每行的图标，
/// 以及「目录行」—— 点目录推入 <see cref="MobaXtermFolderPage"/> 子页，只列该目录下的会话。</para>
/// </summary>
internal sealed partial class MobaXtermSessionListPage : DynamicListPage
{
    private readonly QuickJumpSettings _settings;
    private readonly MobaXtermSessions _sessions;

    public MobaXtermSessionListPage(QuickJumpSettings settings, MobaXtermSessions sessions)
    {
        _settings = settings;
        _sessions = sessions;

        Icon = IconHelpers.FromRelativePath(MobaXtermIcons.App);
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

    public override IListItem[] GetItems()
    {
        // 覆盖路径可能刚被改过：与快照记录的不一致时，这份快照就不作数。
        var overridePath = IniOverride;
        var snapshot = _sessions.TryGetCached();

        if (snapshot is null || snapshot.IniOverride != overridePath)
        {
            // 冷态 / 覆盖变了：唤起一次后台刷新。单飞守卫会拒掉重复调用。
            _sessions.RefreshInBackground(overridePath, OnReadCompleted);

            // 只有从未读过（冷）才给占位；覆盖刚变但已有旧快照时，先拿旧数据顶着。
            return snapshot is null ? [LoadingItem()] : [.. BuildItems(snapshot.Items)];
        }

        if (_sessions.IsStale)
        {
            _sessions.RefreshInBackground(overridePath, OnReadCompleted);
        }

        var all = snapshot.Items;
        if (all.Count == 0)
        {
            return [DiagnosticItem(snapshot.Succeeded, snapshot.Failure)];
        }

        var items = BuildItems(all);
        if (items.Count == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = $"没有匹配 “{SearchText?.Trim()}” 的 MobaXterm session",
                    Subtitle = $"共 {all.Count} 条 session",
                    Icon = new IconInfo(""), // SearchAndApps / 放大镜
                },
            ];
        }

        return [.. items];
    }

    /// <summary>目录行在前，随后是筛选后的 session 行。</summary>
    private List<IListItem> BuildItems(IReadOnlyList<MobaXtermSession> all)
    {
        var query = SearchText?.Trim();
        var matched = MobaXtermSessions.Search(all, query);

        var items = new List<IListItem>();
        if (_settings.MobaXtermShowFolders.Value)
        {
            foreach (var name in Folders(matched))
            {
                items.Add(FolderItem(name, matched.Count(s => s.Folder == name)));
            }
        }

        items.AddRange(matched.Select(s => MobaXtermSessionRow.Create(s, _settings)));
        return items;
    }

    /// <summary>当前可见 session 里出现过的目录（去重、稳定顺序）。</summary>
    private static IEnumerable<string> Folders(IReadOnlyList<MobaXtermSession> sessions) =>
        sessions.Select(s => s.Folder).Where(f => f.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);

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
            Icon = IconHelpers.FromRelativePath(MobaXtermIcons.Folder),
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
