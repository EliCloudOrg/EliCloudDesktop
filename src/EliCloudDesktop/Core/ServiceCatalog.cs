
namespace EliCloudDesktop.Core;

/// <summary>
/// 客户端已知的平台服务清单。
/// </summary>
/// <remarks>
/// <b>这里是硬编码的</b>：主页要展示哪几个服务、每个服务的图标/标题/副标题/所属 scope，
/// 都是设计稿上定死的内容，不该由服务端临时决定。
/// 服务端清单接口（<c>GET /core/v1/services</c>）只用来补充「在线与否」，
/// 而它当前尚未部署（SDK README §7.1），所以状态改由客户端逐个探测得到。
/// </remarks>
/// <param name="Id">平台服务名 —— 与 <c>GetProvider(服务名)</c> 用的名字一致。</param>
/// <param name="DisplayName">卡片标题 / Nav 条目文案。</param>
/// <param name="Caption">卡片副标题（设计稿：绑定用户名以获取准入权限）。</param>
/// <param name="NavGlyph">Nav 条目图标（Segoe Fluent Icons 码位）。</param>
/// <param name="IconUri">卡片图标（ms-appx 语义化图片）。</param>
/// <param name="RequiredScope">调用该服务需要的 scope。</param>
/// <param name="DeclaredAvailability">
/// 该服务在没有探测结论时的**声明状态**。
/// 只有「本版本还探测不了、但平台文档里确实已登记」的服务才写 <see cref="ServiceAvailability.ComingSoon"/>。
/// </param>
/// <param name="IsProbeEnabled">本版本是否会对它做真实的健康探测。</param>
/// <param name="HasDedicatedPage">是否有专属页面（决定 Nav 条目是否可点、卡片是否显示箭头按钮）。</param>
internal sealed record ServiceDescriptor(
    string Id,
    string DisplayName,
    string Caption,
    string NavGlyph,
    string IconUri,
    string RequiredScope,
    ServiceAvailability DeclaredAvailability,
    bool IsProbeEnabled,
    bool HasDedicatedPage);

/// <summary>服务清单本体。</summary>
internal static class ServiceCatalog
{
    /// <summary>MC 服务器白名单服务。</summary>
    public static readonly ServiceDescriptor Minecraft = new(
        Id: "mc",
        DisplayName: "Minecraft 玩家服务器",
        Caption: "绑定用户名以获取准入权限",
        NavGlyph: "\uE7FC", // Segoe Fluent Icons: 方块/游戏手柄一类的占位码位
        IconUri: "ms-appx:///Assets/Images/service-icon-minecraft-block.png",
        RequiredScope: "mc:whitelist",
        DeclaredAvailability: ServiceAvailability.Unknown,
        IsProbeEnabled: true,
        HasDedicatedPage: true);

    /// <summary>
    /// PDF 在线解密。平台文档里已登记（前缀 <c>/pdf-decrypt</c>），
    /// 但契约与实现都还没有（SDK README §7.2），所以客户端声明为「即将上线」，
    /// 且不做探测、不给 Nav 条目 —— 这正是设计稿里第一组「新服务」要表达的状态。
    /// </summary>
    public static readonly ServiceDescriptor PdfDecrypt = new(
        Id: "pdf-decrypt",
        DisplayName: "PDF 在线解密",
        Caption: "上传加密 PDF，解密后直接下载",
        NavGlyph: "\uE8A5", // Segoe Fluent Icons: 文档
        IconUri: "ms-appx:///Assets/Images/service-icon-pdf-lock.png",
        RequiredScope: "pdf:read",
        DeclaredAvailability: ServiceAvailability.ComingSoon,
        IsProbeEnabled: false,
        HasDedicatedPage: false);

    /// <summary>全部已知服务（顺序即主页默认顺序）。</summary>
    public static readonly IReadOnlyList<ServiceDescriptor> All = [Minecraft, PdfDecrypt];

    /// <summary>按服务名查找。</summary>
    public static ServiceDescriptor? Find(string? serviceId) =>
        serviceId is null ? null : All.FirstOrDefault(s => s.Id == serviceId);
}
