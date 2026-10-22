using Xunit;

namespace QuickJump.Tests;

/// <summary>
/// 源文件指纹: 只看 mtime, 但 SQLite 的 -wal 要一起算. 用临时目录, 不碰真实数据.
/// </summary>
public class SourceFingerprintTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "quickjump-fp-" + Guid.NewGuid().ToString("N"));

    public SourceFingerprintTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (Exception)
        {
            // 测试清理失败不影响结论.
        }
    }

    private string InDir(string name) => Path.Combine(_dir, name);

    [Fact]
    public void MissingPathIsEmpty()
    {
        Assert.Equal(string.Empty, SourceFingerprint.Of(InDir("nope.vscdb")));
    }

    [Fact]
    public void ExistingFileIsNotEmpty()
    {
        var path = InDir("state.vscdb");
        File.WriteAllText(path, "x");

        Assert.NotEqual(string.Empty, SourceFingerprint.Of(path));
    }

    /// <summary>写入内容 (mtime 前移) 必须让指纹变化, 否则新项目不会被读出来.</summary>
    [Fact]
    public void ContentChangeMovesFingerprint()
    {
        var path = InDir("state.vscdb");
        File.WriteAllText(path, "x");
        var before = SourceFingerprint.Of(path);

        File.WriteAllText(path, "xx");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Assert.NotEqual(before, SourceFingerprint.Of(path));
    }

    /// <summary>SQLite 常先写 -wal 再 checkpoint; 建 wal 也必须算作变化.</summary>
    [Fact]
    public void WalAppearingMovesFingerprint()
    {
        var path = InDir("state.vscdb");
        File.WriteAllText(path, "x");
        var before = SourceFingerprint.Of(path);

        File.WriteAllText(path + "-wal", "wal");

        Assert.NotEqual(before, SourceFingerprint.Of(path));
    }

    /// <summary>checkpoint 后 wal 被删, 同样要算变化 —— 那意味着主库刚被改过.</summary>
    [Fact]
    public void WalDisappearingMovesFingerprint()
    {
        var path = InDir("state.vscdb");
        File.WriteAllText(path, "x");
        File.WriteAllText(path + "-wal", "wal");
        var before = SourceFingerprint.Of(path);

        File.Delete(path + "-wal");

        Assert.NotEqual(before, SourceFingerprint.Of(path));
    }

    /// <summary>目录也算数据源位置 (globalStorage 下一批库), 必须给出稳定指纹.</summary>
    [Fact]
    public void DirectoryIsRecognised()
    {
        Assert.NotEqual(string.Empty, SourceFingerprint.Of(_dir));
        Assert.Equal(SourceFingerprint.Of(_dir), SourceFingerprint.Of(_dir));
    }

    /// <summary>指纹带路径: 两个内容时间相同的文件不能混为一谈.</summary>
    [Fact]
    public void SameTimestampDifferentPathDiffers()
    {
        var a = InDir("a.vscdb");
        var b = InDir("b.vscdb");
        File.WriteAllText(a, "x");
        File.WriteAllText(b, "x");
        var stamp = DateTime.UtcNow.AddMinutes(-5);
        File.SetLastWriteTimeUtc(a, stamp);
        File.SetLastWriteTimeUtc(b, stamp);

        Assert.NotEqual(SourceFingerprint.Of(a), SourceFingerprint.Of(b));
    }
}
