using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Navigation;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

using G = EliCloud.Generated.DesignTokens;

namespace EliCloudDesktop.Ui;

/// <summary>
/// Figma 设计稿里的版面与排版标尺，逐项对应 <c>EliCloudDesktopUI.fig</c> 的实测值。
/// </summary>
/// <remarks>
/// <para>
/// 稿子用的是 **Windows 11 官方设计组件库**，所以文字一律落在 WinUI 官方文本样式阶梯上
/// （<c>TitleTextBlockStyle</c> / <c>BodyStrongTextBlockStyle</c> / <c>BodyTextBlockStyle</c> /
/// <c>CaptionTextBlockStyle</c>）；只有 Display 这一档官方没有对应样式，用 `design/tokens.json`
/// 里登记过的 custom 令牌（<see cref="G.TypeDisplaySize"/> 等）。颜色一律走 <c>Theme.*</c>，
/// 亮/暗/高对比度三套主题因此自动成立。
/// </para>
/// <para>
/// 这里不再写裸字号/裸圆角：数值来自生成物 <c>Generated/DesignTokens.g.cs</c>（不要手改，
/// 改 <c>design/tokens.json</c> 再跑生成器），有明确的官方来源。
/// </para>
/// </remarks>
internal static class DesignTokens
{
    // ── 版面尺寸（单位 DIP，全部来自设计稿） ───────────────────────────────
    /// <summary>设计稿画布宽。</summary>
    public const double WindowWidth = 1252;

    /// <summary>设计稿画布高。</summary>
    public const double WindowHeight = 800;

    /// <summary>最小窗口宽（design/windows.md §1：再小就退化为内容滚动）。</summary>
    public const double WindowMinWidth = 1000;

    /// <summary>最小窗口高。</summary>
    public const double WindowMinHeight = 640;

    /// <summary>自定义标题栏高度（稿：1252×48）。</summary>
    public const double TitleBarHeight = 48;

    /// <summary>Nav 展开宽度（稿：Nav Pane 320，与 WinUI 默认一致）。</summary>
    public const double NavPaneWidth = 320;

    /// <summary>内容区左右留白（稿：padH=56）。</summary>
    public const double ContentPadding = 56;

    /// <summary>主页 Hero 横幅高度（稿：932×156）。</summary>
    public const double HeroBannerHeight = 156;

    /// <summary>个人资料页头图比例：设计稿横幅 932×186 ≈ 5:1，裁切与显示共用契约值。</summary>
    public const double BannerAspectRatio = Core.Contract.BannerAspectRatio;

    /// <summary>服务卡片最小高度（稿：820×64）。</summary>
    public const double ServiceCardMinHeight = 64;

    /// <summary>服务卡片内边距（稿：padH=15 / padV=13）。</summary>
    public const double ServiceCardPaddingH = 15;

    /// <summary>服务卡片内边距。</summary>
    public const double ServiceCardPaddingV = 13;

    /// <summary>服务图标边长（稿：16×16）。</summary>
    public const double IconSize = 16;

    /// <summary>状态徽标边长（稿：Badge 16×16）。</summary>
    public const double BadgeSize = 16;

    /// <summary>头像边长（稿：64×64）。</summary>
    public const double AvatarSize = 64;

    /// <summary>Nav 页脚资料条目的高度（稿：320×79）。</summary>
    public const double NavProfileItemHeight = 79;

    // ── 排版（官方文本样式；Display 是登记过的 custom 令牌） ──────────────
    /// <summary>登录页产品名（稿：Segoe UI Variable Semibold Display 68/92）。</summary>
    public static TextBlockElement Display(string text) =>
        TextBlock(text)
            .FontFamily(G.TypeDisplayFamilyKey)
            .FontSize(G.TypeDisplaySize)
            .LineHeight(G.TypeDisplayLineHeight)
            .SemiBold();

    /// <summary>页面主标题（官方 <c>TitleTextBlockStyle</c>：28/36 Semibold Display）。</summary>
    public static TextBlockElement PageTitle(string text) => Styled(text, G.TypeTitleKey);

    /// <summary>分组标题（官方 <c>BodyStrongTextBlockStyle</c>：14/20 Semibold Text）。</summary>
    public static TextBlockElement SectionTitle(string text) => Styled(text, BodyStrongTextBlockStyle);

    /// <summary>卡片标题（官方 <c>BodyTextBlockStyle</c>：14/20 Regular Text）。</summary>
    public static TextBlockElement CardTitle(string text) => Styled(text, G.TypeBodyKey);

    /// <summary>正文（官方 <c>BodyTextBlockStyle</c>：14/20 Regular Text）。</summary>
    public static TextBlockElement Body(string text) => Styled(text, G.TypeBodyKey);

    /// <summary>卡片副标题（官方 <c>CaptionTextBlockStyle</c>：12/16 Regular Small）。</summary>
    public static TextBlockElement CardCaption(string text) => Styled(text, G.TypeCaptionKey);

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

    /// <summary>WinUI 官方 14/20 Semibold 文本样式（令牌表里没有单独语义键，直接用官方资源键）。</summary>
    private const string BodyStrongTextBlockStyle = "BodyStrongTextBlockStyle";

    /// <summary>套用官方文本样式（字体族/字号/行高/字重都来自 WinUI 官方 Style，不手写数值）。</summary>
    private static TextBlockElement Styled(string text, string styleKey) =>
        TextBlock(text).Set(block => OfficialStyles.Apply(block, styleKey));
}
