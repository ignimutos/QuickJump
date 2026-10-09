using Xunit;
using QuickJump.MobaXterm;

namespace QuickJump.Tests;

/// <summary>
/// MobaXterm.ini 的 [Bookmarks*] 解析。全部只喂字符串，不碰文件系统 ——
/// 与 <see cref="ParseHistoryKeyTests"/> 同级。
/// </summary>
public class MobaXtermIniTests
{
    // 本机样本：SSH(fax6/gateway)、WSL、带目录层级与转义
    private const string SampleIni = """
        [Misc]
        Version=26.5

        [Bookmarks]
        SubRep=
        ImgNum=42
        WSL-Debian=#149#14%Debian%%Interactive shell%__PTVIRG__fish__PTVIRG__%

        [Bookmarks_1]
        SubRep=ld
        ax6=#109#0%192.168.31.1%22%[ax6]%%-1%
        gateway=#109#0%192.168.31.100%14234%[default]%%0%

        [Bookmarks_2]
        SubRep=remote
        aiyun=#109#0%38.105.28.110%14234%[default]%%0%
        """;

    [Fact]
    public void RootSection_SessionPathHasNoFolder()
    {
        var sessions = MobaXtermIni.ParseBookmarks(SampleIni, out var error);

        Assert.Null(error);
        var wsl = Assert.Single(sessions!, s => s.Name == "WSL-Debian");
        Assert.Equal(string.Empty, wsl.Folder);
        Assert.Equal("User sessions\\WSL-Debian", wsl.SessionPath);
        Assert.Equal(MobaProtocol.Wsl, wsl.Protocol);
        Assert.Equal("Debian", wsl.Host);
    }

    [Fact]
    public void SubRepFolder_IsPrependedToSessionPath()
    {
        var sessions = MobaXtermIni.ParseBookmarks(SampleIni, out _);

        var gateway = Assert.Single(sessions!, s => s.Name == "gateway");
        Assert.Equal("ld", gateway.Folder);
        Assert.Equal("User sessions\\ld\\gateway", gateway.SessionPath);
    }

    [Fact]
    public void SshFields_AreParsed()
    {
        var sessions = MobaXtermIni.ParseBookmarks(SampleIni, out _);

        var aiyun = Assert.Single(sessions!, s => s.Name == "aiyun");
        Assert.Equal(MobaProtocol.Ssh, aiyun.Protocol);
        Assert.Equal("38.105.28.110", aiyun.Host);
        Assert.Equal("14234", aiyun.Port);
        Assert.Equal("[default]", aiyun.User);
        Assert.Equal("remote · [default]@38.105.28.110:14234", aiyun.Subtitle);
    }

    [Fact]
    public void EscapedSemicolon_IsDecodedInFields()
    {
        // 值里带 __PTVIRG__ 的字段还原成 ;
        const string ini = "[Bookmarks]\nx=#109#0%host%22%a__PTVIRG__b%\n";
        var sessions = MobaXtermIni.ParseBookmarks(ini, out _);

        var session = Assert.Single(sessions!);
        Assert.Equal("a;b", session.User);
    }

    [Fact]
    public void ReservedKeys_AreNotTreatedAsSessions()
    {
        var sessions = MobaXtermIni.ParseBookmarks(SampleIni, out _);

        Assert.DoesNotContain(sessions!, s => s.Name is "SubRep" or "ImgNum");
        Assert.Equal(4, sessions!.Count);
    }

    [Fact]
    public void DifferentFoldersWithSameName_AreKept()
    {
        var ini = """
            [Bookmarks_1]
            SubRep=a
            dup=#109#0%h1%22%u
            [Bookmarks_2]
            SubRep=b
            dup=#109#0%h2%22%u
            """;

        var sessions = MobaXtermIni.ParseBookmarks(ini, out _);

        Assert.Equal(2, sessions!.Count);
        Assert.Equal("User sessions\\a\\dup", sessions[0].SessionPath);
        Assert.Equal("User sessions\\b\\dup", sessions[1].SessionPath);
    }

    [Fact]
    public void MalformedValue_IsSkippedWithoutFailingOthers()
    {
        var ini = """
            [Bookmarks]
            good=#109#0%host%22%u
            empty=
            notsession=whatever
            """;

        var sessions = MobaXtermIni.ParseBookmarks(ini, out var error);

        Assert.Null(error);
        var session = Assert.Single(sessions!);
        Assert.Equal("good", session.Name);
    }

    [Fact]
    public void NoBookmarksSection_IsEmptyNotFailure()
    {
        var sessions = MobaXtermIni.ParseBookmarks("[Misc]\nVersion=26.5\n", out var error);

        Assert.Null(error);
        Assert.Empty(sessions!);
    }

    [Fact]
    public void BlankInput_YieldsEmpty()
    {
        Assert.Empty(MobaXtermIni.ParseBookmarks("   \n  ", out var error)!);
        Assert.Null(error);
    }

    [Fact]
    public void AllEntriesUnparsable_IsHardFailure()
    {
        var ini = """
            [Bookmarks]
            a=not-a-session
            b=also-broken
            """;

        Assert.Null(MobaXtermIni.ParseBookmarks(ini, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void UnknownProtocolNumber_IsKeptWithOtherKind()
    {
        var ini = """
            [Bookmarks]
            rdp=#110#0%host%3389%user
            """;

        var sessions = MobaXtermIni.ParseBookmarks(ini, out _);

        var session = Assert.Single(sessions!);
        Assert.Equal(MobaProtocol.Other, session.Protocol);
        Assert.Equal("User sessions\\rdp", session.SessionPath);
    }
}
