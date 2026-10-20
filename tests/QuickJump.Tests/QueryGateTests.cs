using Xunit;

namespace QuickJump.Tests;

/// <summary>搜索门槛规则 (纯逻辑, 不加载 Toolkit).</summary>
public class QueryGateTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData(null, QueryGate.DefaultMinQueryLength)]
    [InlineData("", QueryGate.DefaultMinQueryLength)]
    [InlineData("abc", QueryGate.DefaultMinQueryLength)]
    [InlineData("0", QueryGate.DefaultMinQueryLength)]
    public void Resolve_AcceptsOnlyOneOrTwo(string? value, int expected) =>
        Assert.Equal(expected, QueryGate.ResolveMinQueryLength(value));

    [Fact]
    public void IsTooShort_EmptyQueryIsBrowseView()
    {
        // 空查询 = 浏览视图, 不是 "太短"; 各页本来就要列出全部.
        Assert.False(QueryGate.IsTooShort(string.Empty, 2));
        Assert.False(QueryGate.IsTooShort(null, 2));
    }

    [Fact]
    public void IsTooShort_WhitespaceIsTrimmedByCallers()
    {
        // 契约: 调用方先 Trim. 这里只是把这条前提固定下来, 免得以后直接传原始输入.
        Assert.True(QueryGate.IsTooShort(" ", 2));
    }

    [Fact]
    public void MeetsMinimum_EmptyQueryNeverPasses()
    {
        // 回退项在空查询下必须隐藏, 否则会以空标题占一行.
        Assert.False(QueryGate.MeetsMinimum(string.Empty, 1));
        Assert.False(QueryGate.MeetsMinimum(null, 1));
        Assert.True(QueryGate.MeetsMinimum("r", 1));
    }

    [Fact]
    public void IsTooShort_SingleCharacterDependsOnThreshold()
    {
        Assert.True(QueryGate.IsTooShort("r", 2));
        Assert.False(QueryGate.IsTooShort("r", 1));
    }

    [Fact]
    public void IsTooShort_TwoCharactersPassEitherThreshold()
    {
        Assert.False(QueryGate.IsTooShort("re", 2));
        Assert.False(QueryGate.IsTooShort("re", 1));
    }
}
