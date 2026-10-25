
namespace EliCloudDesktop.Core;

/// <summary>
/// 应用内所有可导航的目的地。
/// 用 record 层次而不是裸 enum，是为了让「服务详情页」这种带参数的路由
/// 也能参与 record 的值相等比较 —— Reactor 的 NavigationHandle 依赖它。
/// </summary>
internal abstract record Route
{
    /// <summary>登录页（应用起点）。</summary>
    public sealed record Login : Route;

    /// <summary>主页。</summary>
    public sealed record Home : Route;

    /// <summary>个人资料页（由 Nav 的 FooterMenuItems 逃生舱入口进入）。</summary>
    public sealed record Profile : Route;

    /// <summary>设置页（Nav 内置的设置项）。</summary>
    public sealed record Settings : Route;

    /// <summary>某个平台服务的专属页面，例如 <c>new Route.Service("mc")</c>。</summary>
    public sealed record Service(string ServiceId) : Route;
}

/// <summary>路由 ↔ NavigationView Tag 的双向映射。</summary>
internal static class RouteTags
{
    public const string Home = "home";
    public const string Profile = "profile";
    public const string Settings = "settings";
    public const string ServicePrefix = "service:";

    public static string ForService(string serviceId) => ServicePrefix + serviceId;

    public static string For(Route? route) => route switch
    {
        Route.Home => Home,
        Route.Profile => Profile,
        Route.Settings => Settings,
        Route.Service s => ForService(s.ServiceId),
        _ => string.Empty,
    };

    public static Route? Parse(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        if (tag == Home) return new Route.Home();
        if (tag == Profile) return new Route.Profile();
        if (tag == Settings) return new Route.Settings();
        if (tag.StartsWith(ServicePrefix, StringComparison.Ordinal))
            return new Route.Service(tag[ServicePrefix.Length..]);
        return null;
    }
}
