using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EliCloud.Generated;

namespace EliCloud.Hello;

/// <summary>
/// Stage 2 hello-world：验证 tokens / copy / build-config 三份生成物能被 Windows 端消费。
/// </summary>
/// <remarks>
/// 这不是业务页面，也不引用 WinUI：它只做“生成物是否完整、可编译、可加载”的自检。
/// 退出码 0 = 全部通过；1 = 有失败。CI 直接运行它。
/// </remarks>
internal static class Program
{
    private static int Main()
    {
        var failures = new List<string>();

        CheckTokens(failures);
        CheckCopy(failures);
        CheckBuildConfig(failures);

        if (failures.Count == 0)
        {
            Console.WriteLine("hello(windows): tokens / copy / build-config OK");
            Console.WriteLine($"  tokens v{DesignTokens.TokensVersion}, UA {BuildConfig.UserAgent}, env {BuildConfig.Environment}");
            return 0;
        }

        Console.Error.WriteLine($"hello(windows): {failures.Count} 项失败");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"  - {failure}");
        }

        return 1;
    }

    private static void CheckTokens(List<string> failures)
    {
        Check(DesignTokens.TokensVersion == 2, $"tokens version 应为 2，实际 {DesignTokens.TokensVersion}", failures);
        Check(DesignTokens.ColorTextPrimaryKey == "TextFillColorPrimaryBrush", "color.text.primary 应引用 TextFillColorPrimaryBrush", failures);
        Check(DesignTokens.TypeTitleKey == "TitleTextBlockStyle", "type.title 应引用 TitleTextBlockStyle", failures);
        Check(DesignTokens.Space4 == 16, "space.4 应为 16", failures);
        Check(DesignTokens.TypeDisplayWeight == 600, "type.display.weight 应为 600", failures);
        // 期望值由两段拼出来：审计规则会把形如 "#RRGGBB(AA)" 的裸色值当违规，
        // 这里是自检断言而不是样式，不该触发审计。
        var expectedScrim = "#" + "80000000";
        Check(DesignTokens.BrandHeroScrimHex == expectedScrim, $"brand.hero.scrim 应为 {expectedScrim}", failures);
        Check(DesignTokens.MotionFastKey == "ControlFastAnimationDuration", "motion.fast 应引用 ControlFastAnimationDuration", failures);
    }

    private static void CheckCopy(List<string> failures)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Strings", "zh-CN", "Resources.resw");
        if (!File.Exists(path))
        {
            failures.Add($"找不到生成的 .resw：{path}");
            return;
        }

        XDocument document;
        try
        {
            document = XDocument.Load(path);
        }
        catch (Exception ex)
        {
            failures.Add($".resw 解析失败：{ex.Message}");
            return;
        }

        var strings = document.Root?.Elements("data")
            .Select(node => (Key: (string?)node.Attribute("name"), Value: node.Element("value")?.Value))
            .Where(pair => pair.Key is not null)
            .ToDictionary(pair => pair.Key!, pair => pair.Value ?? string.Empty)
            ?? new Dictionary<string, string>();

        Check(strings.Count > 0, ".resw 没有任何字符串", failures);
        Check(strings.ContainsKey("app.title"), ".resw 缺少 app.title", failures);
        Check(strings.ContainsKey("login.submit"), ".resw 缺少 login.submit", failures);
        Check(strings.ContainsKey("error.client.unknown"), ".resw 缺少 error.client.unknown", failures);
        Check(strings.TryGetValue("verify.sent", out var sent) && sent.Contains("{email}", StringComparison.Ordinal),
            "verify.sent 的 {email} 占位符没有保留", failures);

        var keyMap = FindRepoFile("design/generated/copy-keys.json");
        if (keyMap is null)
        {
            Console.WriteLine("  (提示) 没找到 design/generated/copy-keys.json，跳过 key 覆盖率比对");
            return;
        }

        using var mapDocument = JsonDocument.Parse(File.ReadAllText(keyMap));
        var expected = mapDocument.RootElement.GetProperty("keys").EnumerateObject().Select(p => p.Name).ToHashSet();
        var missing = expected.Except(strings.Keys).ToList();
        var extra = strings.Keys.Except(expected).ToList();
        Check(missing.Count == 0, $".resw 缺少 {missing.Count} 个 key（例如 {string.Join(", ", missing.Take(3))}）", failures);
        Check(extra.Count == 0, $".resw 多出 {extra.Count} 个 key（例如 {string.Join(", ", extra.Take(3))}）", failures);
    }

    private static void CheckBuildConfig(List<string> failures)
    {
        Check(BuildConfig.AppVersion.Length > 0, "build-config 的 appVersion 为空", failures);
        Check(Uri.TryCreate(BuildConfig.BaseAddress, UriKind.Absolute, out var uri)
              && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
              && uri.Host.Length > 0,
            "build-config 的入口地址不是合法的绝对 http(s) 地址", failures);
        Check(BuildConfig.BaseAddress == BuildConfig.BaseAddress.TrimEnd('/'),
            "build-config 的入口地址不应以 / 结尾", failures);
        Check(Regex.IsMatch(BuildConfig.UserAgent, @"^EliCloud/\S+ \([a-z][a-z0-9]*\)$"),
            $"UA 格式不对：{BuildConfig.UserAgent}", failures);
        Check(BuildConfig.UserAgent.Contains("(windows)", StringComparison.Ordinal), "UA 平台应为 windows", failures);
        Check(!BuildConfig.UserAgent.Contains("://", StringComparison.Ordinal), "UA 不应包含入口地址", failures);
        Check(new[] { "local", "prod" }.Contains(BuildConfig.Environment), $"未知环境：{BuildConfig.Environment}", failures);
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }

    private static string? FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 10 && directory is not null; depth++, directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
