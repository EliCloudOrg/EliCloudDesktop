using EliCloud.Sdk;
using EliCloud.Sdk.Mc;
using EliCloud.Sdk.Sso;
using EliCloudDesktop.Core;

namespace EliCloudDesktop.Services;

/// <summary>
/// EliCloud.Sdk 的唯一入口封装。
/// </summary>
/// <remarks>
/// 刻意**不使用** SDK 的静态门面 <c>EliCloudService</c>：那里是进程级单例配置，
/// 一旦用户在设置页改了入口地址就没法干净地换回来（SDK README §2.4 把这条代价写明了）。
/// 这里改用实例 API（<see cref="SsoClient"/> / <see cref="McClient"/> + <see cref="EliCloudOptions"/>），
/// 换地址 = 换一个 <see cref="EliCloudGateway"/> 实例。
/// </remarks>
internal sealed class EliCloudGateway
{
    /// <summary>探活时单次请求的额外上限（SDK 自身默认 30 秒，对「点开就看状态」来说太长）。</summary>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    private readonly HttpClient _http;

    /// <summary>用构建时注入的平台入口构造的实例，作为 Context 的兜底默认值。</summary>
    public static readonly EliCloudGateway Default = new(PlatformConfig.BaseAddress);

    public EliCloudGateway(string baseAddress)
    {
        BaseAddress = baseAddress;
        Options = new EliCloudOptions
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            // 只报产品与版本，不带地址/域名 —— UA 会被写进服务端日志。
            UserAgent = "EliCloudDesktop/0.1",
            Timeout = TimeSpan.FromSeconds(20),
        };

        // 超时按请求由 SDK 控制，所以这里不需要 HttpClient 自己的超时（共享一个实例即可）。
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>当前平台入口地址。</summary>
    public string BaseAddress { get; }

    /// <summary>SDK 连接配置（各服务基址由它派生）。</summary>
    public EliCloudOptions Options { get; }

    private SsoClient Sso => new(_http, Options);

    private McClient Mc => new(_http, Options);

    private UserInfoApi UserInfo => new(_http, BaseAddress);

    // ---------------------------------------------------------------- 登录 / 注册

    /// <summary>发起注册；返回待邮箱验证的 registration，而不是账号。</summary>
    public Task<RegistrationPending> StartRegistrationAsync(
        string username,
        string password,
        string email,
        CancellationToken ct) =>
        Sso.StartRegistrationAsync(username, password, email, ct);

    /// <summary>查询待验证注册的状态。</summary>
    public Task<RegistrationStatus> GetRegistrationStatusAsync(string registrationId, CancellationToken ct) =>
        Sso.GetRegistrationStatusAsync(registrationId, ct);

    /// <summary>重发验证邮件。</summary>
    public Task<RegistrationPending> ResendRegistrationEmailAsync(string registrationId, CancellationToken ct) =>
        Sso.ResendRegistrationEmailAsync(registrationId, ct);

    /// <summary>取消待验证注册（best-effort，不阻塞 UI）。</summary>
    public Task CancelRegistrationAsync(string registrationId, CancellationToken ct) =>
        Sso.CancelRegistrationAsync(registrationId, ct);

    /// <summary>密码直连登录（仅适用于自家客户端的联调场景，见 SDK README §5.2）。</summary>
    /// <remarks>
    /// 登录成功后**一定**再拉一次 <c>/userinfo</c>：
    /// <list type="bullet">
    ///   <item><c>/login</c> 响应里那个 <c>user</c> 只是便利字段，服务端「有就带、没有就不带」——
    ///         账号没登记邮箱时它就是 <c>null</c>，光靠它无法区分「没有邮箱」与「没返回」；</item>
    ///   <item><c>/userinfo</c> 是按令牌作用域返回身份的权威来源，
    ///         将来换成设备码 / 授权码 + PKCE（桌面端推荐做法）时，令牌响应里根本没有 <c>user</c>，
    ///         那时它是唯一的身份来源 —— 现在就按同一条路径走，免得换流程时才发现。</item>
    /// </list>
    /// </remarks>
    public async Task<SessionState> LoginAsync(string username, string password, CancellationToken ct)
    {
        var result = await Sso.LoginAsync(username, password, ct).ConfigureAwait(false);
        var session = SessionState.FromLogin(username, result);

        try
        {
            return await RefreshUserInfoAsync(session, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // 令牌已经到手，补全失败不该让这次登录作废 —— 先用 /login 带回的信息，
            // 页面顶多显示「未绑定邮箱」，而不是卡在登录页。
            return session;
        }
    }

    /// <summary>拉一次 userinfo，把身份信息（用户名 / 邮箱 / sub / scope）补全到会话里。</summary>
    /// <remarks>
    /// <c>email_verified</c> 由服务端按 <c>users.email_verified_at</c> 返回：
    /// 新注册必须完成邮箱确认；历史账号/管理员建号可能仍为 <c>false</c>。
    /// </remarks>
    public async Task<SessionState> RefreshUserInfoAsync(SessionState session, CancellationToken ct)
    {
        if (session.AccessToken is not { Length: > 0 } token) return session;

        var me = await Sso.GetUserInfoAsync(token, false, ct).ConfigureAwait(false);
        return session with
        {
            Subject = me.Sub,
            Username = string.IsNullOrWhiteSpace(me.PreferredUsername) ? session.Username : me.PreferredUsername,
            Email = string.IsNullOrWhiteSpace(me.Email) ? session.Email : me.Email,
            EmailVerified = me.EmailVerified ?? session.EmailVerified,
            Scope = string.IsNullOrWhiteSpace(me.Scope) ? session.Scope : me.Scope,
        };
    }

    // ---------------------------------------------------------------- 服务探活

    /// <summary>探测一个服务的可用性。这是「可用 / 不可用」两种状态**唯一的**判定来源。</summary>
    public async Task<ServiceStatus> ProbeAsync(ServiceDescriptor descriptor, CancellationToken ct)
    {
        if (!descriptor.IsProbeEnabled)
        {
            // 本版本还没有探测手段：用清单里的声明状态，而不是猜一个「不可用」。
            return new ServiceStatus(descriptor.Id, descriptor.DeclaredAvailability, null, DateTimeOffset.UtcNow);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            return descriptor.Id switch
            {
                "mc" => ToStatus(descriptor.Id, await Mc.GetHealthAsync(timeout.Token).ConfigureAwait(false)),
                _ => new ServiceStatus(descriptor.Id, ServiceAvailability.Unknown, "尚无探测实现", DateTimeOffset.UtcNow),
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // 组件卸载导致的取消，交给上层忽略
        }
        catch (OperationCanceledException)
        {
            return ServiceStatus.Failed(descriptor.Id, $"探测超时（{ProbeTimeout.TotalSeconds:0} 秒）");
        }
        catch (EliCloudApiException ex)
        {
            // 只留错误码：它是平台的契约值，不含主机名、路径等部署信息。
            return ServiceStatus.Failed(descriptor.Id, $"平台返回 {ex.StatusCode} {ex.Error}");
        }
        catch (Exception)
        {
            // 网络层的异常消息里通常带着主机名与端口，属于我方基础设施信息，一律不落到数据里。
            return ServiceStatus.Failed(descriptor.Id, "连不上服务器");
        }
    }

    /// <summary>并发探测清单里所有支持探测的服务。</summary>
    public async Task<IReadOnlyList<ServiceStatus>> ProbeAllAsync(CancellationToken ct)
    {
        var tasks = ServiceCatalog.All.Select(d => ProbeAsync(d, ct)).ToArray();
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static ServiceStatus ToStatus(string serviceId, McHealthStatus health)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(health.Version)) parts.Add($"服务端 {health.Version}");

        if (health.Rcon is { } rcon)
        {
            parts.Add(rcon.Reachable
                ? $"RCON {rcon.LatencyMs:0.#} ms"
                : $"RCON 不可达：{rcon.Error ?? "未知原因"}");
        }

        if (health.WhitelistCount is { } count) parts.Add($"白名单 {count} 人");

        // 服务在跑但 RCON 挂了，对用户来说就是「加不了名字」= 不可用；
        // 这恰好是 SDK 特意保留 503 响应体的那个故障模式。
        var availability = health.IsHealthy ? ServiceAvailability.Available : ServiceAvailability.Unavailable;
        return new ServiceStatus(serviceId, availability, string.Join(" · ", parts), DateTimeOffset.UtcNow);
    }

    // ---------------------------------------------------------------- MC 业务

    /// <summary>MC 服务健康快照（RCON 不可达时服务端返回 503，SDK 仍会解析成对象）。</summary>
    public Task<McHealthStatus> GetMcHealthAsync(CancellationToken ct) => Mc.GetHealthAsync(ct);

    /// <summary>本账号的 MC 白名单条目与名额。</summary>
    public Task<McNameListResponse> ListMyMcNamesAsync(SessionState session, bool includeRemoved, CancellationToken ct) =>
        Mc.ListMyNamesAsync(session.AccessToken!, includeRemoved, ct);

    /// <summary>提交一个 MC 用户名（幂等：重复提交同名返回已有条目）。</summary>
    public Task<McNameEntry> AddMcNameAsync(SessionState session, string name, string note, CancellationToken ct) =>
        Mc.AddNameAsync(session.AccessToken!, name, note, ct);

    /// <summary>撤回自己的 MC 白名单条目。</summary>
    public Task RemoveMcNameAsync(SessionState session, string entryId, CancellationToken ct) =>
        Mc.RemoveNameAsync(session.AccessToken!, entryId, ct);

    // ---------------------------------------------------------------- 个人资料

    /// <summary>读取自己的资料；不存在记录时服务端也返回 200 + null 字段。</summary>
    public async Task<UserProfile> GetMyProfileAsync(SessionState session, CancellationToken ct)
    {
        using var timeout = LinkedTimeout(ct, TimeSpan.FromSeconds(20));
        return await UserInfo.GetMyProfileAsync(session.AccessToken!, timeout.Token).ConfigureAwait(false);
    }

    /// <summary>读取其他已登录用户的公开资料。</summary>
    public async Task<UserProfile> GetProfileAsync(
        SessionState session,
        string sub,
        CancellationToken ct)
    {
        using var timeout = LinkedTimeout(ct, TimeSpan.FromSeconds(20));
        return await UserInfo.GetProfileAsync(session.AccessToken!, sub, timeout.Token).ConfigureAwait(false);
    }

    /// <summary>全量保存昵称与简介。</summary>
    public async Task<UserProfile> UpdateMyProfileAsync(
        SessionState session,
        string nickname,
        string? bio,
        CancellationToken ct)
    {
        using var timeout = LinkedTimeout(ct, TimeSpan.FromSeconds(30));
        return await UserInfo
            .UpdateMyProfileAsync(session.AccessToken!, nickname, bio, timeout.Token)
            .ConfigureAwait(false);
    }

    /// <summary>上传并替换头像 / 头图；kind 只能是 avatar 或 banner。</summary>
    public async Task<UserProfile> UploadMyImageAsync(
        SessionState session,
        string kind,
        byte[] bytes,
        string contentType,
        CancellationToken ct)
    {
        using var timeout = LinkedTimeout(ct, TimeSpan.FromSeconds(60));
        return await UserInfo
            .UploadMyImageAsync(session.AccessToken!, kind, bytes, contentType, timeout.Token)
            .ConfigureAwait(false);
    }

    public async Task DeleteMyImageAsync(SessionState session, string kind, CancellationToken ct)
    {
        using var timeout = LinkedTimeout(ct, TimeSpan.FromSeconds(20));
        await UserInfo.DeleteMyImageAsync(session.AccessToken!, kind, timeout.Token).ConfigureAwait(false);
    }

    public async Task<ImagePayload> GetMyImageAsync(
        SessionState session,
        string kind,
        CancellationToken ct)
    {
        using var timeout = LinkedTimeout(ct, TimeSpan.FromSeconds(20));
        return await UserInfo.GetMyImageAsync(session.AccessToken!, kind, timeout.Token).ConfigureAwait(false);
    }

    private static CancellationTokenSource LinkedTimeout(CancellationToken ct, TimeSpan timeout)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);
        return linked;
    }
}
