namespace QuickJump;

/// <summary>
/// 提示行的共用文案. 三个列表页在查询短于门槛时都要在首行说明 "为什么敲了字没筛",
/// 只有条数单位不同. 放在这里, 三处不各抄一份.
///
/// <para>返回标题与副标题, 由各页自己包成 <c>ListItem</c> -- 图标与命令是宿主类型,
/// 纯逻辑层不碰.</para>
/// </summary>
internal static class QueryGateHint
{
    /// <summary>标题: 说清楚门槛是几个字符, 不是还差几个.</summary>
    public static string Title(string query, int minQueryLength) =>
        $"继续输入: \"{query}\" 至少需要 {minQueryLength} 个字符";

    /// <summary>副标题: 当前按浏览视图列出的总条数, 单位由各页给.</summary>
    public static string Subtitle(int total, string unit) => $"当前列出全部 {total} {unit}";
}
