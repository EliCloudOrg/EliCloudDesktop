
namespace EliCloudDesktop.Core;

/// <summary>客户端的配色主题。</summary>
/// <remarks>
/// 设计稿（<c>EliCloudDesktopUI.fig</c>）里三个页面用的都是设计组件库的 <c>Mode=Light</c> 变体，
/// 所以默认值取 <see cref="Light"/> —— 「按设计稿还原」优先于「跟随系统」。
/// </remarks>
internal enum AppTheme
{
    /// <summary>跟随系统。</summary>
    System = 0,

    /// <summary>浅色（默认，与设计稿一致）。</summary>
    Light,

    /// <summary>深色。</summary>
    Dark,
}
