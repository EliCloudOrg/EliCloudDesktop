using System.Text.Json;

namespace EliCloud.ContractTests;

/// <summary>fixture 里 <c>expected</c> 段的断言提示。</summary>
internal sealed record FixtureExpectation(
    string? ErrorCode,
    string? CopyKey,
    double? RetryAfterSeconds,
    bool Malformed,
    bool NonJson,
    string? Binary);

/// <summary>一个 fixture：请求描述 + 响应描述（与 <c>contracts/mock_server.py</c> 同一格式）。</summary>
internal sealed class Fixture
{
    public required string Name { get; init; }
    public required string OperationId { get; init; }
    public required string Directory { get; init; }
    public required string FullPath { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public Dictionary<string, string> RequestHeaders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> RequestQuery { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? RequestJson { get; init; }
    public Dictionary<string, string>? RequestForm { get; init; }
    public bool RequestIsMultipart { get; init; }
    public string? RequestMultipartFile { get; init; }

    public required int ResponseStatus { get; init; }
    public Dictionary<string, string> ResponseHeaders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ResponseJson { get; init; }
    public string? Raw { get; init; }
    public string? RawFile { get; init; }
    public string? BodyFile { get; init; }

    public FixtureExpectation Expected { get; init; } = new(null, null, null, false, false, null);

    public byte[]? ReadBodyFile()
    {
        var name = BodyFile ?? RawFile;
        if (name is null)
        {
            return null;
        }

        foreach (var candidate in new[] { System.IO.Path.Combine(Directory, name), System.IO.Path.Combine(Directory, "..", name) })
        {
            if (File.Exists(candidate))
            {
                return File.ReadAllBytes(candidate);
            }
        }

        return null;
    }

    public byte[]? ReadMultipartFileBytes()
    {
        if (RequestMultipartFile is null)
        {
            return null;
        }

        var candidate = System.IO.Path.Combine(Directory, RequestMultipartFile);
        return File.Exists(candidate) ? File.ReadAllBytes(candidate) : null;
    }

    public string? ReadRawText()
    {
        if (Raw is not null)
        {
            return Raw;
        }

        var bytes = ReadBodyFile();
        return bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
    }
}

internal static class FixtureLoader
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static string FindContractsRoot(string? explicitRoot)
    {
        if (!string.IsNullOrEmpty(explicitRoot))
        {
            return System.IO.Path.GetFullPath(explicitRoot);
        }

        var candidates = new List<string>();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && directory is not null; depth++)
        {
            candidates.Add(directory.FullName);
            directory = directory.Parent;
        }

        candidates.Add(Directory.GetCurrentDirectory());

        foreach (var candidate in candidates)
        {
            if (File.Exists(System.IO.Path.Combine(candidate, "openapi.yaml"))
                && Directory.Exists(System.IO.Path.Combine(candidate, "fixtures")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            "找不到 contracts 目录（需要 openapi.yaml + fixtures/）。用 --contracts <dir> 指定。");
    }

    public static IReadOnlyList<Fixture> LoadAll(string contractsRoot)
    {
        var fixturesDir = System.IO.Path.Combine(contractsRoot, "fixtures");
        var fixtures = new List<Fixture>();
        foreach (var file in Directory.GetFiles(fixturesDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                fixtures.Add(Load(file));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"fixture 解析失败：{file} → {ex.Message}", ex);
            }
        }

        fixtures.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        return fixtures;
    }

    public static Fixture Load(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(file), JsonOptions);
        var root = document.RootElement;

        var request = root.GetProperty("request");
        var response = root.GetProperty("response");

        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (request.TryGetProperty("headers", out var headers))
        {
            foreach (var property in headers.EnumerateObject())
            {
                requestHeaders[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (response.TryGetProperty("headers", out var responseHeaderNode))
        {
            foreach (var property in responseHeaderNode.EnumerateObject())
            {
                responseHeaders[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        var requestQuery = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (request.TryGetProperty("query", out var queryNode))
        {
            foreach (var property in queryNode.EnumerateObject())
            {
                requestQuery[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.GetRawText();
            }
        }

        Dictionary<string, string>? form = null;
        if (request.TryGetProperty("form", out var formNode))
        {
            form = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in formNode.EnumerateObject())
            {
                form[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.GetRawText();
            }
        }

        var expected = new FixtureExpectation(null, null, null, false, false, null);
        if (root.TryGetProperty("expected", out var expectedNode))
        {
            expected = new FixtureExpectation(
                ReadString(expectedNode, "errorCode"),
                ReadString(expectedNode, "copyKey"),
                ReadDouble(expectedNode, "retryAfterSeconds"),
                ReadBool(expectedNode, "malformed"),
                ReadBool(expectedNode, "nonJson"),
                ReadString(expectedNode, "binary"));
        }

        string? multipartFile = null;
        if (request.TryGetProperty("multipart", out var multipartNode)
            && multipartNode.TryGetProperty("file", out var fileNode))
        {
            multipartFile = ReadString(fileNode, "fixture");
        }

        return new Fixture
        {
            Name = ReadString(root, "name") ?? System.IO.Path.GetFileNameWithoutExtension(file),
            OperationId = ReadString(root, "operationId") ?? string.Empty,
            Directory = System.IO.Path.GetDirectoryName(file)!,
            FullPath = file,
            Method = ReadString(request, "method") ?? "GET",
            Path = ReadString(request, "path") ?? "/",
            RequestHeaders = requestHeaders,
            RequestQuery = requestQuery,
            RequestJson = request.TryGetProperty("json", out var jsonNode) ? jsonNode.GetRawText() : null,
            RequestForm = form,
            RequestIsMultipart = request.TryGetProperty("multipart", out _),
            RequestMultipartFile = multipartFile,
            ResponseStatus = response.GetProperty("status").GetInt32(),
            ResponseHeaders = responseHeaders,
            ResponseJson = response.TryGetProperty("json", out var bodyNode) ? bodyNode.GetRawText() : null,
            Raw = ReadString(response, "raw"),
            RawFile = ReadString(response, "rawFile"),
            BodyFile = ReadString(response, "file"),
            Expected = expected,
        };
    }

    private static string? ReadString(JsonElement node, string name)
        => node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? ReadDouble(JsonElement node, string name)
        => node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    private static bool ReadBool(JsonElement node, string name)
        => node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
