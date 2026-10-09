using Xunit;
using VSCodeRecent.MobaXterm;

namespace VSCodeRecent.Tests;

/// <summary>session 搜索与启动参数拼接（纯逻辑，不碰文件系统、不加载 Toolkit）。</summary>
public class MobaXtermSessionTests
{
    private static MobaXtermSession Ssh(string name, string folder, string host) => new()
    {
        Name = name,
        Folder = folder,
        Protocol = MobaProtocol.Ssh,
        SessionPath = MobaXtermIni.BuildSessionPath(folder, name),
        Host = host,
        Port = "22",
        User = "root",
        Order = 0,
    };

    [Fact]
    public void Search_MatchesNameFolderAndHost()
    {
        var items = new List<MobaXtermSession>
        {
            Ssh("gateway", "ld", "192.168.31.100"),
            Ssh("aiyun", "remote", "38.105.28.110"),
        };

        Assert.Single(MobaXtermSessions.Search(items, "gate"));
        Assert.Single(MobaXtermSessions.Search(items, "remote"));
        Assert.Single(MobaXtermSessions.Search(items, "38.105"));
        Assert.Equal(2, MobaXtermSessions.Search(items, "10").Count);
    }

    [Fact]
    public void Search_EmptyQueryReturnsAll()
    {
        var items = new List<MobaXtermSession> { Ssh("a", "", "h") };
        Assert.Single(MobaXtermSessions.Search(items, "  "));
    }

    [Fact]
    public void InFolder_MatchesExactFolderNotSubstring()
    {
        var items = new List<MobaXtermSession>
        {
            Ssh("a", "prod", "h"),
            Ssh("b", "prod-backup", "h"),   // 名字含 prod，但不是 prod 目录
            Ssh("prod-x", "other", "h"),    // 名字含 prod，在别的目录
        };

        // 子串匹配会把三条都带出来；精确归属只留真的在 prod 目录里的那一条。
        Assert.Equal(3, MobaXtermSessions.Search(items, "prod").Count);
        var inFolder = MobaXtermSessions.InFolder(items, "prod");
        Assert.Single(inFolder);
        Assert.Equal("a", inFolder[0].Name);
    }

    [Fact]
    public void InFolder_IsCaseInsensitive()
    {
        var items = new List<MobaXtermSession> { Ssh("a", "Remote", "h") };
        Assert.Single(MobaXtermSessions.InFolder(items, "remote"));
    }

    [Fact]
    public void BookmarkArguments_QuotesWholePathAndDoesNotAddNewTab()
    {
        var session = Ssh("aiyun", "remote", "h");

        // 路径必须整体带引号（含空格的 "User sessions"）；且不能加 -newtab ——
        // -newtab 会把后跟参数当成要执行的命令。
        Assert.Equal(
            "-bookmark \"User sessions\\remote\\aiyun\"",
            session.BookmarkArguments);
    }
}
