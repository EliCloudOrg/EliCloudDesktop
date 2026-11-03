using EliCloud.Sdk;
using EliCloudDesktop.Generated;

namespace EliCloudDesktop.Core;

/// <summary>
/// 把平台返回的错误码映射到用户看得懂的一句话。
/// </summary>
/// <remarks>
/// <para>
/// 错误码 → 文案 key 的映射由 <c>contracts/errors.json</c> 生成（<c>Generated/CopyKeys.g.cs</c>），
/// 文案本体在 <c>contracts/copy/zh-CN.json</c> 生成的 <c>.resw</c> 里 —— 这里**不再维护第二份表**。
/// </para>
/// <para>
/// SDK README §5.7 要求「按 <c>Error</c> 分支，不要按文案分支」，<see cref="KeyFor"/> 就是那条规则的落点。
/// 文案遵循 Windows 写作风格：不出现错误码、接口名、scope、RCON、主机名或端口。
/// </para>
/// </remarks>
internal static class EliCloudErrorText
{
    /// <summary>兜底：既不知道原因，也不该把技术细节倒给用户。</summary>
    public static string SomethingWentWrong => Copy.Get(CopyKeys.ErrorClientUnknown);

    /// <summary>网络层失败（连不上、超时、DNS）统一说这句 —— 不暴露我方地址。</summary>
    public static string CannotReachServer => Copy.Get(CopyKeys.ErrorClientNetworkError);

    /// <summary>错误码对应的文案 key；未知错误码回落到 <c>error.client.unknown</c>。</summary>
    public static string KeyFor(string? errorCode) =>
        ErrorCopyKeys.ForErrorCode(errorCode) ?? CopyKeys.ErrorClientUnknown;

    /// <summary>把一次 API 失败翻译成一句可展示的文案。</summary>
    public static string Describe(EliCloudApiException ex) =>
        DescribeCode(ex.Error, ex.RetryAfter);

    /// <summary>按错误码与 Retry-After 生成文案（供单元测试直接调用）。</summary>
    public static string DescribeCode(string? errorCode, TimeSpan? retryAfter) =>
        Copy.Format(
            KeyFor(errorCode),
            ("retryAfterSeconds", (int)(retryAfter?.TotalSeconds ?? 60)));
}
