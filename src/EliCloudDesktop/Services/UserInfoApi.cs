using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using EliCloud.Sdk;
using EliCloudDesktop.Core;

namespace EliCloudDesktop.Services;

/// <summary>一张图片的字节与媒体类型（来自 userinfo 的代理接口）。</summary>
internal sealed record ImagePayload(byte[] Bytes, string ContentType);

/// <summary>
/// userinfo 的薄 HTTP 客户端。
/// </summary>
/// <remarks>
/// 刻意不放进 <c>EliCloud.Sdk</c>：新增 SDK 包会卡住桌面端「没有并列 SDK 仓库时走 NuGet」
/// 的独立构建路径。这里只依赖 <see cref="HttpClient"/> 与 SDK 里已有的
/// <see cref="EliCloudApiException"/>，错误码契约保持与其它服务一致。
/// </remarks>
internal sealed class UserInfoApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly Uri _base;

    public UserInfoApi(HttpClient http, string platformBaseAddress)
    {
        _http = http;
        _base = new Uri(new Uri(platformBaseAddress, UriKind.Absolute), "userinfo/");
    }

    // ---------------------------------------------------------------- 读取

    public Task<UserProfile> GetMyProfileAsync(string token, CancellationToken ct) =>
        GetJsonAsync<UserProfile>(token, "v1/profile", ct);

    public Task<UserProfile> GetProfileAsync(string token, string sub, CancellationToken ct) =>
        GetJsonAsync<UserProfile>(token, $"v1/profiles/{Uri.EscapeDataString(sub)}", ct);

    public async Task<ImagePayload> GetMyImageAsync(string token, string kind, CancellationToken ct) =>
        await GetImageAsync(token, $"v1/profile/{kind}", ct).ConfigureAwait(false);

    public async Task<ImagePayload> GetProfileImageAsync(
        string token,
        string sub,
        string kind,
        CancellationToken ct) =>
        await GetImageAsync(token, $"v1/profiles/{Uri.EscapeDataString(sub)}/{kind}", ct).ConfigureAwait(false);

    // ---------------------------------------------------------------- 写入

    public async Task<UserProfile> UpdateMyProfileAsync(
        string token,
        string nickname,
        string? bio,
        CancellationToken ct)
    {
        var payload = new ProfileUpdateRequest(nickname, bio);
        using var request = new HttpRequestMessage(HttpMethod.Put, Endpoint("v1/profile"))
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await SendJsonAsync<UserProfile>(request, ct).ConfigureAwait(false);
    }

    public async Task<UserProfile> UploadMyImageAsync(
        string token,
        string kind,
        byte[] bytes,
        string contentType,
        CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", $"{kind}.{ExtensionFor(contentType)}");

        using var request = new HttpRequestMessage(HttpMethod.Put, Endpoint($"v1/profile/{kind}"))
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await SendJsonAsync<UserProfile>(request, ct).ConfigureAwait(false);
    }

    public async Task DeleteMyImageAsync(string token, string kind, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, Endpoint($"v1/profile/{kind}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateErrorAsync(response, request.RequestUri, ct).ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------- 内部

    private async Task<T> GetJsonAsync<T>(string token, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await SendJsonAsync<T>(request, ct).ConfigureAwait(false);
    }

    private async Task<T> SendJsonAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateErrorAsync(response, request.RequestUri, ct).ConfigureAwait(false);
        }

        try
        {
            var value = await response.Content
                .ReadFromJsonAsync<T>(JsonOptions, ct)
                .ConfigureAwait(false);
            return value ?? throw new JsonException("响应体为空");
        }
        catch (JsonException ex)
        {
            throw new EliCloudApiException(
                (int)HttpStatusCode.BadGateway,
                new EliCloudError("invalid_response", "个人资料服务返回了无法解析的响应"),
                request.RequestUri,
                ex.Message);
        }
    }

    private async Task<ImagePayload> GetImageAsync(string token, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateErrorAsync(response, request.RequestUri, ct).ConfigureAwait(false);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new ImagePayload(bytes, contentType);
    }

    private Uri Endpoint(string path) => new(_base, path);

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/png" => "png",
        "image/webp" => "webp",
        _ => "jpg",
    };

    private static async Task<EliCloudApiException> CreateErrorAsync(
        HttpResponseMessage response,
        Uri? uri,
        CancellationToken ct)
    {
        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 错误体读不出来时仍然返回结构化异常，保留状态码。
        }

        string? error = null;
        string? description = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (document.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        error = errorElement.GetString();
                    }
                    if (document.RootElement.TryGetProperty("error_description", out var descriptionElement))
                    {
                        description = descriptionElement.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // 非 JSON 错误体：只保留状态码。
            }
        }

        var retryAfter = response.Headers.RetryAfter?.Delta;
        return new EliCloudApiException(
            (int)response.StatusCode,
            new EliCloudError(error, description),
            uri,
            body,
            retryAfter);
    }

    private sealed record ProfileUpdateRequest(string Nickname, string? Bio);
}
