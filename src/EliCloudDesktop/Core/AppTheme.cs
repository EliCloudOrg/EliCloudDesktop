
using Microsoft.UI.Xaml;

namespace EliCloudDesktop.Core;

/// <summary>客户端的配色主题。</summary>
/// <remarks>
/// 默认 <see cref="System"/>：不指定元素级主题，颜色随 Windows 的亮/暗设置走。
/// 设置页可以固定成浅色或深色。
/// </remarks>
internal enum AppTheme
{
    /// <summary>跟随系统（默认）。</summary>
    System = 0,

    /// <summary>浅色。</summary>
    Light,

    /// <summary>深色。</summary>
    Dark,
}

/// <summary>把应用主题映射成 XAML 的 <see cref="ElementTheme"/>。</summary>
/// <remarks>
/// 根节点和 ContentDialog 都要用这一份：弹窗挂在 XamlRoot 的浮层上，不会继承应用根节点的
/// <c>RequestedTheme</c>，不显式给就会跟着系统主题走（系统深色 + 应用浅色 = 弹窗是黑的）。
/// </remarks>
internal static class AppThemeExtensions
{
    public static ElementTheme ToElementTheme(this AppTheme theme) => theme switch
    {
        AppTheme.Dark => ElementTheme.Dark,
        AppTheme.Light => ElementTheme.Light,
        _ => ElementTheme.Default,
    };
}
