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

        // 目录归属用精确匹配（不因子串把 prod-backup 之类带进来），再按框内词做子串筛选。
        var inFolder = MobaXtermSessions.InFolder(snapshot.Items, _folder);
        var matched = MobaXtermSessions.Search(inFolder, SearchText?.Trim());

        if (matched.Count == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = $"没有匹配 “{SearchText?.Trim()}” 的 session",
                    Subtitle = $"“{_folder}” 目录下共 {inFolder.Count} 条 session",
                    Icon = new IconInfo(""), // SearchAndApps / 放大镜
                },
            ];
        }

        return [.. matched.Select(s => MobaXtermSessionRow.Create(s, _settings))];
    }
}
