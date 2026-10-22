namespace QuickJump;

/// <summary>
/// 数据源"有没有变"的指纹: 只看最后写入时间, 不读内容.
///
/// <para><b>为什么要它.</b>快照有 30 秒 TTL, 到期后无论源文件变没变都会重读一遍
/// (开 SQLite 库, 解析 JSON, 排序去重). 重读会换掉快照对象, 页面上的行缓存随之全部
/// 作废 -- 用户正在打字时就会撞上一次几百行的重建. 比对指纹后, 源没变只续期 TTL,
/// 快照对象与里面的列表都保持不变, 缓存继续命中.</para>
///
/// <para><b>为什么不直接读内容.</b>内容比对每次要把文件全读一遍, 比它想省的那次重读还贵.
/// mtime 在 NTFS 上是 100ns 精度, 足够发现"VSCode 刚开了个新项目".</para>
///
/// <para>纯 BCL, 不依赖 Toolkit, 便于单测.</para>
/// </summary>
internal static class SourceFingerprint
{
    /// <summary>
    /// 一个文件或目录的指纹, 形如 <c>路径=主文件戳|wal戳</c>. 路径不存在返回空串.
    ///
    /// <para>路径本身也拼进去: 换了一个候选文件时, 光比 mtime 可能恰好相同.</para>
    ///
    /// <para>SQLite 的写入常先落在同名的 <c>-wal</c> 文件里, 主库的 mtime 要等 checkpoint
    /// 才更新 -- 只看主库会把"刚写入"误判成"没变". 因此文件连同 <c>-wal</c> 一起取戳.
    /// 主文件不存在就直接返回空串: 没有主库时 <c>-wal</c> 不算数据源.</para>
    ///
    /// <para><b>存在但取不到时间戳时返回每次都不同的值.</b>那说明权限或文件系统异常;
    /// 若返回一个稳定值, 调用方会把它当成"一直没变", 永远用旧数据.</para>
    /// </summary>
    public static string Of(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return string.Empty;
        }

        var main = Stamp(path);
        if (main is null)
        {
            return $"{path}=?{Guid.NewGuid():N}";
        }

        var wal = Stamp(path + "-wal");
        return wal is null ? $"{path}={main:O}" : $"{path}={main:O}|{wal:O}";
    }

    /// <summary>
    /// 最后写入时间 (UTC). 路径不存在或无权限时返回 null.
    ///
    /// <para><b>必须先判存在.</b><c>File.GetLastWriteTimeUtc</c> 对不存在的路径不抛异常,
    /// 而是返回 1601 年 -- 直接拿来比会把"文件还没出现"和"文件一直存在"混为一谈.</para>
    ///
    /// <para>目录也认: globalStorage 这类位置本身就是目录.</para>
    /// </summary>
    private static DateTime? Stamp(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return null;
            }

            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception)
        {
            // 读不到时间戳不是错误, 由 Of 把它当成"变了"处理.
            return null;
        }
    }
}
