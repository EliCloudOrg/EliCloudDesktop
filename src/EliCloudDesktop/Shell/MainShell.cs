using EliCloudDesktop.Core;
using EliCloudDesktop.Pages;
using EliCloudDesktop.Services;
using EliCloudDesktop.Ui;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

using Microsoft.UI.Reactor.Navigation;

namespace EliCloudDesktop.Shell;

/// <summary>
/// 登录后的主框架：<see cref="NavigationView"/> + 页面宿主。
/// </summary>
/// <remarks>
/// 标题栏不在这里 —— 它属于窗口外壳，由 <c>App</c> 统一托管（登录前后同一条），
/// 这里只负责标题栏以下的那块：导航栏 + 页面。
/// <para>
/// <b>需求 #1 的落点就在这里</b>：<see cref="BuildMenuItems"/> 每次都按最新服务状态重算
/// <c>MenuItems</c>，所以某个服务从「可用」变成「不可用」时，它的 Nav 条目与入口会**自动消失**
/// （Reactor 是声明式 diff，不需要手动增删控件）；恢复可用时又会自动出现。
/// </para>
/// </remarks>
internal sealed class MainShell : Component
{
    private const string AvatarUri = "ms-appx:///Assets/Images/user-avatar-default.png";

    /// <summary>登录后的起始页。</summary>
    private static readonly Route InitialRoute = new Route.Home();

    public override Element Render()
    {
        var state = UseContext(EliCloudContexts.State);
        var gateway = UseContext(EliCloudContexts.Gateway);

        // 登录后整棵树的导航根就在这里：句柄由 MainShell 创建，
        // 并通过 <see cref="NavigationHost"/> 提供给各页面（页面用 child 模式 UseNavigation&lt;Route&gt;() 取到同一个句柄）。
        var nav = UseNavigation<Route>(InitialRoute);

        var lastProfileSignature = UseRef<string?>(null);

        var availabilityKey = string.Join(
            ",",
            ServiceCatalog.All.Select(d => $"{d.Id}={(state.Services.IsAvailable(d.Id) ? 1 : 0)}"));

        // 当前页面所依赖的服务一旦**确认**不可用，就把用户送回主页 —— 否则会停在一个已经消失的入口上。
        // 注意只在拿到确定结论时才动：Unknown（尚未探测完）不该把用户从服务页赶走。
        UseEffect(
            () =>
            {
                if (nav.CurrentRoute is not Route.Service service) return;
                if (state.Services.For(service.ServiceId).Availability == ServiceAvailability.Unavailable)
                {
                    nav.Replace(new Route.Home());
                }
            },
            availabilityKey);

        var (navAvatar, setNavAvatar) = UseState<ImageSource?>(null);
        var avatarSha = state.Profile?.Avatar?.Sha256;

        UseEffect(
            () =>
            {
                if (avatarSha is not { Length: > 0 })
                {
                    setNavAvatar(null);
                    return () => { };
                }

                var cts = new CancellationTokenSource();
                _ = LoadAvatarAsync(cts.Token);
                return () =>
                {
                    cts.Cancel();
                    cts.Dispose();
                };

                async Task LoadAvatarAsync(CancellationToken ct)
                {
                    try
                    {
                        var payload = await gateway.GetMyImageAsync(state.Session, "avatar", ct).ConfigureAwait(true);
                        var bitmap = await ProfileImageCache.LoadAsync(payload.Bytes, avatarSha, ct).ConfigureAwait(true);
                        if (!ct.IsCancellationRequested)
                        {
                            setNavAvatar(bitmap);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception)
                    {
                        // 头像拉不下来就回退默认图，不影响导航。
                        if (!ct.IsCancellationRequested) setNavAvatar(null);
                    }
                }
            },
            avatarSha);

        var displayName = state.Profile?.DisplayName(state.Session.DisplayName) ?? state.Session.DisplayName;
        // avatarSha 只说明「服务端有图」；位图加载完成后 navAvatar 才非 null。
        // 两者都要进签名，否则图拉回来时签名不变，Apply 不会重建 Nav 内容。
        var avatarKey = avatarSha is not { Length: > 0 }
            ? "default"
            : navAvatar is null
                ? "pending"
                : $"loaded:{avatarSha}";
        var profileSignature =
            $"{displayName}|{state.Session.DisplayEmail}|{avatarKey}";

        var view = (NavigationView([.. BuildMenuItems(state)], NavigationHost(nav, RouteMap))
            with
            {
                SelectedTag = RouteTags.For(nav.CurrentRoute),
                IsSettingsVisible = true,
                IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
                IsPaneToggleButtonVisible = false,
                PaneTitle = "EliCloud",
                OpenPaneLength = DesignTokens.NavPaneWidth,
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,

                // 标题栏与导航栏是纵向排布的邻居、不是叠在一起，所以不要那层「给标题栏让位」的自动内边距。
                IsTitleBarAutoPaddingEnabled = false,

                OnSelectedTagChanged = tag =>
                {
                    if (RouteTags.Parse(tag) is { } target) nav.Replace(target);
                },
                OnSettingsSelected = () => nav.Replace(new Route.Settings()),
            })
            .Set(native => NavFooterProfileItem.Apply(
                native,
                profileSignature,
                lastProfileSignature,
                state.Session,
                displayName,
                navAvatar,
                AvatarUri));

        // 外层（App 的标题栏 + 内容区）已经给出了这块的尺寸，这里只管铺满。
        return view;
    }

    /// <summary>
    /// 按当前服务状态构造 Nav 菜单。
    /// 「服务列表」表头只在至少有一个可用服务时才出现 —— 与主页「空分组不加表头」同一条规则。
    /// </summary>
    private static List<NavigationViewItemData> BuildMenuItems(AppState state)
    {
        var items = new List<NavigationViewItemData>
        {
            NavItem("主页", icon: "\uE80F", tag: RouteTags.Home),
        };

        var available = ServiceCatalog.All.Where(d => state.Services.IsAvailable(d.Id)).ToList();
        if (available.Count == 0) return items;

        items.Add(NavItemHeader("服务列表"));

        foreach (var descriptor in available)
        {
            items.Add(new NavigationViewItemData(descriptor.DisplayName, Tag: RouteTags.ForService(descriptor.Id))
            {
                IconElement = BitmapIcon(new Uri(descriptor.IconUri), showAsMonochrome: false),
            });
        }

        return items;
    }

    private static Element RouteMap(Route route) => route switch
    {
        Route.Home => Component<HomePage>(),
        Route.Profile => Component<ProfilePage>(),
        Route.Settings => Component<SettingsPage>(),
        Route.Service service when service.ServiceId == ServiceCatalog.Minecraft.Id => Component<McServicePage>(),
        Route.Service service => UnknownServicePage(service.ServiceId),
        _ => Component<HomePage>(),
    };

    private static Element UnknownServicePage(string serviceId) =>
        VStack(
            8,
            DesignTokens.PageTitle("服务页缺失").Foreground(Theme.PrimaryText),
            DesignTokens.Body($"清单里的服务 “{serviceId}” 还没有对应的页面实现。").Foreground(Theme.SecondaryText))
        .Margin(DesignTokens.ContentPadding);
}
