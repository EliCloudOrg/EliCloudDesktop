using Microsoft.UI.Reactor;
using static Microsoft.UI.Reactor.Factories;

using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Navigation;

namespace EliCloudDesktop.Ui;

/// <summary>
/// Figma 设计稿里的版面与排版标尺，逐项对应 <c>EliCloudDesktopUI.fig</c> 的实测值。
/// </summary>
/// <remarks>
/// 稿子用的是 **Windows 11 官方设计组件库**，所以这里的字号/行高正好落在
/// WinUI 的文本样式阶梯上（Display 68/92、Title 28/36、BodyStrong 14/20、Body 14/20、Caption 12/16），
/// 颜色也一律走 <c>Theme.*</c> 主题令牌 —— 亮/暗/高对比度三套主题因此自动成立。
/// </remarks>
internal static class DesignTokens
{
    // ── 版面尺寸（单位 DIP，全部来自设计稿） ───────────────────────────────
    /// <summary>设计稿画布宽。</summary>
    public const double WindowWidth = 1252;

    /// <summary>设计稿画布高。</summary>
    public const double WindowHeight = 800;

    /// <summary>自定义标题栏高度（稿：1252×48）。</summary>
    public const double TitleBarHeight = 48;

    /// <summary>Nav 展开宽度（稿：Nav Pane 320，与 WinUI 默认一致）。</summary>
    public const double NavPaneWidth = 320;

    /// <summary>内容区左右留白（稿：padH=56）。</summary>
    public const double ContentPadding = 56;

    /// <summary>主页 Hero 横幅高度（稿：932×156）。</summary>
    public const double HeroBannerHeight = 156;

    /// <summary>服务卡片最小高度（稿：820×64）。</summary>
    public const double ServiceCardMinHeight = 64;

    /// <summary>服务卡片内边距（稿：padH=15 / padV=13）。</summary>
    public const double ServiceCardPaddingH = 15;

    /// <summary>服务卡片内边距。</summary>
    public const double ServiceCardPaddingV = 13;

    /// <summary>卡片圆角（WinUI 控件圆角令牌 <c>ControlCornerRadius</c>）。</summary>
    public const double CardCornerRadius = 4;

    /// <summary>服务图标边长（稿：16×16）。</summary>
    public const double IconSize = 16;

    /// <summary>状态徽标边长（稿：Badge 16×16）。</summary>
    public const double BadgeSize = 16;

    /// <summary>头像边长（稿：64×64）。</summary>
    public const double AvatarSize = 64;

    /// <summary>Nav 页脚资料条目的高度（稿：320×79）。</summary>
    public const double NavProfileItemHeight = 79;

    // ── 排版 ──────────────────────────────────────────────────────────
    private const string DisplayFamily = "Segoe UI Variable Display";
    private const string TextFamily = "Segoe UI Variable Text";

    /// <summary>登录页产品名（稿：Segoe UI Variable Semibold Display 68/92）。</summary>
    public static TextBlockElement Display(string text) =>
        TextBlock(text).FontFamily(DisplayFamily).FontSize(68).LineHeight(92).SemiBold();

    /// <summary>页面主标题（稿：28/36 Semibold Display）。</summary>
    public static TextBlockElement PageTitle(string text) =>
        TextBlock(text).FontFamily(DisplayFamily).FontSize(28).LineHeight(36).SemiBold();

    /// <summary>分组标题（稿：14/20 Semibold Text）。</summary>
    public static TextBlockElement SectionTitle(string text) =>
        TextBlock(text).FontFamily(TextFamily).FontSize(14).LineHeight(20).SemiBold();

    /// <summary>卡片标题（稿：14/20 Regular Text）。</summary>
    public static TextBlockElement CardTitle(string text) =>
        TextBlock(text).FontFamily(TextFamily).FontSize(14).LineHeight(20);

    /// <summary>正文（稿：14/20 Regular Text）。</summary>
    public static TextBlockElement Body(string text) =>
        TextBlock(text).FontFamily(TextFamily).FontSize(14).LineHeight(20);

    /// <summary>卡片副标题（稿：12/16 Regular Small）。</summary>
    public static TextBlockElement CardCaption(string text) =>
        TextBlock(text).FontFamily(TextFamily).FontSize(12).LineHeight(16);

    /// <summary>
    /// 输入框下面那行提示 / 约束说明（12/16 Regular Small，Secondary）。
    /// </summary>
    /// <remarks>
    /// 自己画，而不是用 WinUI <c>TextBox.Description</c>：后者在 Reactor 里经
    /// <c>.Description(...)</c> 设置后在实机上没有渲染（UI 自动化树里既没有这行文字，
    /// 输入框的 HelpText 也是空的）。自己画还顺带保证了读屏能读到它。
    /// </remarks>
    public static Element Hint(string text) =>
        CardCaption(text).Foreground(Theme.SecondaryText).TextWrapping();

    /// <summary>把一个输入框和它的提示行绑在一起（4px 间距），避免提示看起来像属于下一个字段。</summary>
    public static Element Field(Element input, string? hint) =>
        hint is { Length: > 0 }
            ? VStack(4, input, Hint(hint))
            : VStack(4, input);

    /// <summary>1px 分隔线（稿：Line + DividerStrokeColorDefault）。</summary>
    public static Element Divider(double topMargin = 0) =>
        Border(Empty())
            .Height(1)
            .Background(Theme.DividerStroke)
            .Margin(0, topMargin, 0, 0);
}
