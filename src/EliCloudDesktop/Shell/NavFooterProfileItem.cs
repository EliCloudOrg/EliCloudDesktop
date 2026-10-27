using EliCloudDesktop.Core;
using Microsoft.UI.Reactor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Navigation;
using static Microsoft.UI.Reactor.Factories;

// Grid 这个名字被 Reactor 的工厂方法占用了，这里要的是原生面板。
using XamlGrid = Microsoft.UI.Xaml.Controls.Grid;

namespace EliCloudDesktop.Shell;

/// <summary>
/// Nav 页脚的「个人资料」入口。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么必须走逃生舱（<c>.Set(...)</c>）而不是声明式 API</b>：
/// 设计稿里这个条目是「64×64 圆角头像 + 两行文字（用户名 / 邮箱）+ 选中指示条」，
/// 而 Reactor 声明的 <c>NavigationViewItemData</c> 只能表达
/// <c>Content</c>（字符串）、<c>Icon</c>、<c>Tag</c>、<c>Children</c> —— 没有自定义内容模板的位置。
/// 想把它塞进 <c>FooterMenuItems</c>（需求 #6），只能拿到底层
/// <see cref="NavigationView"/> 之后自己造一个 <see cref="NavigationViewItem"/>。
/// </para>
/// <para>
/// <b>为什么不会每次渲染重复添加</b>：条目按 <see cref="Tag"/> 去重，
/// 内容只在「签名」（用户名/邮箱/是否选中）变化时才重建，
/// 签名存在调用方的 <c>UseRef</c> 里，跨渲染稳定。
/// </para>
/// </remarks>
internal static class NavFooterProfileItem
{
    /// <summary>
    /// 条目对读屏软件 / UI 自动化暴露的名字。
    /// </summary>
    /// <remarks>
    /// 内容是一棵控件树（头像 + 两行文字），WinUI 从中推不出一个有意义的名字，
    /// 所以必须显式给；顺带也让 UI 自动化测试能按名字找到这个条目。
    /// </remarks>
    public const string AccessibleName = "个人资料";

    /// <summary>往页脚补一个个人资料条目；已存在则按需要刷新内容。</summary>
    /// <param name="view">底层 NavigationView（逃生舱拿到的实例）。</param>
    /// <param name="signature">内容签名，变化才重建内容。</param>
    /// <param name="lastSignature">上一次的签名（由调用方用 UseRef 持有）。</param>
    /// <param name="session">当前会话。</param>
    /// <param name="displayName">展示名（优先 userinfo 昵称）。</param>
    /// <param name="avatarSource">自定义头像；null 时用默认头像。</param>
    /// <param name="avatarUri">默认头像图片地址。</param>
    public static void Apply(
        NavigationView view,
        string signature,
        Ref<string?> lastSignature,
        SessionState session,
        string displayName,
        ImageSource? avatarSource,
        string avatarUri)
    {
        var item = view.FooterMenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(candidate => Equals(candidate.Tag, RouteTags.Profile));

        if (item is null)
        {
            item = new NavigationViewItem
            {
                Tag = RouteTags.Profile,
                // 条目本身不显示文字，全部内容交给 Content。
                Content = BuildContent(session, displayName, avatarSource, avatarUri),
            };

            // 自定义 Content 是个控件树，读屏软件从这里拿不到名字 —— 必须显式给。
            AutomationProperties.SetName(item, AccessibleName);
            view.FooterMenuItems.Add(item);
            lastSignature.Current = signature;
            return;
        }

        if (lastSignature.Current == signature) return;

        item.Content = BuildContent(session, displayName, avatarSource, avatarUri);
        lastSignature.Current = signature;
    }

    private static UIElement BuildContent(
        SessionState session,
        string displayName,
        ImageSource? avatarSource,
        string avatarUri)
    {
        var avatar = new Image
        {
            Width = 64,
            Height = 64,
            Stretch = Stretch.UniformToFill,
            Source = avatarSource ?? new BitmapImage(new Uri(avatarUri)),
        };

        var avatarHost = new Border
        {
            Width = 64,
            Height = 64,
            CornerRadius = new CornerRadius(32),
            Child = avatar,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var text = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        text.Children.Add(new TextBlock
        {
            Text = displayName,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,

            // TextTrimming 只在「量到的宽度比文字窄」时才出省略号。放进横向 StackPanel
            // （主轴上不限宽）时 TextBlock 会按整段文字量宽，永远不触发 —— 截断就只剩硬裁。
            // 所以下面把根节点换成 Auto + Star 的 Grid，让文字列拿到确定的剩余宽度。
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        });
        text.Children.Add(new TextBlock
        {
            Text = session.DisplayEmail,
            FontSize = 12,
            Opacity = 0.61,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        });

        // 头像 Auto 列、文字 Star 列：文字据此拿到页脚条目的剩余宽度，省略号才会生效。
        // 稿子里这一格是 320×79，头像是 64×64 —— 上下各留 8（合计 16）正好凑出 79。
        var root = new XamlGrid
        {
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0, 8, 0, 8),
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        XamlGrid.SetColumn(avatarHost, 0);
        XamlGrid.SetColumn(text, 1);
        root.Children.Add(avatarHost);
        root.Children.Add(text);
        return root;
    }
}
