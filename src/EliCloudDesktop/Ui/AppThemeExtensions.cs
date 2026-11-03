using EliCloudDesktop.Core;

using Microsoft.UI.Xaml;

namespace EliCloudDesktop.Ui;

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
