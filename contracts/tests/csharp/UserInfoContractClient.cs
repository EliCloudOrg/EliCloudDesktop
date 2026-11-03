using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EliCloud.Sdk;

namespace EliCloud.ContractTests;

// userinfo 的 DTO 在桌面端是内部类型（客户端也不该因为多一个 NuGet 包而卡住构建），
// 这里按契约字段独立声明一份，专门用来验证线缆上的 snake_case 能正确绑定。
internal sealed record ProfileImageInfoDto(
    string ContentType,
    long SizeBytes,
    string Sha256,
    string UpdatedAt);

internal sealed record UserProfileDto(
    string UserId,
    string? Nickname,
    string? Bio,
    ProfileImageInfoDto? Avatar,
    ProfileImageInfoDto? Banner,
    string? CreatedAt,
    string? UpdatedAt);

internal sealed record DeleteImageDto(bool Deleted, bool Changed);

internal sealed record ImagePayload(byte[] Bytes, string? ContentType, string? ETag, int Status);

/// <summary>
/// 与桌面端 <c>UserInfoApi</c> 同构的薄客户端：只用来证明 userinfo 契约能被正确消费。
/// 刻意不放进 SDK —— 与桌面端的取舍一致。
/// </summary>
internal sealed class UserInfoContractClient
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public UserInfoContractClient(HttpClient http) => _http = http;

    public Task<UserProfileDto> GetProfileAsync(string token, string path, CancellationToken ct) =>
        SendJsonAsync<UserProfileDto>(token, HttpMethod.Get, path, null, ct);

    public async Task<UserProfileDto> UpdateProfileAsync(
        string token,
        string nickname,
        string? bio,
        CancellationToken ct)
    {
        var payload = new ProfileUpdateBody(nickname, bio);
        return await SendJsonAsync<UserProfileDto>(token, HttpMethod.Put, "userinfo/v1/profile", payload, ct)
            .ConfigureAwait(false);
    }

    public async Task<UserProfileDto> UploadImageAsync(
        string token,
        string path,
        byte[] bytes,
        string contentType,
        CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", "avatar.png");

        using var request = new HttpRequestMessage(HttpMethod.Put, Endpoint(path)) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await SendAsync<UserProfileDto>(request, ct).ConfigureAwait(false);
    }

    public async Task<DeleteImageDto> DeleteImageAsync(string token, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, Endpoint(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await SendAsync<DeleteImageDto>(request, ct).ConfigureAwait(false);
    }

    public async Task<ImagePayload> GetImageAsync(string token, string path, string? ifNoneMatch, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (ifNoneMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && (int)response.StatusCode != 304)
        {
            throw await ErrorAsync(response, request.RequestUri, ct).ConfigureAwait(false);
        }

        var bytes = response.Content is null
            ? Array.Empty<byte>()
            : await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return new ImagePayload(
            bytes,
            response.Content?.Headers.ContentType?.MediaType,
            response.Headers.ETag?.Tag,
            (int)response.StatusCode);
    }

    private Uri Endpoint(string path) => new(new Uri(Program.BaseAddress, UriKind.Absolute), path);

    private async Task<T> SendJsonAsync<T>(
        string token,
        HttpMethod method,
        string path,
        object? payload,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, Endpoint(path));
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, options: JsonOptions);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await SendAsync<T>(request, ct).ConfigureAwait(false);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await ErrorAsync(response, request.RequestUri, ct).ConfigureAwait(false);
        }

        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
            return value ?? throw new JsonException("空响应体");
        }
        catch (JsonException ex)
        {
            throw new EliCloudMalformedResponseException(
                $"响应体不是合法 JSON：{request.RequestUri}",
                request.RequestUri,
                ex.Message);
        }
    }

    private static async Task<EliCloudApiException> ErrorAsync(
        HttpResponseMessage response,
        Uri? uri,
        CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
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

        return new EliCloudApiException(
            (int)response.StatusCode,
            new EliCloudError(error, description),
            uri,
            body,
            response.Headers.RetryAfter?.Delta);
    }

    private sealed record ProfileUpdateBody(
        [property: JsonPropertyName("nickname")] string Nickname,
        [property: JsonPropertyName("bio")] string? Bio);
}
