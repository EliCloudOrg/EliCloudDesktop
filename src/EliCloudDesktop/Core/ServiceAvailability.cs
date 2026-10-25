
namespace EliCloudDesktop.Core;

/// <summary>
/// 一个平台服务当前对用户而言的可用性。
/// </summary>
/// <remarks>
/// <b>当前只有 <see cref="Available"/> 与 <see cref="Unavailable"/> 可以被判定</b>
/// —— 它们来自客户端对服务健康端点的真实探测（见 <c>ServiceProbe</c>）。
/// 其余取值是**预留状态**，用来承载以后才会有的信息源，例如：
/// <list type="bullet">
///   <item><see cref="ComingSoon"/>：平台服务清单 <c>GET /core/v1/services</c> 里已登记、但本版本客户端还没有探测手段的服务。</item>
///   <item><see cref="Maintenance"/>：服务自报的维护窗口（目前没有任何接口返回它）。</item>
///   <item><see cref="Unknown"/>：尚未探测 / 探测被取消。</item>
/// </list>
/// 主页与 Nav 都是按这个取值分组的，所以「新增一种状态」只需要在
/// <see cref="ServiceCatalog"/> 里给出它的来源，UI 侧一行都不用改。
/// </remarks>
internal enum ServiceAvailability
{
    /// <summary>尚未探测出来。</summary>
    Unknown = 0,

    /// <summary>服务已登记但还没上线（预留状态）。</summary>
    ComingSoon,

    /// <summary>健康探测通过。</summary>
    Available,

    /// <summary>维护中（预留状态）。</summary>
    Maintenance,

    /// <summary>健康探测失败，或服务未部署。</summary>
    Unavailable,
}

/// <summary>服务状态的显示文案 —— 与 Figma 稿上的四处状态文案逐字对齐。</summary>
internal static class ServiceAvailabilityText
{
    public static string Label(this ServiceAvailability availability) => availability switch
    {
        ServiceAvailability.Available => "可用",
        ServiceAvailability.Unavailable => "不可用",
        ServiceAvailability.Maintenance => "维护中",
        ServiceAvailability.ComingSoon => "即将上线",
        _ => "检测中",
    };
}
