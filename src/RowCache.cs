using System.Diagnostics.CodeAnalysis;

namespace QuickJump;

/// <summary>
/// 列表页的行缓存: 同一份数据、同一组开关下, 每个有效查询词只构建一次行.
///
/// <para><b>为什么需要.</b>浏览视图 (查询短于门槛, 或空查询) 一次要建几百上千个
/// <c>ListItem</c>. 宿主按引用相等复用行视图模型, 但行对象每次都是新实例, 于是每次按键
/// 都要整批重建再整批发布 -- 用户看到的就是"退格到 1 个字符时列表要等一会儿".
/// 缓存后空查询与短查询共用同一份行, 退格那一下直接命中.</para>
///
/// <para><b>失效靠引用.</b><c>source</c> 传快照里的条目列表: 快照一换
/// (后台刷新读到新数据), 引用就变, 缓存整体作废. 页面自己的开关 (显示文件 / 显示目录) 走
/// <c>variant</c>. 两者都没变时, 数据层续期 TTL 不会踩掉缓存 -- 续期时复用同一个列表.</para>
///
/// <para><b>淘汰最久未用的那一项, 不清空整表.</b>打字到第 5 个字符就把前几项全丢掉的话,
/// 用户退格时又得逐级重建, 与缓存的目的相反.</para>
///
/// <para><b>返回的是缓存里那份数组, 调用方不得改写.</b>行对象发布后改属性会触发
/// <c>PropChanged</c>, 宿主的视图模型借用该事件且不退订, 改动会串到别处.</para>
///
/// <para>纯 BCL, 不依赖 Toolkit, 便于单测.</para>
/// </summary>
internal sealed class RowCache<T>
{
    /// <summary>同时保留几个查询词的行. 打字过程里前后相邻的几个词够用, 多了只占内存.</summary>
    private const int MaxQueries = 4;

    private readonly object _gate = new();

    /// <summary>
    /// 按查询词存行, 同时记最后一次命中的序号, 用来淘汰最久未用的那一项.
    ///
    /// <para>忽略大小写与检索规则一致 (<c>VSCodeHistory.Search</c> 等都用
    /// <c>OrdinalIgnoreCase</c>): 否则 <c>RE</c> 与 <c>re</c> 各占一项, 白白挤掉别的词.</para>
    /// </summary>
    private readonly Dictionary<string, (T[] Rows, long Used)> _byQuery =
        new(StringComparer.OrdinalIgnoreCase);
    private object? _source;
    private string _variant = string.Empty;
    private long _clock;

    /// <summary>
    /// 取缓存. <paramref name="query"/> 用有效查询词 (浏览视图统一传空串,
    /// 这样空框与"太短"共享同一项).
    /// </summary>
    /// <returns>命中为 true.</returns>
    /// <param name="rows">命中的行. 命中时是缓存里那份数组, 调用方不得改写.</param>
    public bool TryGet(object source, string variant, string query, [MaybeNullWhen(false)] out T[] rows)
    {
        lock (_gate)
        {
            Rebase(source, variant);

            if (!_byQuery.TryGetValue(query, out var entry))
            {
                rows = null;
                return false;
            }

            // 记一次使用: 淘汰按"最久没被取过", 而不是"最早放进来".
            _byQuery[query] = (entry.Rows, ++_clock);
            rows = entry.Rows;
            return true;
        }
    }

    /// <summary>
    /// 放入缓存. 构建在锁外进行, 期间快照可能已经换了 -- 那时这份结果属于旧数据,
    /// 丢弃并让缓存以新快照重新开始.
    /// </summary>
    public void Put(object source, string variant, string query, T[] rows)
    {
        lock (_gate)
        {
            // 缓存里已有源且与这次不同: 构建期间快照或开关换了, 这份结果按旧数据建的,
            // 丢弃. 缓存仍指向旧源, 下一次 TryGet 会换成新源并清空.
            if (_source is not null && !IsCurrent(source, variant))
            {
                return;
            }

            Rebase(source, variant);

            if (!_byQuery.ContainsKey(query) && _byQuery.Count >= MaxQueries)
            {
                EvictLeastRecentlyUsed();
            }

            _byQuery[query] = (rows, ++_clock);
        }
    }

    /// <summary>丢掉最久没被取过的一项. 调用方已持锁.</summary>
    private void EvictLeastRecentlyUsed()
    {
        var oldest = default(KeyValuePair<string, (T[] Rows, long Used)>);
        var first = true;

        foreach (var pair in _byQuery)
        {
            if (first || pair.Value.Used < oldest.Value.Used)
            {
                oldest = pair;
                first = false;
            }
        }

        if (!first)
        {
            _byQuery.Remove(oldest.Key);
        }
    }

    private bool IsCurrent(object source, string variant) =>
        ReferenceEquals(_source, source) && _variant == variant;

    /// <summary>源或开关变了就整体清空并换上新标识. 调用方已持锁.</summary>
    private void Rebase(object source, string variant)
    {
        if (IsCurrent(source, variant))
        {
            return;
        }

        _byQuery.Clear();
        _source = source;
        _variant = variant;
    }
}
