using EliCloudDesktop.Core;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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
    /// 主页 Hero：banner 图铺满<b>整块页头</b> —— 标题就在它自己的背景上，而不是页顶单独的一条图。
    /// 对应设计稿的 Sticky Header（932×183，其中横幅 932×156）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 图上压一层渐变：标题与说明直接落在渐变上，不套卡片/亚克力。
    /// 渐变本身既是设计稿的 <c>GRADIENT_LINEAR(#F9F9F9@0 → #FFA2E0@40%@1)</c>，
    /// 也是给页头文字垫的「区分背景」。
    /// </para>
    /// <para>
    /// 渐变要跟着主题换底：浅色主题是设计稿的白→粉；深色主题换成 <see cref="DarkWash"/> 的
    /// 深灰→暗紫粉→粉，页头下沿和深色页面接上，上沿仍留着粉色的辨识度。
    /// </para>
    /// </remarks>
    public static Element HeroBanner(string title, string description, string bannerImageUri, bool isDarkTheme)
    {
        var heading = Border(
            VStack(
                8,
                DesignTokens.PageTitle(title).Foreground(Theme.PrimaryText),
                DesignTokens.Body(description).Foreground(Theme.SecondaryText).TextWrapping(),
                DesignTokens.Divider(topMargin: 20)))
            .Padding(DesignTokens.ContentPadding, 36, DesignTokens.ContentPadding, 24)
            .Background(isDarkTheme ? DarkWash : PinkWash);

        // 深色主题先在底图上压一层暗化层，再叠设计给的渐变。
        // 那张 banner 是浅色的，而渐变的粉色只有 40% 不透明 —— 直接压上去会变回浅粉，
        // 白色标题/说明就没对比度了。压暗底图后，渐变露出的才是它本来那档暗粉。
        var surface = isDarkTheme
            ? Border(heading).Background(HeroScrim)
            : heading;

        return Border(surface)
            .Background(BannerBrush(bannerImageUri))
            .MinHeight(DesignTokens.HeroBannerHeight);
    }

    /// <summary>
    /// 普通页头（个人资料页 / 服务页 / 设置页）：可选头像 + 标题 + 副标题 + 可选操作区 + 分隔线。
    /// 对应设计稿个人资料页的 Sticky Header（932×163）。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="description">副标题。</param>
    /// <param name="avatar">可选头像。</param>
    /// <param name="actions">可选操作区。</param>
    /// <param name="actionsAtRight">
    /// 把操作区顶到页头右边缘。默认 <c>false</c>，操作区紧跟在标题后面（个人资料页的稿子就是这样）；
    /// 仅对没有 banner 承托面的普通页头生效。
    /// </param>
    public static Element PageHeader(
        string title,
        string description,
        Element? avatar = null,
        Element? actions = null,
        bool actionsAtRight = false) =>
        Border(
            VStack(
                20,
                HeaderHeading(title, description, avatar, actions, actionsAtRight: actionsAtRight),
                DesignTokens.Divider()))
        .Padding(DesignTokens.ContentPadding, 36, DesignTokens.ContentPadding, 24);

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
                    // 必须显式回落到默认图：头像被「移除」时 source 会变回 null，
                    // 只在非空时赋值的话，Image 会一直留着上一张图。
                    var target = source ?? CachedImage(fallbackUri);
                    if (!ReferenceEquals(image.Source, target)) image.Source = target;
                    image.Stretch = Stretch.UniformToFill;
                })
                .AccessibilityHidden())
            .Width(size)
            .Height(size)
            .CornerRadius(size / 2);

    /// <summary>
    /// 个人资料页头：有头图时 banner 图铺满整块页头，头像 / 昵称 / 简介 / 操作区压在图里；
    /// 没有头图时退回普通页头（<see cref="PageHeader"/>）。
    /// </summary>
    /// <remarks>
    /// 没图时不塞渐变、也不塞亚克力：那样「移除头图」和「没设置过头图」看起来都像还有一张图，
    /// 用户分不清自己到底改成功没有。粉色渐变是主页 Hero 的品牌底，不在这里复用。
    /// <para>
    /// <paramref name="headerWidth"/> 是页头量到的实际宽度（页面在 <c>OnSizeChanged</c> 里喂进来）。
    /// 承托面按内容收缩，文字列因此需要一个显式的「可用宽度」上限，否则长简介会顶着卡片冲出页头右侧。
    /// </para>
    /// </remarks>
    public static Element ProfileHeader(
        string title,
        string description,
        Element avatar,
        Element? actions,
        ImageSource? bannerSource,
        double headerWidth,
        double actionsWidth = 0)
    {
        if (bannerSource is null)
        {
            return PageHeader(title, description, avatar, actions);
        }

        var textMaxWidth = Math.Max(
            DesignTokens.AvatarSize * 2,
            headerWidth
                - (2 * DesignTokens.ContentPadding)
                - (2 * HeaderPlatePadding)
                - DesignTokens.AvatarSize
                - HeaderAvatarGap);

        var heading = VStack(
            20,
            HeaderHeading(title, description, avatar, actions, textMaxWidth, actionsWidth),
            DesignTokens.Divider());

        return BannerHeader(BannerBrush(bannerSource), HeaderPlate(heading));
    }

    /// <summary>头像与文字块的间距（4px 网格上的 16；原先的 10 让两者贴在一起）。</summary>
    private const double HeaderAvatarGap = 16;

    /// <summary>标题与右侧操作按钮的间距。</summary>
    private const double HeaderActionGap = 48;

    /// <summary>承托面的内边距（4px 网格上的 20）。</summary>
    private const double HeaderPlatePadding = 20;

    /// <summary>
    /// 页头主体：头像保持 64×64 不被挤压，文字宽度随内容收缩、最长到可用宽度。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="textMaxWidth"/> 非空时（压着 banner、承托面按内容收缩）两列都给 <c>Auto</c>：
    /// Star 列在有限宽度下总会吃掉全部剩余宽度，会把承托面重新撑成整条页头；两列 Auto 时
    /// 文字列量到的就是文字自身的宽度，长简介则被 <c>MaxWidth</c> 卡在页头 padding 边处换行。
    /// </para>
    /// <para>
    /// 为空时（普通页头，没有承托面）保持原来的「Auto + Star」：标题吃满剩余宽度、描述正常换行。
    /// </para>
    /// <para>
    /// 头像列始终是 Auto，宽度恒为 64，不会被文字挤窄（原先用 FlexRow 默认 <c>shrink: 1</c> 时
    /// 昵称越长头像越窄，实测 64 被压到 57，这就是「头像被挤掉」的来源）。
    /// </para>
    /// </remarks>
    private static Element HeaderHeading(
        string title,
        string description,
        Element? avatar,
        Element? actions,
        double? textMaxWidth = null,
        double actionsWidth = 0,
        bool actionsAtRight = false)
    {
        var titleMaxWidth = textMaxWidth is { } availableTextWidth && actions is not null
            ? Math.Max(120, availableTextWidth - HeaderActionGap - actionsWidth)
            : (double?)null;

        Element textColumn = VStack(
            8,
            HeaderTitleRow(title, actions, titleMaxWidth, actionsAtRight),
            DesignTokens.Body(description).Foreground(Theme.SecondaryText).TextWrapping());

        if (textMaxWidth is { } maxWidth)
        {
            textColumn = textColumn.MaxWidth(maxWidth);
        }

        if (avatar is null) return textColumn;

        GridSize[] columns = textMaxWidth is null
            ? [GridSize.Auto, GridSize.Star()]
            : [GridSize.Auto, GridSize.Auto];

        return Grid(
            columns: columns,
            rows: [GridSize.Auto],
            avatar
                .VerticalAlignment(VerticalAlignment.Center)
                .Grid(row: 0, column: 0),
            textColumn.Grid(row: 0, column: 1))
            .Set(grid => grid.ColumnSpacing = HeaderAvatarGap);
    }

    /// <summary>标题行：昵称过长时省略号收尾，操作按钮不参与收缩。</summary>
    /// <remarks>
    /// <para>
    /// <paramref name="actions"/> 建议给普通按钮 / <c>HStack</c>：Reactor 里把一个 <c>FlexRow</c>
    /// 直接当动作区塞进这个标题行，嵌套 flex 会把标题量成 0 宽（MC 服务页的标题曾因此整段消失）。
    /// </para>
    /// <para>
    /// <paramref name="titleMaxWidth"/> 非空时（承托面按内容收缩）用两列 Auto 的 Grid：
    /// FlexRow 在有限宽度下会把整行量成「吃满」，承托面就又被撑宽了。标题给一个显式的宽度上限，
    /// 超长时正常出省略号；按钮列保持自然宽度 <c>shrink: 0</c> 的等价行为。
    /// </para>
    /// <para>
    /// <paramref name="actionsAtRight"/> 只作用于没有 <paramref name="titleMaxWidth"/> 的普通页头；
    /// 承托面里的两列 Auto 由内容宽度决定，不参与右对齐。
    /// </para>
    /// </remarks>
    private static Element HeaderTitleRow(
        string title,
        Element? actions,
        double? titleMaxWidth = null,
        bool actionsAtRight = false)
    {
        var titleText = DesignTokens.PageTitle(title)
            .Foreground(Theme.PrimaryText)
            .TextTrimming(TextTrimming.CharacterEllipsis);

        // 只有可能被省略的长标题才需要悬停看全名，短标题上挂 tooltip 只是噪音。
        if (title.Length > 12) titleText = titleText.ToolTip(title);

        if (actions is null) return titleText;

        if (titleMaxWidth is { } maxTitleWidth)
        {
            return Grid(
                columns: [GridSize.Auto, GridSize.Auto],
                rows: [GridSize.Auto],
                titleText
                    .MaxWidth(Math.Max(120, maxTitleWidth))
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Grid(row: 0, column: 0),
                actions
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Grid(row: 0, column: 1))
                .Set(grid => grid.ColumnSpacing = HeaderActionGap);
        }

        return FlexRow(
                titleText.Flex(shrink: 1),
                actions.Flex(shrink: 0))
            with
            {
                ColumnGap = HeaderActionGap,
                AlignItems = FlexAlign.Center,
                JustifyContent = actionsAtRight ? FlexJustify.SpaceBetween : FlexJustify.Auto,
            };
    }

    /// <summary>一个图标字形（用于没有位图资源的条目）。</summary>
    public static Element GlyphIcon(string glyph, double size = DesignTokens.IconSize) =>
        Icon(FontIcon(glyph, fontSize: size)).Width(size).Height(size);

    // ── 页头底图 / 承托面 ──────────────────────────────────────────────

    /// <summary>
    /// 页头外框：底图铺满<b>整块页头</b>，页头高度随窗口宽度按 <see cref="DesignTokens.BannerAspectRatio"/> 等比缩放。
    /// </summary>
    /// <remarks>
    /// 高度不能只由内容决定：内容高度基本是固定的（约 185），窗口一拉宽，底图就会被
    /// <c>UniformToFill</c> 横向裁掉，裁切弹窗里看到的取景和页头对不上 —— 那样裁切就白做了。
    /// <para>
    /// 这里塞一个 5:1 的隐形占位（<see cref="Viewbox"/> 在宽度受限、高度不限时量出的高度 = 宽 / 比例），
    /// 让「高度 ≥ 宽 / 比例」成为布局约束；内容更高时（窄窗口）仍以内容为准，不会把标题压扁。
    /// </para>
    /// </remarks>
    private static Element BannerHeader(Brush background, Element content)
    {
        // Viewbox 的 Stretch=Uniform 把子元素的固定尺寸换算成宽度约束下的等比高度；
        // 子元素没有背景、不可命中，只负责把这一行撑到 5:1。
        var aspectKeeper = Viewbox(
                Border(Empty())
                    .Width(BannerAspectBasisWidth)
                    .Height(BannerAspectBasisWidth / DesignTokens.BannerAspectRatio))
            .Stretch(Stretch.Uniform);

        return Border(
                Grid(
                    columns: [GridSize.Star()],
                    rows: [GridSize.Auto],
                    aspectKeeper.Grid(row: 0, column: 0),
                    Border(content)
                        .Padding(DesignTokens.ContentPadding, 36, DesignTokens.ContentPadding, 24)
                        .Grid(row: 0, column: 0)))
            .Background(background)
            .MinHeight(DesignTokens.HeroBannerHeight);
    }

    /// <summary>5:1 占位用的基准宽度（DIP）；只决定比例，不参与实际显示。</summary>
    private const double BannerAspectBasisWidth = 1000;

    /// <summary>
    /// 压在 banner 上的承托面：亚克力（把底图模糊掉）+ 细描边 + 圆角。
    /// banner 是用户可换的图片，明暗不可控；标题用 PrimaryText、说明用 SecondaryText，
    /// 没有这一层就会和图片混在一起 —— 这就是「按需要给 UI 元素加区分背景」的落点。
    /// </summary>
    /// <remarks>
    /// 宽度随内容收缩（<c>HAlign(Left)</c>），上限是页头外框的 padding 边：短昵称/短简介时承托面
    /// 就是文字那么宽，长简介换行到 padding 边为止。不写死像素上限，也不强制吃满整条页头。
    /// <para>
    /// 垂直方向必须 <c>VAlign(Top)</c>：页头高度现在由 5:1 比例撑开（见 <see cref="BannerHeader"/>），
    /// 默认的 Stretch 会把承托面拉成和底图一样高，卡片里留出一大片空的亚克力。
    /// </para>
    /// <para>
    /// 不能用 <c>Card</c> 的 <c>CardBackgroundFillColorDefaultBrush</c>：它在深色主题下只有 5% 白，
    /// 压到一张浅色 banner 上等于没有，浅色文字会直接糊在图上。亚克力的着色跟随主题，
    /// 浅色主题出浅磨砂、深色主题出深磨砂，两种主题下文字都保得住。
    /// </para>
    /// </remarks>
    private static Element HeaderPlate(Element content) =>
        Border(content)
            .Padding(HeaderPlatePadding)
            .HAlign(HorizontalAlignment.Left)
            .VAlign(VerticalAlignment.Top)
            .Background(Theme.Ref("AcrylicBackgroundFillColorDefaultBrush"))
            .WithBorder(Theme.Ref("SurfaceStrokeColorDefaultBrush"), 1)
            .CornerRadius(8)
            .Set(border => border.BackgroundSizing = BackgroundSizing.InnerBorderEdge);

    /// <summary>把一张 ms-appx 图片铺成页头底图（UniformToFill 裁切）。</summary>
    private static Brush BannerBrush(string imageUri)
    {
        if (!BannerBrushCache.TryGetValue(imageUri, out var brush))
        {
            brush = BannerBrush(new BitmapImage(new Uri(imageUri)));
            BannerBrushCache[imageUri] = brush;
        }

        return brush;
    }

    /// <summary>默认头像之类的回退图：同一 URI 复用同一个 <see cref="BitmapImage"/>，避免反复解码。</summary>
    private static ImageSource CachedImage(string imageUri)
    {
        if (!ImageCache.TryGetValue(imageUri, out var image))
        {
            image = new BitmapImage(new Uri(imageUri));
            ImageCache[imageUri] = image;
        }

        return image;
    }

    /// <summary>把用户资料里的头图铺成页头底图（UniformToFill 裁切）。</summary>
    private static Brush BannerBrush(ImageSource source) =>
        new ImageBrush
        {
            ImageSource = source,
            Stretch = Stretch.UniformToFill,
        };

    // Reactor 按引用 diff Background：每次渲染都 new 一个画刷会让底图被反复重设，按 URI 复用。
    private static readonly Dictionary<string, Brush> BannerBrushCache = new(StringComparer.Ordinal);

    // 回退图同理：每次渲染都 new 一个 BitmapImage 会让 Image 重新加载。
    private static readonly Dictionary<string, BitmapImage> ImageCache = new(StringComparer.Ordinal);

    // ── 渐变笔刷 ──────────────────────────────────────────────────────

    /// <summary>深色主题下压在 banner 底图上的暗化层（50% 黑），见 <see cref="HeroBanner"/>。</summary>
    private static readonly SolidColorBrush HeroScrim = new(Color.FromArgb(0x80, 0x00, 0x00, 0x00));

    /// <summary>
    /// 浅色主题下主页 Hero 那层粉色渐变：<c>GRADIENT_LINEAR(#F9F9F9@0 → #FFA2E0@40%@1)</c>。
    /// 从下沿的实心浅色过渡到上沿的半透明粉，把底图往「能压深色标题」的方向洗淡。
    /// </summary>
    private static readonly LinearGradientBrush PinkWash = CreatePinkWash();

    /// <summary>
    /// 深色主题下的 Hero 渐变，按设计给的色标：<c>#262626@100% → #4B3443@75% → #FFA2E0@40%</c>。
    /// </summary>
    /// <remarks>
    /// 下沿从深灰 <c>#262626</c> 起步、和深色页面接上；41% 处过渡到暗紫粉 <c>#4B3443</c>；
    /// 上沿是 40% 的粉，底图能在那里透出来。
    /// </remarks>
    private static readonly LinearGradientBrush DarkWash = CreateDarkWash();

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

    private static LinearGradientBrush CreateDarkWash()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 1),
            EndPoint = new Point(0.5, 0),
        };
        brush.GradientStops.Add(new GradientStop
        {
            Offset = 0,
            Color = Color.FromArgb(0xFF, 0x26, 0x26, 0x26),
        });
        brush.GradientStops.Add(new GradientStop
        {
            Offset = 0.41,
            Color = Color.FromArgb(0xBF, 0x4B, 0x34, 0x43),
        });
        brush.GradientStops.Add(new GradientStop
        {
            Offset = 1,
            Color = Color.FromArgb(0x66, 0xFF, 0xA2, 0xE0),
        });
        return brush;
    }
}
