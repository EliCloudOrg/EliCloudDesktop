using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EliCloudDesktop.Ui;

/// <summary>
/// 取 WinUI 官方文本样式（<c>TitleTextBlockStyle</c> / <c>BodyTextBlockStyle</c> …）。
/// </summary>
/// <remarks>
/// <para>
/// 官方样式定义在 <c>XamlControlsResources</c> 的合并字典里，不是一个普通的主题颜色。
/// <c>ThemeResource.Get&lt;Style&gt;</c> 在本项目里拿不到它（返回 null，标题会退回 TextBlock 默认 14px），
/// 所以这里直接沿 <see cref="Application.Current"/> 的 <c>Resources</c> + <c>MergedDictionaries</c> 查找。
/// </para>
/// <para>
/// 平台字号/行高/字重全部来自这份官方 Style，页面不写裸值 —— 这是
/// 「Windows 按 Windows 设计规范走」的落点（macOS/Android/Linux 各自用它们的官方样式）。
/// </para>
/// </remarks>
internal static class OfficialStyles
{
    /// <summary>按资源键查找官方样式；找不到返回 <c>null</c>。</summary>
    public static Style? Find(string styleKey)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return null;

        return FindIn(resources, styleKey, depth: 0);
    }

    /// <summary>把官方样式套到原生 TextBlock 上；返回是否成功。</summary>
    public static bool Apply(TextBlock block, string styleKey)
    {
        if (Find(styleKey) is not { } style) return false;

        Reapply(block, style);

        // 挂载前/首次挂载时写 Style 会被 Reactor/隐式样式回落到 TextBlock 默认 14px（实测：
        // 登录后第一次渲染的 Home 标题仍是 Body 字号，切页回来才变 28px）。
        // Loaded 与首帧 LayoutUpdated 各补一次，覆盖「首次挂载」和「已 Loaded 后才挂 setter」两条路径。
        block.Loaded += (_, _) => Reapply(block, style);

        void OnLayoutUpdated(object? sender, object e)
        {
            block.LayoutUpdated -= OnLayoutUpdated;
            Reapply(block, style);
        }

        block.LayoutUpdated += OnLayoutUpdated;
        return true;
    }

    private static void Reapply(TextBlock block, Style style)
    {
        block.ClearValue(TextBlock.FontSizeProperty);
        block.ClearValue(TextBlock.LineHeightProperty);
        block.ClearValue(TextBlock.FontWeightProperty);
        block.Style = style;
    }

    private static Style? FindIn(ResourceDictionary resources, string key, int depth)
    {
        if (depth > 8) return null;

        if (resources.TryGetValue(key, out var value) && value is Style style)
        {
            return style;
        }

        foreach (var merged in resources.MergedDictionaries)
        {
            if (FindIn(merged, key, depth + 1) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
