using EliCloud.Sdk.Sso;

namespace EliCloudDesktop.Core;

/// <summary>
/// 一次登录会话的不可变快照。
/// 刻意做成 record：Reactor 的 UseState/UseReducer 与 Context 都按值比较，
/// 值相等的会话对象不会触发多余的重新渲染。
/// </summary>
/// <param name="AccessToken">业务接口用的 access token（<c>aud=elicloud-services</c>）。</param>
/// <param name="RefreshToken">仅密码直连/授权码 + <c>offline_access</c> 才会拿到。</param>
/// <param name="Scope">服务端回显的 scope 原文（空格分隔，整词匹配用）。</param>
/// <param name="Subject">OIDC <c>sub</c>：平台做数据隔离的唯一依据。</param>
/// <param name="Username"><c>preferred_username</c>。</param>
/// <param name="Email">注册时登记的邮箱。</param>
/// <param name="EmailVerified">邮箱是否已完成验证；无邮箱时为 <c>null</c>。</param>
/// <param name="ExpiresAt">access token 的绝对过期时刻（本地推算）。</param>
internal sealed record SessionState(
    string? AccessToken,
    string? RefreshToken,
    string? Scope,
    string? Subject,
    string? Username,
    string? Email,
    bool? EmailVerified,
    DateTimeOffset? ExpiresAt)
{
    /// <summary>未登录。</summary>
    public static readonly SessionState SignedOut = new(null, null, null, null, null, null, null, null);

    /// <summary>是否已登录。</summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    /// <summary>Nav 页脚条目与资料页标题显示的名字。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Username) ? "未登录" : Username;

    /// <summary>Nav 页脚条目与资料页副标题显示的邮箱。</summary>
    public string DisplayEmail => string.IsNullOrWhiteSpace(Email) ? "未绑定邮箱" : Email;

    /// <summary>access token 是否已过期（留 60 秒余量）。</summary>
    public bool IsAccessTokenExpired =>
        ExpiresAt is { } exp && exp - DateTimeOffset.UtcNow <= TimeSpan.FromSeconds(60);

    /// <summary>
    /// 登录成功后用 <c>/login</c> 的返回装配会话（用户名/邮箱随后由 <c>/userinfo</c> 校正）。
    /// </summary>
    /// <param name="username">用户输入的用户名 —— 平台只按用户名登录，用它兜底显示名。</param>
    /// <param name="result">SDK 的登录返回。</param>
    public static SessionState FromLogin(string username, LoginResult result)
    {
        var expiresAt = result.ExpiresIn is { } seconds and > 0
            ? DateTimeOffset.UtcNow.AddSeconds(seconds)
            : (DateTimeOffset?)null;

        return new SessionState(
            AccessToken: result.AccessToken,
            RefreshToken: result.RefreshToken,
            Scope: result.Scope,
            Subject: result.User?.Id,
            Username: string.IsNullOrWhiteSpace(result.User?.Username) ? username : result.User!.Username,
            Email: result.User?.Email,
            EmailVerified: result.User?.EmailVerified,
            ExpiresAt: expiresAt);
    }
}
