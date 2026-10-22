using Xunit;

namespace QuickJump.Tests;

/// <summary>
/// 行缓存: 按源引用与开关失效, 按有效查询词命中. 纯 BCL, 不加载 Toolkit.
/// </summary>
public class RowCacheTests
{
    /// <summary>只用来当引用标识, 内容无所谓.</summary>
    private sealed class Source
    {
    }

    [Fact]
    public void TryGet_HitsSameSourceVariantAndQuery()
    {
        var cache = new RowCache<string>();
        var source = new Source();
        string[] rows = ["a", "b"];

        cache.Put(source, "projects", "", rows);

        Assert.True(cache.TryGet(source, "projects", "", out var hitRows));
        Assert.Same(rows, hitRows);
    }

    /// <summary>快照一换 (新条目列表), 旧行不能再命中 —— 否则会显示过期数据.</summary>
    [Fact]
    public void TryGet_MissesAfterSourceChanges()
    {
        var cache = new RowCache<string>();
        cache.Put(new Source(), "projects", "re", ["old"]);

        Assert.False(cache.TryGet(new Source(), "projects", "re", out _));
    }

    /// <summary>「显示文件」开关一变, 同一份快照的行也不作数.</summary>
    [Fact]
    public void TryGet_MissesAfterVariantChanges()
    {
        var cache = new RowCache<string>();
        var source = new Source();
        cache.Put(source, "projects", "re", ["old"]);

        Assert.False(cache.TryGet(source, "files", "re", out _));
    }

    /// <summary>切换开关后只保留当前开关那一份 —— 切回去要重建, 这是有意的.</summary>
    [Fact]
    public void SwitchingVariantKeepsOnlyCurrentOne()
    {
        var cache = new RowCache<string>();
        var source = new Source();
        cache.Put(source, "projects", "re", ["p"]);

        Assert.False(cache.TryGet(source, "files", "re", out _));

        cache.Put(source, "files", "re", ["f"]);

        Assert.True(cache.TryGet(source, "files", "re", out var kept));
        Assert.Single(kept);
        Assert.Equal("f", kept[0]);

        // 切回 "projects": 上面那份被清掉, 需要重建.
        Assert.False(cache.TryGet(source, "projects", "re", out _));
    }

    /// <summary>查询词不同各存一份, 相邻几次输入都能命中.</summary>
    [Fact]
    public void DifferentQueriesCoexist()
    {
        var cache = new RowCache<string>();
        var source = new Source();
        cache.Put(source, "projects", "r", ["r"]);
        cache.Put(source, "projects", "re", ["re"]);

        Assert.True(cache.TryGet(source, "projects", "r", out _));
        Assert.True(cache.TryGet(source, "projects", "re", out _));
    }

    /// <summary>
    /// 超过上限只淘汰最久未用的那一项, 不清空整表. 整表清空的话, 打字到第 5 个字符
    /// 会把前面几个相邻词全丢掉, 用户退格时又得逐级重建.
    /// </summary>
    [Fact]
    public void ExceedingLimitEvictsOnlyLeastRecentlyUsed()
    {
        var cache = new RowCache<string>();
        var source = new Source();
        foreach (var query in new[] { "a", "b", "c", "d" })
        {
            cache.Put(source, "projects", query, [query]);
        }

        // 再取一次 "a", 让它变成最近用过的一个.
        Assert.True(cache.TryGet(source, "projects", "a", out _));

        cache.Put(source, "projects", "e", ["e"]);

        Assert.True(cache.TryGet(source, "projects", "a", out _), "'a' 刚被取过, 不该被淘汰");
        Assert.True(cache.TryGet(source, "projects", "e", out _));
        Assert.False(cache.TryGet(source, "projects", "b", out _), "'b' 才是最久未用的");
    }

    /// <summary>浏览视图那一项 (空查询) 被取过就不该被挤掉 —— 退格回单字符靠它.</summary>
    [Fact]
    public void BrowseEntrySurvivesWhileQueriesChurn()
    {
        var cache = new RowCache<string>();
        var source = new Source();
        cache.Put(source, "projects", "", ["all"]);

        foreach (var query in new[] { "a", "b", "c", "d" })
        {
            Assert.True(cache.TryGet(source, "projects", "", out _), "浏览视图应一直命中");
            cache.Put(source, "projects", query, [query]);
        }

        Assert.True(cache.TryGet(source, "projects", "", out var all));
        Assert.Equal("all", all[0]);
    }

    /// <summary>大小写不同算同一个查询词 —— 与检索规则一致, 不该各占一项.</summary>
    [Fact]
    public void QueryKeysIgnoreCase()
    {
        var cache = new RowCache<string>();
        var source = new Source();
        cache.Put(source, "projects", "re", ["re"]);

        Assert.True(cache.TryGet(source, "projects", "RE", out var rows));
        Assert.Equal("re", rows[0]);
    }

    /// <summary>
    /// 构建期间快照换了: 用旧快照建好的行不能写进已指向新快照的缓存.
    ///
    /// <para>顺序是 "先按旧源取, 再按新源写" —— 写入方拿到的是新源, 缓存还停在旧源,
    /// 于是这次写入被丢弃; 下一次访问换到新源并清空.</para>
    /// </summary>
    [Fact]
    public void Put_DoesNotAcceptRowsForAnotherSource()
    {
        var cache = new RowCache<string>();
        var older = new Source();
        var newer = new Source();

        cache.Put(older, "projects", "", ["older"]);

        // 缓存当前源是 older, 这次写入用的是 newer: 丢弃.
        cache.Put(newer, "projects", "re", ["stale"]);

        Assert.False(cache.TryGet(newer, "projects", "re", out _));

        // 换成新源后, 旧的 "" 项也不该再命中.
        Assert.False(cache.TryGet(newer, "projects", "", out _));
    }
}
