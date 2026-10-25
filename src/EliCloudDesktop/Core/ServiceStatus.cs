
namespace EliCloudDesktop.Core;

/// <summary>某个服务在一次探测之后得到的结论。</summary>
/// <param name="ServiceId">平台服务名（<c>mc</c> / <c>pdf-decrypt</c> …）。</param>
/// <param name="Availability">可用性结论。</param>
/// <param name="Detail">给人看的补充说明（版本号、RCON 延迟、失败原因……）。</param>
/// <param name="CheckedAt">结论的产生时刻。</param>
/// <param name="IsChecking">本次结论是否仍在刷新中。</param>
internal sealed record ServiceStatus(
    string ServiceId,
    ServiceAvailability Availability,
    string? Detail,
    DateTimeOffset CheckedAt,
    bool IsChecking = false)
{
    /// <summary>尚未探测时的占位结论。</summary>
    public static ServiceStatus Unknown(string serviceId) =>
        new(serviceId, ServiceAvailability.Unknown, null, DateTimeOffset.MinValue);

    /// <summary>探测失败时的结论。</summary>
    public static ServiceStatus Failed(string serviceId, string detail) =>
        new(serviceId, ServiceAvailability.Unavailable, detail, DateTimeOffset.UtcNow);
}

/// <summary>
/// 全量服务状态。
/// 用 <see cref="IReadOnlyList{T}"/> 而不是字典：record 的值相等比较对集合走引用比较，
/// 每次探测产出的新列表天然是「不同的值」，正好驱动重新渲染。
/// </summary>
internal sealed record ServiceStatusSnapshot(IReadOnlyList<ServiceStatus> Items)
{
    /// <summary>空快照。</summary>
    public static readonly ServiceStatusSnapshot Empty = new([]);

    /// <summary>取某个服务的结论；没有记录则视为 <see cref="ServiceAvailability.Unknown"/>。</summary>
    public ServiceStatus For(string serviceId) =>
        Items.FirstOrDefault(i => i.ServiceId == serviceId) ?? ServiceStatus.Unknown(serviceId);

    /// <summary>该服务此刻是否可用 —— Nav 条目与主页分组都以它为准。</summary>
    public bool IsAvailable(string serviceId) =>
        For(serviceId).Availability == ServiceAvailability.Available;
}
