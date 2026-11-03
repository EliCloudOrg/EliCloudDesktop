using System.Net;
using System.Text;

namespace EliCloud.ContractTests;

/// <summary>
/// 按 fixture 回放的 <see cref="HttpMessageHandler"/>，并记录客户端实际发出的请求。
/// 故意不 mock 任何 SDK 内部类型：SsoClient / McClient 走的仍然是真实传输层。
/// </summary>
internal sealed class FixtureHandler : HttpMessageHandler
{
    private readonly Fixture _fixture;
    private readonly TimeSpan _delay;

    public FixtureHandler(Fixture fixture, TimeSpan? delay = null)
    {
        _fixture = fixture;
        _delay = delay ?? TimeSpan.Zero;
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    public string? LastAuthorization { get; private set; }

    public string? LastContentType { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastAuthorization = request.Headers.Authorization?.ToString();
        LastContentType = request.Content?.Headers.ContentType?.ToString();
        if (request.Content is not null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
        }

        var response = new HttpResponseMessage((HttpStatusCode)_fixture.ResponseStatus);

        if (_fixture.ResponseJson is { } json)
        {
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        else if (_fixture.Raw is not null || _fixture.RawFile is not null)
        {
            response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(_fixture.ReadRawText() ?? string.Empty));
        }
        else if (_fixture.BodyFile is not null)
        {
            var bytes = _fixture.ReadBodyFile()
                ?? throw new InvalidOperationException($"fixture 引用的二进制文件不存在：{_fixture.BodyFile}");
            response.Content = new ByteArrayContent(bytes);
        }

        foreach (var (key, value) in _fixture.ResponseHeaders)
        {
            if (string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                if (response.Content is null)
                {
                    response.Content = new ByteArrayContent(Array.Empty<byte>());
                }

                response.Content.Headers.Remove("Content-Type");
                response.Content.Headers.TryAddWithoutValidation("Content-Type", value);
                continue;
            }

            if (!response.Headers.TryAddWithoutValidation(key, value) && response.Content is not null)
            {
                response.Content.Headers.TryAddWithoutValidation(key, value);
            }
        }

        return response;
    }
}
