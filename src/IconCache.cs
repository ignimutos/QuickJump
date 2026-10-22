using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickJump;

/// <summary>
/// 图标实例缓存: 同一个包内路径只造一个 <c>IconInfo</c>.
///
/// <para><b>为什么.</b>列表页每行都要一个 <c>IconInfo</c>, 而几百行的图标其实只有几十种.
/// 原先每行调一次 <c>IconHelpers.FromRelativePath</c> —— 那个方法本身只是拼路径,
/// 但新实例每次都要过一遍 COM 投影; 行缓存命中与否, 首次构建都省不掉这笔.
/// 同一实例返回多次是 Toolkit 支持的用法 (宿主只读 <c>Light</c>/<c>Dark</c>).</para>
///
/// <para>键空间 = 包内图标路径数, 天然有界, 不需要淘汰.</para>
/// </summary>
internal static class IconCache
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, IconInfo> ByPath = new(StringComparer.Ordinal);

    /// <summary>包内相对路径 → 图标. 同一路径返回同一个实例.</summary>
    public static IconInfo From(string relativePath)
    {
        lock (Gate)
        {
            if (!ByPath.TryGetValue(relativePath, out var icon))
            {
                ByPath[relativePath] = icon = IconHelpers.FromRelativePath(relativePath);
            }

            return icon;
        }
    }
}
