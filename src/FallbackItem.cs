using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickJump;

/// <summary>
/// 根搜索回退项（<see cref="FallbackCommandItem"/>）的共用骨架。VSCode 与 MobaXterm
/// 两个模块各有一个回退项，清文案、过短门、0/1/多命中三分支的形状逐字相同，收在这里。
///
/// <para>子类只实现 <see cref="Resolve"/>：把查询词解析成要展示的命中项（标题/副标题/命令），
/// 缓存未就绪或无命中时返回 null。宿主在根搜索框每敲一次字调用 <see cref="UpdateQuery"/>，
/// 本类负责把旧文案清干净、按命中重填；子类不碰 <c>Title</c>/<c>Subtitle</c>/<c>Command</c>。</para>
/// </summary>
internal abstract partial class FallbackItem : FallbackCommandItem
{
    /// <summary>太短的词几乎必然误命中，也会让根列表每敲一个字就抖动，直接忽略。</summary>
    private const int MinQueryLength = 2;

    /// <summary>上一次查询词，供后台刷新完成后 <see cref="Requery"/> 重放。</summary>
    private string _lastQuery = string.Empty;

    protected FallbackItem(string displayTitle, string id, string iconPath)
        : base(new NoOpCommand(), displayTitle, id)
    {
        // 没有命中时 Title 留空，宿主就不会把这一项显示出来
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = IconHelpers.FromRelativePath(iconPath);
    }

    /// <summary>命中结果：要展示的文案，以及回车要执行/跳转的命令。</summary>
    protected sealed record Hit(string Title, string Subtitle, ICommand Command);

    /// <summary>
    /// 把查询词解析成要展示的命中项；缓存未就绪或无命中时返回 null（本项对宿主隐藏）。
    /// 收到的 query 已 trim 且长度 ≥ <see cref="MinQueryLength"/>。
    /// </summary>
    protected abstract Hit? Resolve(string query);

    /// <summary>
    /// 用当前查询词重跑一次解析。子类在触发后台刷新后可把本方法交给刷新回调 ——
    /// 刷新写完新快照时，回退项据此立刻给出结果，而不是等用户再敲一个字。
    /// </summary>
    protected void Requery() => UpdateQuery(_lastQuery);

    public sealed override void UpdateQuery(string query)
    {
        query = query?.Trim() ?? string.Empty;
        _lastQuery = query;

        // 先清干净，再按命中情况重新填 —— 命中数变化时旧文案不留残影
        Title = string.Empty;
        Subtitle = string.Empty;

        var hit = query.Length < MinQueryLength ? null : Resolve(query);
        if (hit is null)
        {
            Command = new NoOpCommand();
            return;
        }

        Title = hit.Title;
        Subtitle = hit.Subtitle;
        Command = hit.Command;
    }
}
