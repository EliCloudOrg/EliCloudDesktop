using EliCloud.Sdk;

namespace EliCloudDesktop.Core;

/// <summary>
/// 把平台返回的错误码翻译成用户看得懂的一句话。
/// </summary>
/// <remarks>
/// <para>
/// SDK README §5.7 要求「按 <c>Error</c> 分支，不要按文案分支」，这里就是那条规则的唯一落点。
/// </para>
/// <para>
/// 文案遵循 Windows 写作风格（friendly / helpful / concise）：
/// 不出现错误码、接口名、scope、RCON 这类术语；只说发生了什么、接下来怎么办。
/// </para>
/// </remarks>
internal static class EliCloudErrorText
{
    /// <summary>兜底：既不知道原因，也不该把技术细节倒给用户。</summary>
    public const string SomethingWentWrong = "出了点问题，稍后再试。";

    /// <summary>网络层失败（连不上、超时、DNS）统一说这句 —— 不暴露我方地址。</summary>
    public const string CannotReachServer = "连不上服务器。检查一下网络，然后再试。";

    public static string Describe(EliCloudApiException ex) => ex.Error switch
    {
        EliCloudErrorCodes.InvalidGrant => "账号或密码不对，再试一次。",
        EliCloudErrorCodes.UserExists => "这个用户名已经有人用了，换一个试试。",
        EliCloudErrorCodes.EmailExists => "这个邮箱已经注册过了，直接登录就行。",
        EliCloudErrorCodes.RegistrationDisabled => "注册暂时关闭了，请联系我们。",
        EliCloudErrorCodes.EmailNotVerified =>
            "这个账号的邮箱还没验证。请回到「创建账号」用同样的信息重新提交，或者查看注册时收到的邮件。",
        EliCloudErrorCodes.RegistrationPending =>
            "这个用户名或邮箱正在注册中。用原来的密码继续，或者稍后再试。",
        EliCloudErrorCodes.RegistrationNotFound or EliCloudErrorCodes.RegistrationExpired =>
            "这次注册已经失效了，回到「创建账号」重新来一次。",
        EliCloudErrorCodes.AlreadyVerified => "邮箱已经验证过了，直接登录就行。",
        EliCloudErrorCodes.EmailDeliveryFailed => "验证邮件没发出去，稍后再试一次。",
        EliCloudErrorCodes.EmailServiceUnavailable => "邮箱验证暂时不可用，稍后再试。",
        EliCloudErrorCodes.RateLimited or EliCloudErrorCodes.TooManyRequests =>
            $"试得有点频繁，等 {ex.RetryAfter?.TotalSeconds ?? 60:0} 秒再试。",
        EliCloudErrorCodes.InsufficientScope or EliCloudErrorCodes.AccessDenied =>
            "这次登录没有拿到需要的权限，重新登录后再试。",
        EliCloudErrorCodes.InvalidToken or EliCloudErrorCodes.ExpiredToken =>
            "登录状态过期了，重新登录一下吧。",
        EliCloudErrorCodes.NameTaken => "这个用户名已经有人用了，换一个，或者找管理员确认。",
        EliCloudErrorCodes.QuotaExceeded => "名额满了，先撤回一个再添加。",
        EliCloudErrorCodes.RconUnavailable => "服务器暂时连不上，稍后再试。",
        EliCloudErrorCodes.NotFound => "找不到这条记录了，可能已经被撤回。",
        "invalid_nickname" => "昵称不能为空，最多 32 个字符，也不能换行。",
        "invalid_bio" => "简介最多 500 个字符，不能包含控制字符。",
        "unsupported_image_type" => "只支持 PNG、JPEG 或 WebP 图片。",
        "invalid_image" => "这张图片读不出来，换一张试试。",
        "image_too_large" => "图片太大了：头像不超过 5 MB，头图不超过 10 MB。",
        "image_quota_exceeded" => "图片空间满了，先删掉旧图片再试。",
        "file_service_unavailable" => "图片服务暂时不可用，稍后再试。",
        "file_service_error" => "图片服务返回了意外结果，稍后再试。",
        "profile_not_found" => "还没有这个用户的资料。",
        "image_not_found" => "这张图片已经不在了。",
        "database_unavailable" => "资料暂时保存不了，稍后再试。",
        _ => SomethingWentWrong,
    };
}
