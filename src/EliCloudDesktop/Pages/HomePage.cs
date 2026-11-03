using EliCloudDesktop.Core;
using EliCloudDesktop.Generated;
using EliCloudDesktop.Ui;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

using Microsoft.UI.Reactor.Navigation;

namespace EliCloudDesktop.Pages;

/// <summary>
/// 主页 —— 对应设计稿第二个 App window。
/// </summary>
/// <remarks>
/// <b>内容来自设计稿</b>：Hero 文案、三组表头的标题与说明、每张服务卡片的标题/副标题/图标，
/// 全部来自设计稿（见 docs/design-spec.md），不依赖任何接口；文案 key 来自
/// <c>contracts/copy/zh-CN.json</c>。
/// 唯一「活」的东西是<b>卡片上的状态徽标</b>，以及由此决定的分组归属 ——
/// 某个状态当下一个服务都没有时，整组表头连同卡片一起不渲染。
/// </remarks>
internal sealed class HomePage : Component
{
    private const string HeroBannerUri = "ms-appx:///Assets/Images/home-hero-banner-christmas-pink.jpg";

    /// <summary>一组表头：标题/说明的 copy key，<see cref="States"/> 决定哪些服务落进这一组。</summary>
    private sealed record SectionSpec(string TitleKey, string DescriptionKey, ServiceAvailability[] States);

    private static readonly SectionSpec[] Sections =
    [
        new(CopyKeys.HomeSectionNewTitle, CopyKeys.HomeSectionNewDescription,
            [ServiceAvailability.ComingSoon]),

        new(CopyKeys.HomeSectionWorkingTitle, CopyKeys.HomeSectionWorkingDescription,
            [ServiceAvailability.Available]),

        new(CopyKeys.HomeSectionStoppedTitle, CopyKeys.HomeSectionStoppedDescription,
            [ServiceAvailability.Maintenance, ServiceAvailability.Unavailable]),
    ];

    public override Element Render()
    {
        var state = UseContext(EliCloudContexts.State);
        var nav = UseNavigation<Route>();

        // Hero 的渐变要跟着实际生效的主题换底（浅色白、深色黑）。
        // UseIsDarkTheme() 只反映应用级配色、不看元素上的 RequestedTheme，而 App 是按 state.Theme
        // 给整棵树设元素级主题的 —— 所以显式主题直接用 state，只有「跟随系统」才回落到这个应用级值。
        var appIsDark = UseIsDarkTheme();
        var isDarkTheme = state.Theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => appIsDark,
        };

        var body = new List<Element>();

        // 还没拿到任何结论时，先说明正在检查，而不是把服务错误地归到「停止服务」。
        if (state.Services.Items.Count == 0)
        {
            body.Add(FlexRow(
                ProgressRing().Width(20).Height(20),
                DesignTokens.Body(Copy.Get(CopyKeys.HomeLoading)).Foreground(Theme.SecondaryText)) with
            {
                ColumnGap = 12,
                AlignItems = FlexAlign.Center,
            });
        }
        else
        {
            foreach (var section in Sections)
            {
                var services = ServiceCatalog.All
                    .Where(d => section.States.Contains(AvailabilityOf(state, d)))
                    .ToList();

                // 这一组现在一个服务都没有 —— 连表头也不加。
                if (services.Count == 0) continue;

                body.Add(Widgets.SectionHeader(
                    Copy.Get(section.TitleKey),
                    Copy.Get(section.DescriptionKey)));

                foreach (var descriptor in services)
                {
                    body.Add(ServiceCard(descriptor, state, nav));
                }
            }

            if (body.Count == 0)
            {
                body.Add(DesignTokens.Body(Copy.Get(CopyKeys.HomeEmpty)).Foreground(Theme.SecondaryText));
            }
        }

        return FlexColumn(
            Widgets.HeroBanner(
                Copy.Get(CopyKeys.HomeHeroTitle),
                Copy.Get(CopyKeys.HomeHeroDescription),
                HeroBannerUri,
                isDarkTheme),

            ScrollView(
                VStack(16, [.. body])
                    .Margin(DesignTokens.ContentPadding, 24, DesignTokens.ContentPadding, 40))
                .Flex(grow: 1, basis: 0));
    }

    private static ServiceAvailability AvailabilityOf(AppState state, ServiceDescriptor descriptor)
    {
        var status = state.Services.For(descriptor.Id);

        // 「还没结论」与「确实不可用」是两件事：前者不进任何分组，后者进「停止服务」。
        if (status.IsChecking) return ServiceAvailability.Unknown;
        if (status.Availability == ServiceAvailability.Unknown && descriptor.DeclaredAvailability != ServiceAvailability.Unknown)
            return descriptor.DeclaredAvailability;

        return status.Availability;
    }

    private static Element ServiceCard(ServiceDescriptor descriptor, AppState state, NavigationHandle<Route> nav)
    {
        var status = state.Services.For(descriptor.Id);
        var availability = AvailabilityOf(state, descriptor);

        var trailing = new List<Element>
        {
            DesignTokens.Body(availability.Label()).Foreground(Theme.PrimaryText),
            status.IsChecking
                ? ProgressRing().Width(DesignTokens.BadgeSize).Height(DesignTokens.BadgeSize)
                : Widgets.StatusBadge(availability),
        };

        // 只有「可用 + 有专属页面」的服务才给入口；其余状态保持纯展示（与设计稿一致）。
        Action? open = descriptor.HasDedicatedPage && availability == ServiceAvailability.Available
            ? () => nav.Replace(new Route.Service(descriptor.Id))
            : null;

        return Widgets.SettingCard(
            icon: Widgets.ServiceIcon(descriptor.IconUri),
            title: descriptor.DisplayName,
            caption: descriptor.Caption,
            trailing: FlexRow([.. trailing]) with
            {
                ColumnGap = 10,
                AlignItems = FlexAlign.Center,
            },
            onClick: open,
            actionToolTip: Copy.Get(CopyKeys.ServiceCardOpen));
    }
}
