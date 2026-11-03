using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EliCloud.Sdk;
using EliCloud.Sdk.MainApi;
using EliCloud.Sdk.Mc;
using EliCloud.Sdk.Sso;

namespace EliCloud.ContractTests;

internal sealed class Check
{
    public List<string> Failures { get; } = new();

    public void True(bool condition, string message)
    {
        if (!condition)
        {
            Failures.Add(message);
        }
    }

    public void Eq(string label, object? actual, object? expected)
    {
        if (!Equals(actual, expected))
        {
            Failures.Add($"{label}: 期望 {Show(expected)}，实际 {Show(actual)}");
        }
    }

    private static string Show(object? value) => value switch
    {
        null => "<null>",
        string text => $"\"{text}\"",
        _ => value.ToString() ?? "<null>",
    };
}

internal static class Program
{
    internal const string BaseAddress = "https://api.example.com";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private static string ContractsRoot { get; set; } = string.Empty;

    public static async Task<int> Main(string[] args)
    {
        string? contractsArg = ReadOption(args, "--contracts");
        string? filter = ReadOption(args, "--filter");

        string contractsRoot;
        try
        {
            contractsRoot = FixtureLoader.FindContractsRoot(contractsArg);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        var fixtures = FixtureLoader.LoadAll(contractsRoot)
            .Where(fixture => filter is null || fixture.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ContractsRoot = contractsRoot;

        Console.WriteLine($"contracts: {contractsRoot}");
        Console.WriteLine($"fixtures : {fixtures.Count}");

        var failures = new List<string>();
        var passed = 0;

        foreach (var fixture in fixtures)
        {
            var check = new Check();
            try
            {
                await RunAsync(fixture, check).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                check.Failures.Add($"未处理异常 {ex.GetType().Name}: {ex.Message}");
            }

            if (check.Failures.Count == 0)
            {
                passed++;
                Console.WriteLine($"  ok   {fixture.Name}");
            }
            else
            {
                Console.WriteLine($"  FAIL {fixture.Name}");
                foreach (var message in check.Failures)
                {
                    Console.WriteLine($"         - {message}");
                }

                failures.Add(fixture.Name);
            }
        }

        foreach (var (name, action) in new (string, Func<Task<Check>>)[]
                 {
                     ("synthetic/timeout", RunTimeoutTestAsync),
                     ("synthetic/retry-after-http-date", RunRetryAfterDateTestAsync),
                 })
        {
            var check = await action().ConfigureAwait(false);
            if (check.Failures.Count == 0)
            {
                passed++;
                Console.WriteLine($"  ok   {name}");
            }
            else
            {
                Console.WriteLine($"  FAIL {name}");
                foreach (var message in check.Failures)
                {
                    Console.WriteLine($"         - {message}");
                }

                failures.Add(name);
            }
        }

        var total = fixtures.Count + 2;
        Console.WriteLine();
        Console.WriteLine($"契约测试：{passed}/{total} 通过，{failures.Count} 失败");
        return failures.Count == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- 单项执行

    private static async Task RunAsync(Fixture fixture, Check check)
    {
        var handler = new FixtureHandler(fixture);
        using var http = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        var options = new EliCloudOptions
        {
            BaseAddress = new Uri(BaseAddress, UriKind.Absolute),
            Timeout = DefaultTimeout,
            // UA 里只放产品/版本，不放地址 —— 与客户端约定一致。
            UserAgent = "EliCloudContractTests/1.0",
        };

        var sso = new SsoClient(http, options);
        var mc = new McClient(http, options);
        var userinfo = new UserInfoContractClient(http);

        var token = AccessToken(fixture);
        var isHealth = fixture.OperationId == "mcHealth";
        var isNotModified = fixture.ResponseStatus == 304;
        var expectError = fixture.ResponseStatus >= 400 && !isHealth && !isNotModified;
        var verifyHttp = true;

        switch (fixture.OperationId)
        {
            case "login":
            {
                if (fixture.Expected.Malformed)
                {
                    await ExpectMalformedAsync(
                        () => sso.LoginAsync(Required(fixture, "username"), Required(fixture, "password")),
                        check).ConfigureAwait(false);
                    break;
                }

                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => sso.LoginAsync(Required(fixture, "username"), Required(fixture, "password")),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var result = await sso
                        .LoginAsync(Required(fixture, "username"), Required(fixture, "password"))
                        .ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("access_token", result.AccessToken, Text(body, "access_token"));
                    check.Eq("token_type", result.TokenType, Text(body, "token_type"));
                    check.Eq("expires_in", result.ExpiresIn, Number(body, "expires_in"));
                    check.Eq("refresh_token", result.RefreshToken, Text(body, "refresh_token"));
                    check.Eq("scope", result.Scope, Text(body, "scope"));
                    check.Eq("user.id", result.User?.Id, Text(body, "user", "id"));
                    check.Eq("user.username", result.User?.Username, Text(body, "user", "username"));
                    check.Eq("user.email", result.User?.Email, Text(body, "user", "email"));
                    check.Eq("user.email_verified", result.User?.EmailVerified, Bool(body, "user", "email_verified"));
                }

                break;
            }

            case "refreshToken":
            {
                var refreshToken = Required(fixture, "refresh_token");
                if (expectError)
                {
                    var error = await ExpectErrorAsync(() => sso.RefreshAsync(refreshToken), check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var result = await sso.RefreshAsync(refreshToken).ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("access_token", result.AccessToken, Text(body, "access_token"));
                    check.Eq("refresh_token", result.RefreshToken, Text(body, "refresh_token"));
                    check.Eq("expires_in", result.ExpiresIn, Number(body, "expires_in"));
                }

                break;
            }

            case "registerStart":
            {
                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => sso.StartRegistrationAsync("alice", "S3cret!pass", "alice@example.com"),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var result = await sso
                        .StartRegistrationAsync(
                            Required(fixture, "username"),
                            Required(fixture, "password"),
                            Required(fixture, "email"))
                        .ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("registration_id", result.RegistrationId, Text(body, "registration_id"));
                    check.Eq("status", result.Status, Text(body, "status"));
                    check.Eq("email_masked", result.EmailMasked, Text(body, "email_masked"));
                    check.Eq("expires_at", result.ExpiresAt, Text(body, "expires_at"));
                    check.Eq("resend_after", result.ResendAfter, Number(body, "resend_after"));
                    check.Eq("status", result.Status, "pending");
                }

                break;
            }

            case "getRegistrationStatus":
            {
                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => sso.GetRegistrationStatusAsync(Required(fixture, "registration_id")),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var result = await sso
                        .GetRegistrationStatusAsync(Required(fixture, "registration_id"))
                        .ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("registration_id", result.RegistrationId, Text(body, "registration_id"));
                    check.Eq("status", result.Status, Text(body, "status"));
                    check.Eq("email_masked", result.EmailMasked, Text(body, "email_masked"));
                    check.Eq("resend_after", result.ResendAfter, Number(body, "resend_after"));
                    check.Eq("reason", result.Reason, Text(body, "reason"));
                    check.Eq("user.id", result.User?.Id, Text(body, "user", "id"));
                    check.Eq("IsVerified", result.IsVerified, Text(body, "status") == "verified");
                    check.Eq("IsTerminal", result.IsTerminal, Text(body, "status") != "pending");
                }

                break;
            }

            case "resendRegistrationEmail":
            {
                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => sso.ResendRegistrationEmailAsync(Required(fixture, "registration_id")),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var result = await sso
                        .ResendRegistrationEmailAsync(Required(fixture, "registration_id"))
                        .ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("registration_id", result.RegistrationId, Text(body, "registration_id"));
                    check.Eq("resend_after", result.ResendAfter, Number(body, "resend_after"));
                }

                break;
            }

            case "cancelRegistration":
            {
                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => sso.CancelRegistrationAsync(Required(fixture, "registration_id")),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    await sso.CancelRegistrationAsync(Required(fixture, "registration_id")).ConfigureAwait(false);
                }

                break;
            }

            case "getUserInfo":
            case "postUserInfo":
            {
                var usePost = fixture.OperationId == "postUserInfo";
                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => sso.GetUserInfoAsync(token, usePost),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var result = await sso.GetUserInfoAsync(token, usePost).ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("sub", result.Sub, Text(body, "sub"));
                    check.Eq("preferred_username", result.PreferredUsername, Text(body, "preferred_username"));
                    check.Eq("email", result.Email, Text(body, "email"));
                    check.Eq("email_verified", result.EmailVerified, Bool(body, "email_verified"));
                    check.Eq("scope", result.Scope, Text(body, "scope"));
                }

                break;
            }

            case "getOidcDiscovery":
            {
                var result = await sso.GetDiscoveryDocumentAsync().ConfigureAwait(false);
                var body = Body(fixture);
                check.Eq("issuer", result.Issuer, Text(body, "issuer"));
                // 一个词的线缆名：按命名策略猜成 user_info_endpoint 会静默绑定成 null。
                check.Eq("userinfo_endpoint", result.UserInfoEndpoint, Text(body, "userinfo_endpoint"));
                check.Eq("jwks_uri", result.JwksUri, Text(body, "jwks_uri"));
                check.Eq("end_session_endpoint", result.EndSessionEndpoint, Text(body, "end_session_endpoint"));
                check.True(
                    result.CodeChallengeMethodsSupported?.Contains("S256") == true,
                    "code_challenge_methods_supported 必须含 S256");
                break;
            }

            case "getJwks":
            {
                var result = await sso.GetJsonWebKeySetAsync().ConfigureAwait(false);
                var body = Body(fixture);
                var expectedKeys = body?["keys"]?.AsArray().Count ?? 0;
                check.Eq("keys", result.Keys.Count, expectedKeys);
                if (result.Keys.Count > 0)
                {
                    check.Eq("kid", result.Keys[0].Kid, Text(body, "keys", "0", "kid"));
                }

                break;
            }

            case "authorize":
            {
                var request = new AuthorizationRequest
                {
                    ClientId = Query(fixture, "client_id"),
                    RedirectUri = Query(fixture, "redirect_uri"),
                    Scope = Query(fixture, "scope"),
                    State = Query(fixture, "state"),
                    Nonce = Query(fixture, "nonce"),
                    CodeChallenge = Query(fixture, "code_challenge"),
                    CodeChallengeMethod = Query(fixture, "code_challenge_method"),
                };
                var url = sso.BuildAuthorizationUrl(request);
                var actual = ParseQuery(url.Query);
                check.Eq("response_type", actual.GetValueOrDefault("response_type"), "code");
                check.Eq("client_id", actual.GetValueOrDefault("client_id"), request.ClientId);
                check.Eq("redirect_uri", actual.GetValueOrDefault("redirect_uri"), request.RedirectUri);
                check.Eq("scope", actual.GetValueOrDefault("scope"), request.Scope);
                check.Eq("state", actual.GetValueOrDefault("state"), request.State);
                check.Eq("nonce", actual.GetValueOrDefault("nonce"), request.Nonce);
                check.Eq("code_challenge", actual.GetValueOrDefault("code_challenge"), request.CodeChallenge);
                check.Eq("code_challenge_method", actual.GetValueOrDefault("code_challenge_method"), "S256");
                verifyHttp = false;
                break;
            }

            case "token":
            {
                var form = fixture.RequestForm ?? new Dictionary<string, string>();
                var grant = form.GetValueOrDefault("grant_type", string.Empty);
                var clientSecret = form.GetValueOrDefault("client_secret");
                if (grant == "authorization_code")
                {
                    var request = new AuthorizationCodeTokenRequest
                    {
                        Code = form["code"],
                        RedirectUri = form["redirect_uri"],
                        ClientId = form["client_id"],
                        CodeVerifier = form.GetValueOrDefault("code_verifier"),
                        ClientSecret = clientSecret,
                    };
                    if (expectError)
                    {
                        var error = await ExpectErrorAsync(() => sso.ExchangeCodeAsync(request), check).ConfigureAwait(false);
                        CheckError(fixture, error, check);
                    }
                    else
                    {
                        var result = await sso.ExchangeCodeAsync(request).ConfigureAwait(false);
                        var body = Body(fixture);
                        check.Eq("access_token", result.AccessToken, Text(body, "access_token"));
                        check.Eq("token_type", result.TokenType, Text(body, "token_type"));
                        check.Eq("expires_in", result.ExpiresIn, Number(body, "expires_in"));
                        check.Eq("scope", result.Scope, Text(body, "scope"));
                        check.Eq("id_token", result.IdToken, Text(body, "id_token"));
                        check.Eq("refresh_token", result.RefreshToken, Text(body, "refresh_token"));
                    }
                }
                else if (grant == "refresh_token")
                {
                    var request = new RefreshTokenGrantRequest
                    {
                        RefreshToken = form["refresh_token"],
                        ClientId = form["client_id"],
                        ClientSecret = clientSecret,
                    };
                    var result = await sso.RefreshTokenAsync(request).ConfigureAwait(false);
                    check.Eq("access_token", result.AccessToken, Text(Body(fixture), "access_token"));
                }
                else if (grant == "urn:ietf:params:oauth:grant-type:device_code")
                {
                    var poll = await sso
                        .PollDeviceCodeAsync(form["device_code"], form["client_id"], clientSecret)
                        .ConfigureAwait(false);
                    // 协议状态不抛异常：由返回值表达。
                    check.Eq("poll.Error", poll.Error, fixture.Expected.ErrorCode);
                    if (fixture.Expected.ErrorCode == "authorization_pending")
                    {
                        check.True(poll.IsPending, "IsPending 应为 true");
                        check.True(!poll.IsTerminal, "IsTerminal 应为 false");
                    }
                    else if (fixture.Expected.ErrorCode == "slow_down")
                    {
                        check.True(poll.IsSlowDown, "IsSlowDown 应为 true");
                    }
                    else if (fixture.Expected.ErrorCode is null)
                    {
                        check.True(poll.IsSuccess, "IsSuccess 应为 true");
                        check.Eq("access_token", poll.Tokens?.AccessToken, Text(Body(fixture), "access_token"));
                    }
                }
                else
                {
                    check.True(false, $"fixture 的 grant_type 未覆盖：{grant}");
                }

                break;
            }

            case "deviceAuthorization":
            {
                var result = await sso
                    .StartDeviceAuthorizationAsync(Required(fixture, "client_id"), Required(fixture, "scope"))
                    .ConfigureAwait(false);
                var body = Body(fixture);
                check.Eq("device_code", result.DeviceCode, Text(body, "device_code"));
                check.Eq("user_code", result.UserCode, Text(body, "user_code"));
                check.Eq("verification_uri", result.VerificationUri, Text(body, "verification_uri"));
                check.Eq(
                    "verification_uri_complete",
                    result.VerificationUriComplete,
                    Text(body, "verification_uri_complete"));
                check.Eq("expires_in", result.ExpiresIn, Number(body, "expires_in"));
                check.Eq("interval", result.Interval, Number(body, "interval"));
                break;
            }

            case "logout":
            {
                var request = new LogoutRequest
                {
                    PostLogoutRedirectUri = Query(fixture, "post_logout_redirect_uri"),
                    IdTokenHint = Query(fixture, "id_token_hint"),
                    State = Query(fixture, "state"),
                };
                var url = sso.BuildLogoutUrl(request);
                var actual = ParseQuery(url.Query);
                check.Eq(
                    "post_logout_redirect_uri",
                    actual.GetValueOrDefault("post_logout_redirect_uri"),
                    request.PostLogoutRedirectUri);
                check.Eq("state", actual.GetValueOrDefault("state"), request.State);
                verifyHttp = false;
                break;
            }

            case "getMyProfile":
            {
                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => userinfo.GetProfileAsync(token, fixture.Path, CancellationToken.None),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var profile = await userinfo
                        .GetProfileAsync(token, fixture.Path, CancellationToken.None)
                        .ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("user_id", profile.UserId, Text(body, "user_id"));
                    check.Eq("nickname", profile.Nickname, Text(body, "nickname"));
                    check.Eq("bio", profile.Bio, Text(body, "bio"));
                    check.Eq("avatar", profile.Avatar is null, body?["avatar"] is null);
                    check.Eq("created_at", profile.CreatedAt, Text(body, "created_at"));
                }

                break;
            }

            case "getProfile":
            {
                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => userinfo.GetProfileAsync(token, fixture.Path, CancellationToken.None),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var profile = await userinfo
                        .GetProfileAsync(token, fixture.Path, CancellationToken.None)
                        .ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("user_id", profile.UserId, Text(body, "user_id"));
                    check.Eq("nickname", profile.Nickname, Text(body, "nickname"));
                    check.Eq("avatar.content_type", profile.Avatar?.ContentType, Text(body, "avatar", "content_type"));
                    check.Eq("avatar.sha256", profile.Avatar?.Sha256, Text(body, "avatar", "sha256"));
                }

                break;
            }

            case "updateMyProfile":
            {
                var nickname = Required(fixture, "nickname");
                var bio = fixture.RequestJson is null ? null : Optional(fixture, "bio");
                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => userinfo.UpdateProfileAsync(token, nickname, bio, CancellationToken.None),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var profile = await userinfo
                        .UpdateProfileAsync(token, nickname, bio, CancellationToken.None)
                        .ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("user_id", profile.UserId, Text(body, "user_id"));
                    check.Eq("nickname", profile.Nickname, Text(body, "nickname"));
                    check.Eq("bio", profile.Bio, Text(body, "bio"));
                }

                break;
            }

            case "getMyProfileImage":
            case "getProfileImage":
            {
                var ifNoneMatch = fixture.RequestHeaders.TryGetValue("If-None-Match", out var value) ? value : null;
                var payload = await userinfo
                    .GetImageAsync(token, fixture.Path, ifNoneMatch, CancellationToken.None)
                    .ConfigureAwait(false);
                check.Eq("status", payload.Status, isNotModified ? 304 : 200);
                if (isNotModified)
                {
                    check.Eq("304 body", payload.Bytes.Length, 0);
                }
                else
                {
                    check.True(payload.Bytes.Length > 0, "图片字节为空");
                    var expectedType = fixture.ResponseHeaders.GetValueOrDefault("Content-Type");
                    check.Eq("Content-Type", payload.ContentType, expectedType);
                    var expectedEtag = fixture.ResponseHeaders.GetValueOrDefault("ETag");
                    if (expectedEtag is not null)
                    {
                        check.Eq("ETag", payload.ETag, expectedEtag);
                    }
                }

                break;
            }

            case "uploadMyProfileImage":
            {
                var bytes = fixture.ReadMultipartFileBytes() ?? fixture.ReadBodyFile();
                var contentType = "image/png";
                if (fixture.ResponseStatus >= 400)
                {
                    // 超限/类型错误的 fixture 也用同一张图；服务端只看声明的 Content-Type。
                    bytes ??= Encoding.UTF8.GetBytes("not-an-image");
                    contentType = fixture.Name.Contains("unsupported", StringComparison.Ordinal) ? "image/gif" : "image/png";
                }

                check.True(bytes is { Length: > 0 }, "fixture 没有可上传的图片字节");
                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => userinfo.UploadImageAsync(token, fixture.Path, bytes!, contentType, CancellationToken.None),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var profile = await userinfo
                        .UploadImageAsync(token, fixture.Path, bytes!, contentType, CancellationToken.None)
                        .ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("user_id", profile.UserId, Text(body, "user_id"));
                    check.Eq("avatar.sha256", profile.Avatar?.Sha256, Text(body, "avatar", "sha256"));
                    check.Eq("avatar.size_bytes", profile.Avatar?.SizeBytes, (long?)Number(body, "avatar", "size_bytes"));
                }

                break;
            }

            case "deleteMyProfileImage":
            {
                if (expectError)
                {
                    var error = await ExpectErrorAsync(
                        () => userinfo.DeleteImageAsync(token, fixture.Path, CancellationToken.None),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var result = await userinfo
                        .DeleteImageAsync(token, fixture.Path, CancellationToken.None)
                        .ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("deleted", result.Deleted, Bool(body, "deleted"));
                    check.Eq("changed", result.Changed, Bool(body, "changed"));
                }

                break;
            }

            case "mcHealth":
            {
                // 503 也要解析成对象：这不是错误响应。
                var health = await mc.GetHealthAsync().ConfigureAwait(false);
                var body = Body(fixture);
                check.Eq("status", health.Status, Text(body, "status"));
                check.Eq("rcon.reachable", health.Rcon?.Reachable, Bool(body, "rcon", "reachable"));
                check.Eq("whitelist_count", health.WhitelistCount, Number(body, "whitelist_count"));
                check.Eq("version", health.Version, Text(body, "version"));
                check.Eq("IsHealthy", health.IsHealthy, fixture.ResponseStatus == 200);
                break;
            }

            case "listMyMcNames":
            {
                if (expectError)
                {
                    var includeRemoved = Query(fixture, "include_removed") == "true";
                    var error = await ExpectErrorAsync(
                        () => mc.ListMyNamesAsync(token, includeRemoved),
                        check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var includeRemoved = Query(fixture, "include_removed") == "true";
                    var result = await mc.ListMyNamesAsync(token, includeRemoved).ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("sub", result.Sub, Text(body, "sub"));
                    check.Eq("quota.limit", result.Quota?.Limit, Number(body, "quota", "limit"));
                    check.Eq("quota.used", result.Quota?.Used, Number(body, "quota", "used"));
                    check.Eq("quota.remaining", result.Quota?.Remaining, Number(body, "quota", "remaining"));
                    check.Eq("names.count", result.Names.Count, body?["names"]?.AsArray().Count ?? 0);
                    if (result.Names.Count > 0)
                    {
                        check.Eq("names[0].name", result.Names[0].Name, Text(body, "names", "0", "name"));
                        check.Eq("names[0].name_display", result.Names[0].NameDisplay, Text(body, "names", "0", "name_display"));
                    }
                }

                break;
            }

            case "addMcName":
            {
                var name = Required(fixture, "name");
                var note = Required(fixture, "note");
                if (expectError)
                {
                    var error = await ExpectErrorAsync(() => mc.AddNameAsync(token, name, note), check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var entry = await mc.AddNameAsync(token, name, note).ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("id", entry.Id, Text(body, "id"));
                    check.Eq("name", entry.Name, Text(body, "name"));
                    check.Eq("name_display", entry.NameDisplay, Text(body, "name_display"));
                    check.Eq("status", entry.Status, Text(body, "status"));
                }

                break;
            }

            case "removeMcName":
            {
                var id = fixture.Path[(fixture.Path.LastIndexOf('/') + 1)..];
                if (expectError)
                {
                    var error = await ExpectErrorAsync(() => mc.RemoveNameAsync(token, id), check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var entry = await mc.RemoveNameAsync(token, id).ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("id", entry.Id, Text(body, "id"));
                    check.Eq("status", entry.Status, Text(body, "status"));
                    check.Eq("removed_at", entry.RemovedAt, Text(body, "removed_at"));
                }

                break;
            }

            case "mcIdentity":
            {
                if (expectError)
                {
                    var error = await ExpectErrorAsync(() => mc.GetIdentityAsync(token), check).ConfigureAwait(false);
                    CheckError(fixture, error, check);
                }
                else
                {
                    var identity = await mc.GetIdentityAsync(token).ConfigureAwait(false);
                    var body = Body(fixture);
                    check.Eq("sub", identity.Sub, Text(body, "sub"));
                    check.Eq("username", identity.Username, Text(body, "username"));
                    check.Eq("issuer", identity.Issuer, Text(body, "issuer"));
                    check.Eq("scope", identity.Scope, Text(body, "scope"));
                }

                break;
            }

            case "listCoreServices":
            {
                var mainApi = new MainApiClient(http, options);
                var services = await mainApi.ListServicesAsync().ConfigureAwait(false);
                var body = Body(fixture);
                var expectedCount = body?["services"]?.AsArray().Count ?? 0;
                check.Eq("services.count", services.Count, expectedCount);
                if (services.Count > 0)
                {
                    check.Eq("services[0].id", services[0].Id, Text(body, "services", "0", "id"));
                    check.Eq("services[0].path", services[0].Path, Text(body, "services", "0", "path"));
                    check.Eq("services[0].online", services[0].Online, Bool(body, "services", "0", "online"));
                }

                break;
            }

            default:
                check.True(false, $"没有为 operationId={fixture.OperationId} 写契约测试");
                verifyHttp = false;
                break;
        }

        if (verifyHttp)
        {
            VerifyRequest(fixture, handler, check);
        }
    }

    // ------------------------------------------------------------- 请求断言

    private static void VerifyRequest(Fixture fixture, FixtureHandler handler, Check check)
    {
        var request = handler.LastRequest;
        check.True(request is not null, "客户端没有发出 HTTP 请求");
        if (request is null)
        {
            return;
        }

        check.Eq("method", request.Method.Method, fixture.Method);
        check.Eq("path", request.RequestUri?.AbsolutePath, fixture.Path);

        foreach (var (key, value) in fixture.RequestHeaders)
        {
            if (string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                check.Eq("Authorization", handler.LastAuthorization, value);
                continue;
            }

            var actualValues = request.Headers.TryGetValues(key, out var values)
                ? string.Join(", ", values)
                : null;
            check.Eq($"header.{key}", actualValues, value);
        }

        var actualQuery = ParseQuery(request.RequestUri?.Query ?? string.Empty);
        foreach (var (key, value) in fixture.RequestQuery)
        {
            check.Eq($"query.{key}", actualQuery.GetValueOrDefault(key), value);
        }

        if (fixture.RequestJson is { } expectedJson)
        {
            var expected = JsonNode.Parse(expectedJson);
            var actual = handler.LastBody is null ? null : JsonNode.Parse(handler.LastBody);
            check.True(
                JsonNode.DeepEquals(expected, actual),
                $"请求体与 fixture 不一致：期望 {expected?.ToJsonString()}，实际 {handler.LastBody}");
        }

        if (fixture.RequestForm is { } expectedForm)
        {
            var actual = ParseQuery(handler.LastBody ?? string.Empty);
            foreach (var (key, value) in expectedForm)
            {
                // client_secret/client_id 会因客户端认证方式（Basic vs post）换成 Authorization 头。
                if (key == "client_secret")
                {
                    continue;
                }

                if (key == "client_id" && expectedForm.ContainsKey("client_secret"))
                {
                    continue;
                }

                check.Eq($"form.{key}", actual.GetValueOrDefault(key), value);
            }
        }

        if (fixture.RequestIsMultipart)
        {
            check.True(
                (handler.LastContentType ?? string.Empty).StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase),
                $"Content-Type 不是 multipart/form-data：{handler.LastContentType}");
            var body = handler.LastBody ?? string.Empty;
            check.True(
                body.Contains("name=\"file\"", StringComparison.Ordinal)
                || body.Contains("name=file", StringComparison.Ordinal),
                "multipart 请求里没有 file 字段");
        }
    }

    // ------------------------------------------------------------ 错误断言

    private static async Task<EliCloudApiException?> ExpectErrorAsync(Func<Task> action, Check check)
    {
        try
        {
            await action().ConfigureAwait(false);
            check.True(false, "期望抛出 EliCloudApiException，但调用成功");
            return null;
        }
        catch (EliCloudApiException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            check.True(false, $"期望 EliCloudApiException，实际 {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static async Task ExpectMalformedAsync(Func<Task> action, Check check)
    {
        try
        {
            await action().ConfigureAwait(false);
            check.True(false, "期望抛出 EliCloudMalformedResponseException（2xx 但不是合法 JSON）");
        }
        catch (EliCloudMalformedResponseException)
        {
            // 预期
        }
        catch (Exception ex)
        {
            check.True(false, $"期望 EliCloudMalformedResponseException，实际 {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void CheckError(Fixture fixture, EliCloudApiException? error, Check check)
    {
        if (error is null)
        {
            return;
        }

        check.Eq("status", error.StatusCode, fixture.ResponseStatus);

        if (fixture.Expected.ErrorCode is { } expectedCode)
        {
            check.Eq("error", error.Error, expectedCode);
        }

        if (fixture.Expected.NonJson)
        {
            check.True(error.Error is null, $"非 JSON 错误体时 Error 应为 null，实际 {error.Error}");
            check.True(
                (error.ResponseBody ?? string.Empty).Contains("<html>", StringComparison.OrdinalIgnoreCase),
                "非 JSON 错误体应保留在 ResponseBody 里");
        }

        if (fixture.Expected.RetryAfterSeconds is { } seconds)
        {
            check.True(error.RetryAfter is not null, "Retry-After 没有解析出来");
            if (error.RetryAfter is { } retryAfter)
            {
                check.True(
                    Math.Abs(retryAfter.TotalSeconds - seconds) <= 1,
                    $"RetryAfter={retryAfter.TotalSeconds:0.#}s，期望 {seconds}s");
            }

            check.True(error.IsRateLimited, "IsRateLimited 应为 true");
        }

        switch (fixture.Expected.ErrorCode)
        {
            case "invalid_token":
                check.True(error.IsInvalidToken, "IsInvalidToken 应为 true");
                break;
            case "email_not_verified":
                check.True(error.IsEmailNotVerified, "IsEmailNotVerified 应为 true");
                break;
            case "insufficient_scope":
                check.True(error.IsInsufficientScope, "IsInsufficientScope 应为 true");
                break;
        }

        if (fixture.Name.Contains("unicode", StringComparison.Ordinal))
        {
            // UTF-8 往返：中文/emoji 不能变成乱码或丢字符。
            check.True(
                error.ErrorDescription?.Contains("简介", StringComparison.Ordinal) == true,
                $"UTF-8 error_description 解码异常：{error.ErrorDescription}");
        }
    }

    // ------------------------------------------------------------- 补充测试

    private static async Task<Check> RunTimeoutTestAsync()
    {
        var check = new Check();
        var login = FixtureLoader.LoadAll(ContractsRoot)
            .FirstOrDefault(fixture => fixture.Name == "auth/login-success.json")
            ?? throw new InvalidOperationException("缺少 auth/login-success fixture");

        var handler = new FixtureHandler(login, TimeSpan.FromMilliseconds(500));
        using var http = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        var options = new EliCloudOptions
        {
            BaseAddress = new Uri(BaseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromMilliseconds(50),
        };
        var sso = new SsoClient(http, options);

        try
        {
            await sso.LoginAsync("alice", "S3cret!pass").ConfigureAwait(false);
            check.True(false, "超时应当抛 TimeoutException");
        }
        catch (TimeoutException)
        {
            // 预期：SDK 把「单次请求超时」翻译成 TimeoutException，而不是 TaskCanceledException。
        }
        catch (Exception ex)
        {
            check.True(false, $"期望 TimeoutException，实际 {ex.GetType().Name}: {ex.Message}");
        }

        return check;
    }

    private static async Task<Check> RunRetryAfterDateTestAsync()
    {
        var check = new Check();
        var synthetic = new Fixture
        {
            Name = "synthetic/retry-after-http-date",
            OperationId = "login",
            Directory = ContractsRoot,
            FullPath = "<synthetic>",
            Method = "POST",
            Path = "/auth/login",
            RequestJson = "{\"username\":\"alice\",\"password\":\"x\"}",
            ResponseStatus = 429,
            ResponseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Retry-After"] = DateTimeOffset.UtcNow.AddSeconds(45).ToString("R", CultureInfo.InvariantCulture),
            },
            ResponseJson = "{\"error\":\"rate_limited\",\"error_description\":\"稍后再试\"}",
        };

        var handler = new FixtureHandler(synthetic);
        using var http = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        var options = new EliCloudOptions { BaseAddress = new Uri(BaseAddress, UriKind.Absolute) };
        var sso = new SsoClient(http, options);

        try
        {
            await sso.LoginAsync("alice", "x").ConfigureAwait(false);
            check.True(false, "429 应当抛 EliCloudApiException");
        }
        catch (EliCloudApiException ex)
        {
            check.Eq("status", ex.StatusCode, 429);
            check.Eq("error", ex.Error, "rate_limited");
            check.True(ex.RetryAfter is not null, "HTTP-date 形式的 Retry-After 没有解析出来");
            if (ex.RetryAfter is { } retryAfter)
            {
                check.True(
                    retryAfter.TotalSeconds is > 30 and <= 45,
                    $"HTTP-date 解析结果应在 30–45 秒之间，实际 {retryAfter.TotalSeconds:0.#}s");
            }
        }

        return check;
    }

    // ---------------------------------------------------------------- 工具

    private static string? ReadOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static JsonNode? Body(Fixture fixture) =>
        fixture.ResponseJson is null ? null : JsonNode.Parse(fixture.ResponseJson);

    private static string AccessToken(Fixture fixture)
    {
        if (fixture.RequestHeaders.TryGetValue("Authorization", out var authorization))
        {
            const string prefix = "Bearer ";
            return authorization.StartsWith(prefix, StringComparison.Ordinal)
                ? authorization[prefix.Length..]
                : authorization;
        }

        return "contract-test-token";
    }

    private static string Required(Fixture fixture, string name) =>
        Optional(fixture, name) ?? throw new InvalidOperationException($"fixture {fixture.Name} 缺少请求字段 {name}");

    private static string? Optional(Fixture fixture, string name)
    {
        if (fixture.RequestForm is { } form && form.TryGetValue(name, out var formValue))
        {
            return formValue;
        }

        if (fixture.RequestJson is null)
        {
            return null;
        }

        var body = JsonNode.Parse(fixture.RequestJson);
        var node = body?[name];
        if (node is null)
        {
            return null;
        }

        return node.GetValueKind() == JsonValueKind.Null ? null : node.ToString();
    }

    private static string Query(Fixture fixture, string name, string fallback = "") =>
        fixture.RequestQuery.TryGetValue(name, out var value) ? value : fallback;

    private static string? Text(JsonNode? node, params string[] path)
    {
        var target = Navigate(node, path);
        if (target is null)
        {
            return null;
        }

        return target.GetValueKind() == JsonValueKind.Null ? null : target.ToString();
    }

    private static int? Number(JsonNode? node, params string[] path)
    {
        var target = Navigate(node, path);
        if (target is null || target.GetValueKind() == JsonValueKind.Null)
        {
            return null;
        }

        return target.GetValueKind() == JsonValueKind.Number
            ? target.GetValue<int>()
            : int.TryParse(target.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    private static bool? Bool(JsonNode? node, params string[] path)
    {
        var target = Navigate(node, path);
        if (target is null || target.GetValueKind() == JsonValueKind.Null)
        {
            return null;
        }

        return target.GetValue<bool>();
    }

    private static JsonNode? Navigate(JsonNode? node, params string[] path)
    {
        var current = node;
        foreach (var segment in path)
        {
            if (current is null)
            {
                return null;
            }

            if (int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                && current is JsonArray array)
            {
                if (index < 0 || index >= array.Count)
                {
                    return null;
                }

                current = array[index];
                continue;
            }

            current = current[segment];
        }

        return current;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
            result[Uri.UnescapeDataString(key.Replace('+', ' '))] =
                Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        return result;
    }
}
