namespace QuickJump;

/// <summary>
/// 搜索门槛规则. "少于 N 个字符就不筛" 这条判断在回退项与三个列表页里共用,
/// 抽成纯函数, 便于脱离 Toolkit 测试 -- 调用方所在的类型都继承扩展 SDK, 测试项目加载不了.
///
/// <para>两个判定各管一头: 列表页用 <see cref="IsTooShort"/> (空查询是浏览视图, 列出全部),
/// 回退项用 <see cref="MeetsMinimum"/> (空查询必须隐藏, 否则空标题会占一行).</para>
/// </summary>
internal static class QueryGate
{
    /// <summary>默认门槛. 单字符几乎必然命中一大批, 先挡掉; 想改可去设置.</summary>
    public const int DefaultMinQueryLength = 2;

    /// <summary>
    /// 设置里的选项值转成门槛. 只认 1 与 2, 其余 (空, 脏值, 旧版本没写这一项) 一律回默认.
    /// </summary>
    public static int ResolveMinQueryLength(string? settingValue) => settingValue switch
    {
        "1" => 1,
        "2" => 2,
        _ => DefaultMinQueryLength,
    };

    /// <summary>
    /// 查询词 (已 Trim) 是否达到门槛. 空查询不算 -- 回退项在空查询时必须隐藏,
    /// 否则它会以空标题占一行.
    /// </summary>
    public static bool MeetsMinimum(string? trimmedQuery, int minQueryLength) =>
        trimmedQuery is { Length: > 0 } query && query.Length >= minQueryLength;

    /// <summary>
    /// 列表页用: 查询词非空但短于门槛. 空查询返回 false -- 空 = 浏览视图, 不是 "太短".
    /// </summary>
    public static bool IsTooShort(string? trimmedQuery, int minQueryLength) =>
        trimmedQuery is { Length: > 0 } && !MeetsMinimum(trimmedQuery, minQueryLength);
}
