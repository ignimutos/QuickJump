using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickJump.MobaXterm;

/// <summary>
/// 单个 MobaXterm 目录的 session 列表页。主页（<see cref="MobaXtermSessionListPage"/>）
/// 点目录行时推入本页 —— 命令是 <see cref="DynamicListPage"/>（宿主认作 <c>IPage</c>），
/// 因此宿主机自己装上返回箭头，返回也由它处理；本页不碰输入框、不另存「上级」状态。
///
/// <para>每次点目录行新建一个实例。它只读共享的 <see cref="MobaXtermSessions"/> 缓存快照
/// （点得进来说明主页已读过），不订阅设置、不主动刷新 —— 瞬态页持有订阅会泄漏。</para>
/// </summary>
internal sealed partial class MobaXtermFolderPage : DynamicListPage
{
    private readonly QuickJumpSettings _settings;
    private readonly MobaXtermSessions _sessions;
    private readonly string _folder;

    /// <summary>按快照复用行, 见 <see cref="RowCache{T}"/>.</summary>
    private readonly RowCache<IListItem> _rows = new();

    public MobaXtermFolderPage(QuickJumpSettings settings, MobaXtermSessions sessions, string folder)
    {
        _settings = settings;
        _sessions = sessions;
        _folder = folder;

        Icon = IconHelpers.FromRelativePath(MobaXtermIcons.Folder);
        Title = folder;
        Name = "Open";
    }

    /// <summary>宿主机每次输入：查询词已在 <see cref="ListPage.SearchText"/> 上，这里只刷列表。</summary>
    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var snapshot = _sessions.TryGetCached();
        if (snapshot is null || snapshot.Items.Count == 0)
        {
            // 主页能推到本页说明快照已就绪；真的空了给一条不误导的提示，别假装在加载。
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "没有读到 MobaXterm session",
                    Subtitle = "回到上一页刷新后重试",
                    Icon = new IconInfo(""), // Warning
                },
            ];
        }

        var query = SearchText?.Trim();
        var tooShort = QueryGate.IsTooShort(query, _settings.MinQueryLengthValue);

        // 本页是瞬时实例, 缓存只在这次进出内有意义. 源用快照里的完整列表 --
        // InFolder 每次都新建列表, 拿它当键会永远不命中. 目录名是实例常量, 不进键.
        var effective = tooShort ? null : query;
        var key = effective ?? string.Empty;

        // 缓存里是共享实例, 发布后一律不改.
        if (_rows.TryGet(snapshot.Items, string.Empty, key, out var rows))
        {
            return rows;
        }

        // 目录归属用精确匹配 (不因子串把 prod-backup 之类带进来), 再按框内词做子串筛选.
        // 查询短于门槛时传 null, 按浏览视图列出全部.
        var inFolder = MobaXtermSessions.InFolder(snapshot.Items, _folder);
        var matched = MobaXtermSessions.Search(inFolder, effective);

        if (matched.Count == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = $"没有匹配 “{query}” 的 session",
                    Subtitle = $"“{_folder}” 目录下共 {inFolder.Count} 条 session",
                    Icon = new IconInfo(""), // SearchAndApps / 放大镜
                },
            ];
        }

        rows = matched.Select(s => MobaXtermSessionRow.Create(s, _settings)).ToArray();
        _rows.Put(snapshot.Items, string.Empty, key, rows);
        return rows;
    }
}
