using EliCloudDesktop.Core;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;
using static EliCloudDesktop.Ui.SettingsCardElement;
using static Microsoft.UI.Reactor.Factories;

using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Navigation;

namespace EliCloudDesktop.Ui;

/// <summary>
/// 界面里可复用的视觉单元。
/// </summary>
/// <remarks>
/// 这些都是**纯函数**（给定输入返回同一棵元素树，自己没有状态），所以不写成
/// <c>Component</c> 子类 —— 父组件渲染时重新构造，Reactor 的 diff 会按结构复用已实现的控件。
/// </remarks>
internal static class Widgets
{
    // ── Hero / 页头 ────────────────────────────────────────────────────

    /// <summary>
    /// 主页 Hero：整宽图片横幅 + 粉色渐变叠层 + 底部压暗 + 页标题。
    /// 对应设计稿的 Sticky Header（932×183，其中横幅 932×156）。
    /// </summary>
    public static Element HeroBanner(string title, string description, string bannerImageUri)
    {
        var layers = new List<Element>
        {
            // 底图：整宽、156 高，UniformToFill 裁切（纯装饰，不参与读屏）
            Image(bannerImageUri)
                .Height(DesignTokens.HeroBannerHeight)
                .VerticalAlignment(VerticalAlignment.Top)
                .Set(image => image.Stretch = Stretch.UniformToFill)
                .AccessibilityHidden()
                .Grid(row: 0, column: 0),

            // 稿里的 GRADIENT_LINEAR(#F9F9F9 → #FFA2E0@40%)
            Border(Empty())
                .Height(DesignTokens.HeroBannerHeight)
                .VerticalAlignment(VerticalAlignment.Top)
                .Background(PinkWash)
                .Grid(row: 0, column: 0),

            // 稿里横幅底部那层 FOREGROUND_BLUR(#000000@25%, 半径 4)：
            // WinUI 里没有等价的「只把图片某个局部模糊」的廉价做法，用同位置的压暗渐变近似。
            /*Border(Empty())
                .Height(DesignTokens.HeroBannerHeight)
                .VerticalAlignment(VerticalAlignment.Top)
                .Background(BottomScrim)
                .Grid(row: 0, column: 0),*/

            // 文字层
            Border(
                VStack(
                    8,
                    DesignTokens.PageTitle(title).Foreground(Theme.PrimaryText),
                    DesignTokens.Body(description).Foreground(Theme.SecondaryText).TextWrapping(),
                    DesignTokens.Divider(topMargin: 20))
            ).Padding(DesignTokens.ContentPadding, 36, DesignTokens.ContentPadding, 24)
             .Grid(row: 0, column: 0),
        };

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto],
            children: [.. layers]);
    }

    /// <summary>
    /// 普通页头（个人资料页 / 服务页 / 设置页）：可选头像 + 标题 + 副标题 + 可选操作区 + 分隔线。
    /// 对应设计稿个人资料页的 Sticky Header（932×163）。
    /// </summary>
    public static Element PageHeader(
        string title,
        string description,
        Element? avatar = null,
        Element? actions = null)
    {
        var heading = new List<Element>();
        if (avatar is not null) heading.Add(avatar);

        var titleRow = new List<Element>
        {
            DesignTokens.PageTitle(title).Foreground(Theme.PrimaryText),
        };
        if (actions is not null) titleRow.Add(actions);

        var textColumn = new List<Element>
        {
            FlexRow([.. titleRow]) with
            {
                ColumnGap = 48,
                AlignItems = FlexAlign.Center,
            },
            DesignTokens.Body(description).Foreground(Theme.SecondaryText).TextWrapping(),
        };

        heading.Add((FlexColumn([.. textColumn]) with
        {
            RowGap = 8,
        }).Flex(grow: 1));

        return Border(
            VStack(
                20,
                FlexRow([.. heading]) with
                {
                    ColumnGap = 10,
                    AlignItems = FlexAlign.Center,
                },
                DesignTokens.Divider())
        ).Padding(DesignTokens.ContentPadding, 36, DesignTokens.ContentPadding, 24);
    }

    /// <summary>主页的分组表头（稿：标题 14/20 Semibold + 说明 14/20 Secondary）。</summary>
    public static Element SectionHeader(string title, string description) =>
        Border(
            VStack(
                4,
                DesignTokens.SectionTitle(title).Foreground(Theme.PrimaryText),
                DesignTokens.Body(description).Foreground(Theme.SecondaryText).TextWrapping())
        ).Padding(0, 11, 0, 11);

    // ── SettingCard ───────────────────────────────────────────────────

    /// <summary>
    /// 一条设置/服务条目。
    /// </summary>
    /// <remarks>
    /// 直接用 Windows Community Toolkit 的 <c>SettingsCard</c>（经源生成器包成 Reactor 元素，
    /// 见 <see cref="SettingsCardElement"/>）：描边、底色、圆角、悬停/按下/焦点反馈、
    /// 键盘可达性、自动化对等，以及右侧那个箭头动作区，全部由控件的官方模板提供 ——
    /// 我们只负责「说什么」和「点了做什么」。
    /// </remarks>
    /// <param name="icon">左侧图标（可为空）。</param>
    /// <param name="title">标题。</param>
    /// <param name="caption">副标题。</param>
    /// <param name="trailing">右侧内容（状态文案、徽标……）；<c>null</c> 表示没有。</param>
    /// <param name="onClick">整卡点击行为；为空则卡片不可点，也不显示右侧箭头。</param>
    /// <param name="actionToolTip">右侧箭头的提示文字（可点击时才有意义）。</param>
    public static Element SettingCard(
        Element? icon,
        string title,
        string caption,
        Element? trailing = null,
        Action? onClick = null,
        string? actionToolTip = null)
    {
        var card = SettingsCard(
            header: title,
            description: caption,
            headerIcon: icon,
            content: trailing,
            isClickEnabled: onClick is not null,
            onClick: onClick);

        // ActionIconToolTip 是普通字符串属性，没有生成对应的 init-prop（生成器只挑了
        // 有 PropertyId 的依赖属性），所以走逃生舱写一次。
        return actionToolTip is { Length: > 0 } tip
            ? card.Set(card => card.ActionIconToolTip = tip)
            : card;
    }

    /// <summary>卡片上的服务图标（稿：16×16）。</summary>
    /// <remarks>
    /// 必须是 <c>IconElement</c> 家族的元素：<c>SettingsCard.HeaderIcon</c> 的类型就是它，
    /// 往里塞一个 <c>Image</c> 会在挂载时抛 InvalidCastException。
    /// </remarks>
    public static Element ServiceIcon(string iconUri) =>
        Icon(BitmapIcon(new Uri(iconUri), showAsMonochrome: false))
            .Width(DesignTokens.IconSize)
            .Height(DesignTokens.IconSize)
            .VerticalAlignment(VerticalAlignment.Center)
            .AccessibilityHidden();

    /// <summary>
    /// 状态徽标：按可用性取系统语义色的小圆点。
    /// 「可用 / 不可用」是当前唯一能真实判定出来的两种状态，其余取值先留出颜色与文案。
    /// </summary>
    public static Element StatusBadge(ServiceAvailability availability)
    {
        var color = availability switch
        {
            ServiceAvailability.Available => Theme.SystemSuccess,
            ServiceAvailability.Unavailable => Theme.SystemCritical,
            ServiceAvailability.Maintenance => Theme.SystemCaution,
            ServiceAvailability.ComingSoon => Theme.SystemAttention,
            _ => Theme.SystemNeutral,
        };

        var label = availability.Label();

        return Border(
            Border(Empty())
                .Width(8)
                .Height(8)
                .CornerRadius(4)
                .Background(color))
            .Width(DesignTokens.BadgeSize)
            .Height(DesignTokens.BadgeSize)
            .VerticalAlignment(VerticalAlignment.Center)
            .ToolTip(label)
            .AutomationName($"服务状态：{label}");
    }

    /// <summary>圆角头像（稿：64×64，圆角 999 ≈ 圆形）。</summary>
    public static Element Avatar(string imageUri, double size) =>
        Border(
            Image(imageUri)
                .Set(image => image.Stretch = Stretch.UniformToFill)
                .AccessibilityHidden())
            .Width(size)
            .Height(size)
            .CornerRadius(size / 2);

    /// <summary>圆角头像（自定义图片；null 时回退默认头像）。</summary>
    public static Element Avatar(ImageSource? source, string fallbackUri, double size) =>
        Border(
            Image(fallbackUri)
                .Set(image =>
                {
                    if (source is not null) image.Source = source;
                    image.Stretch = Stretch.UniformToFill;
                })
                .AccessibilityHidden())
            .Width(size)
            .Height(size)
            .CornerRadius(size / 2);

    /// <summary>
    /// 个人资料页头：整宽 banner（无图时粉色渐变）+ 头像 / 昵称 / 简介 / 操作区。
    /// </summary>
    public static Element ProfileHeader(
        string title,
        string description,
        Element avatar,
        Element? actions,
        ImageSource? bannerSource)
    {
        var bannerLayers = new List<Element>();
        if (bannerSource is not null)
        {
            bannerLayers.Add(
                Image("ms-appx:///Assets/Images/home-hero-banner-christmas-pink.jpg")
                    .Height(DesignTokens.HeroBannerHeight)
                    .Set(image =>
                    {
                        image.Source = bannerSource;
                        image.Stretch = Stretch.UniformToFill;
                    })
                    .AccessibilityHidden());
        }
        else
        {
            bannerLayers.Add(
                Border(Empty())
                    .Height(DesignTokens.HeroBannerHeight)
                    .Background(PinkWash));
        }

        var titleRow = new List<Element>
        {
            DesignTokens.PageTitle(title).Foreground(Theme.PrimaryText),
        };
        if (actions is not null) titleRow.Add(actions);

        var textColumn = new List<Element>
        {
            FlexRow([.. titleRow]) with
            {
                ColumnGap = 48,
                AlignItems = FlexAlign.Center,
            },
            DesignTokens.Body(description).Foreground(Theme.SecondaryText).TextWrapping(),
        };

        var headingRow = new List<Element> { avatar };
        headingRow.Add((FlexColumn([.. textColumn]) with
        {
            RowGap = 8,
        }).Flex(grow: 1));

        return VStack(
            0,
            Grid(
                columns: [GridSize.Star()],
                rows: [GridSize.Auto],
                children: [.. bannerLayers]),
            Border(
                VStack(
                    20,
                    FlexRow([.. headingRow]) with
                    {
                        ColumnGap = 10,
                        AlignItems = FlexAlign.Center,
                    },
                    DesignTokens.Divider()))
            .Padding(DesignTokens.ContentPadding, 24, DesignTokens.ContentPadding, 24));
    }

    /// <summary>一个图标字形（用于没有位图资源的条目）。</summary>
    public static Element GlyphIcon(string glyph, double size = DesignTokens.IconSize) =>
        Icon(FontIcon(glyph, fontSize: size)).Width(size).Height(size);

    // ── 渐变笔刷 ──────────────────────────────────────────────────────

    private static readonly LinearGradientBrush PinkWash = CreatePinkWash();

    private static readonly LinearGradientBrush BottomScrim = CreateBottomScrim();

    private static LinearGradientBrush CreatePinkWash()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5,1),
            EndPoint = new Point(0.5, 0),
        };
        brush.GradientStops.Add(new GradientStop
        {
            Offset = 0,
            Color = Color.FromArgb(0xFF, 0xF9, 0xF9, 0xF9),
        });
        brush.GradientStops.Add(new GradientStop
        {
            Offset = 1,
            Color = Color.FromArgb(0x66, 0xFF, 0xA2, 0xE0),
        });
        return brush;
    }

    private static LinearGradientBrush CreateBottomScrim()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 1),
            EndPoint = new Point(0.5, 0.6),
        };
        brush.GradientStops.Add(new GradientStop
        {
            Offset = 0,
            Color = Color.FromArgb(0x26, 0x00, 0x00, 0x00),
        });
        brush.GradientStops.Add(new GradientStop
        {
            Offset = 1,
            Color = Color.FromArgb(0x00, 0x00, 0x00, 0x00),
        });
        return brush;
    }
}
