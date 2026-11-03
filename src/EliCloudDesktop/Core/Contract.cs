using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EliCloudDesktop.Core;

/// <summary>
/// 契约里的硬数字与超时，全部集中在 <c>contracts/validation.md</c> 这一份事实来源上。
/// </summary>
/// <remarks>
/// 页面与服务层都从这里取值，不要在别处再写一遍 5 MB / 512 / 20 秒之类的字面量：
/// 四端必须用同一套规则，改规则时只改契约。
/// </remarks>
internal static partial class Contract
{
    // ── 超时（validation.md §5）──────────────────────────────────────────
    /// <summary>普通请求（列表、详情、登录、注册）：20 秒。</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    /// <summary>服务探活（GET /mc/healthz）：8 秒。</summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    /// <summary>资料保存（PUT /userinfo/v1/profile）：30 秒。</summary>
    public static readonly TimeSpan ProfileSaveTimeout = TimeSpan.FromSeconds(30);

    /// <summary>图片上传（PUT /userinfo/v1/profile/{kind}）：60 秒。</summary>
    public static readonly TimeSpan ImageUploadTimeout = TimeSpan.FromSeconds(60);

    // ── 图片（validation.md §4）──────────────────────────────────────────
    /// <summary>头像上传上限：5 MB（1024 进制）。</summary>
    public const long AvatarMaxBytes = 5L * 1024 * 1024;

    /// <summary>头图上传上限：10 MB（1024 进制）。</summary>
    public const long BannerMaxBytes = 10L * 1024 * 1024;

    /// <summary>头像输出最大边长：512 px。</summary>
    public const int AvatarMaxEdge = 512;

    /// <summary>头图输出最大边长：1600 px。</summary>
    public const int BannerMaxEdge = 1600;

    /// <summary>头图裁切/展示比例：5:1。</summary>
    public const double BannerAspectRatio = 5d;

    // ── 文本（validation.md §2、§3）──────────────────────────────────────
    /// <summary>昵称最多 32 个码点。</summary>
    public const int NicknameMaxChars = 32;

    /// <summary>简介最多 500 个码点。</summary>
    public const int BioMaxChars = 500;

    /// <summary>MC 备注最多 200 个码点。</summary>
    public const int McNoteMaxChars = 200;

    /// <summary>UA 只报产品/版本与平台，不带入口地址（build-config.json 的 userAgent 模板）。</summary>
    public static string UserAgent => PlatformConfig.UserAgent;
}

/// <summary>
/// 客户端提交前校验：与服务端规则同构，只做「提前拦住」，不放宽服务端限制。
/// </summary>
/// <remarks>
/// 这里的函数同时被 UI 的「按钮是否可用」和提交前的防御性检查使用 —— 只有一份判据。
/// </remarks>
internal static partial class Validation
{
    /// <summary>昵称是否可提交：trim + NFC 后 1–32 个码点，且不含 C*（控制/格式/代理/私用/未分配）。</summary>
    public static bool IsValidNickname(string? value)
    {
        var cleaned = value?.Trim().Normalize(NormalizationForm.FormC);
        if (string.IsNullOrEmpty(cleaned)) return false;

        var count = 0;
        foreach (var rune in cleaned.EnumerateRunes())
        {
            if (IsDisallowedRune(rune, allowNewline: false)) return false;
            count++;
        }

        return count <= Contract.NicknameMaxChars;
    }

    /// <summary>简介归一化：NFC、\r\n/\r → \n、trim；空串等价于清空（返回 null）。</summary>
    public static string? NormalizeBio(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var text = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Normalize(NormalizationForm.FormC)
            .Trim();

        return text.Length == 0 ? null : text;
    }

    /// <summary>简介是否可提交：归一化后 ≤500 个码点，只允许 \n 一个控制字符。</summary>
    public static bool IsValidBio(string? value)
    {
        var text = NormalizeBio(value);
        if (text is null) return true;

        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsDisallowedRune(rune, allowNewline: true)) return false;
            count++;
        }

        return count <= Contract.BioMaxChars;
    }

    /// <summary>MC 用户名：<c>^[A-Za-z0-9_]{3,16}$</c>（服务端归一为小写后存储）。</summary>
    public static bool IsValidMinecraftName(string? value) =>
        value is not null && MinecraftNamePattern().IsMatch(value.Trim());

    /// <summary>MC 备注：trim 后 1–200 个码点，不含控制字符。</summary>
    public static bool IsValidMcNote(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return false;

        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsDisallowedRune(rune, allowNewline: false)) return false;
            count++;
        }

        return count <= Contract.McNoteMaxChars;
    }

    /// <summary>注册用户名：3–32 位，字母数字开头，只含 <c>A-Z a-z 0-9 _ . -</c>，不能是邮箱。</summary>
    public static bool IsValidRegistrationUsername(string? value) =>
        value is not null && RegistrationUsernamePattern().IsMatch(value.Trim());

    /// <summary>密码的客户端前置检查：8–64 字符、UTF-8 ≤72 字节、同时含字母与数字。</summary>
    /// <remarks>最终裁判仍是服务端；这里只拦明显不满足规则的值。</remarks>
    public static bool IsPlausiblePassword(string? value)
    {
        if (value is null) return false;
        if (value.Length is < 8 or > 64) return false;
        if (Encoding.UTF8.GetByteCount(value) > 72) return false;

        var hasLetter = false;
        var hasDigit = false;
        foreach (var ch in value)
        {
            if (char.IsLetter(ch)) hasLetter = true;
            else if (char.IsAsciiDigit(ch)) hasDigit = true;
        }

        return hasLetter && hasDigit;
    }

    /// <summary>邮箱只做「有 @ 和 .」级别的前置检查，不自己实现 RFC 5322。</summary>
    public static bool LooksLikeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var at = value.IndexOf('@');
        return at > 0 && value.IndexOf('.', at) > at + 1 && value.Length <= 254;
    }

    private static bool IsDisallowedRune(Rune rune, bool allowNewline)
    {
        if (allowNewline && rune.Value == '\n') return false;

        return Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.Surrogate
            or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned;
    }

    [GeneratedRegex("^[A-Za-z0-9_]{3,16}$")]
    private static partial Regex MinecraftNamePattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]{2,31}$")]
    private static partial Regex RegistrationUsernamePattern();
}
