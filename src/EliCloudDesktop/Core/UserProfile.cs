namespace EliCloudDesktop.Core;

/// <summary>
/// 一张资料图片的元数据（字节仍在服务端，客户端按 sha256 缓存）。
/// </summary>
internal sealed record ProfileImageInfo(
    string ContentType,
    long SizeBytes,
    string Sha256,
    string UpdatedAt);

/// <summary>
/// 个人资料快照。字段与 userinfo 的 JSON 一一对应（snake_case 由序列化器映射）。
/// </summary>
internal sealed record UserProfile(
    string UserId,
    string? Nickname,
    string? Bio,
    ProfileImageInfo? Avatar,
    ProfileImageInfo? Banner,
    string? CreatedAt,
    string? UpdatedAt)
{
    /// <summary>Nav / 页头展示名：优先昵称，其次 SSO 用户名。</summary>
    public string DisplayName(string fallback) =>
        string.IsNullOrWhiteSpace(Nickname) ? fallback : Nickname;
}
