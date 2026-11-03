//
//  EliCloudError.swift
//  EliCloudSDK
//
//  契约 §4 的错误模型：
//    - 传输错误信封 `{"error": "...", "error_description": "..."}` → code / description
//    - 非 JSON 错误体 → 只保留 HTTP 状态
//    - `Retry-After`（秒数或 HTTP-date）→ retryAfter（秒）
//
//  这里 **只做结构化**：不带任何用户可读文案。文案由各端按 `contracts/errors.json`
//  的 `copyKey` 从 `copy/zh-CN.json` 取，SDK 里出现中文就会和契约分叉。
//

import Foundation
import OpenAPIRuntime

#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

/// SDK 抛出的唯一错误类型。
public struct EliCloudError: Error, Sendable, Equatable {

    /// 错误来源。
    public enum Kind: String, Sendable, Equatable {
        /// 服务端返回了业务错误（有 HTTP 状态码）。
        case http
        /// 传输层失败：没网、DNS、TLS、超时、连接被拒。
        case transport
        /// 响应体不符合契约（JSON 坏了或缺必填字段）。
        case decoding
        /// 调用方取消了请求。
        case cancelled
    }

    public var kind: Kind
    /// HTTP 状态码；`kind == .transport` 时为 nil。
    public var status: Int?
    /// 契约错误码，如 `invalid_grant` / `rate_limited`；解析不出来时 nil。
    public var code: String?
    /// 服务端给的说明（`error_description`），只用于诊断，不能当分支条件。
    public var description: String?
    /// `Retry-After` 换算出的秒数；没有该头时 nil。
    public var retryAfter: TimeInterval?
    /// 底层错误的文字（URLError 描述等），只用于日志，不展示给用户。
    public var underlying: String?

    public init(
        kind: Kind,
        status: Int? = nil,
        code: String? = nil,
        description: String? = nil,
        retryAfter: TimeInterval? = nil,
        underlying: String? = nil
    ) {
        self.kind = kind
        self.status = status
        self.code = code
        self.description = description
        self.retryAfter = retryAfter
        self.underlying = underlying
    }
}

// MARK: - 分类判断（UI 只按这些判断决定展示/重试，不按文案）

extension EliCloudError {

    /// 令牌缺失、过期、签名不符（401 `invalid_token`）或凭证错误（401 `invalid_grant`）。
    public var isUnauthorized: Bool { status == 401 }

    /// 被限流：可以按 `retryAfter`（默认 30s）退避后重试。
    public var isRateLimited: Bool { status == 429 || code == "rate_limited" || code == "too_many_requests" }

    /// 依赖服务不可用（503），或网关异常（502）。
    public var isServiceUnavailable: Bool { status == 503 || status == 502 }

    /// 取消（不是失败，界面不要提示错误）。
    public var isCancelled: Bool { kind == .cancelled }

    /// 网络层问题（离线/超时），可以提示“检查网络”。
    public var isNetworkProblem: Bool { kind == .transport }

    /// 是否值得原样重试（限流与 5xx）。
    public var isRetryable: Bool { isRateLimited || isServiceUnavailable || status == 500 }
}

// MARK: - 从响应构造

extension EliCloudError {

    /// 从错误响应构造；`body` 不是 JSON 时只保留状态码。
    public static func fromResponse(
        status: Int,
        headerFields: [String: String],
        body: Data,
        underlying: String? = nil
    ) -> EliCloudError {
        let envelope = decodeEnvelope(body)
        return EliCloudError(
            kind: .http,
            status: status,
            code: envelope?.error,
            description: envelope?.description,
            retryAfter: RetryAfter.parse(headerFields: headerFields),
            underlying: underlying
        )
    }

    /// 传输层失败；取消会单独识别，避免被当成错误弹窗。
    public static func fromTransport(_ error: Error) -> EliCloudError {
        if error is CancellationError {
            return EliCloudError(kind: .cancelled)
        }
        if let urlError = error as? URLError {
            if urlError.code == .cancelled {
                return EliCloudError(kind: .cancelled)
            }
            return EliCloudError(kind: .transport, underlying: urlError.localizedDescription)
        }
        if let eliCloudError = error as? EliCloudError {
            return eliCloudError
        }
        return EliCloudError(kind: .transport, underlying: String(describing: error))
    }

    /// 响应体解析失败（契约与实现对不上，属于事故）。
    public static func decoding(_ error: Error, status: Int? = nil) -> EliCloudError {
        EliCloudError(kind: .decoding, status: status, underlying: String(describing: error))
    }

    /// 把任意错误归一成 `EliCloudError`。
    ///
    /// 生成的 `Client` 会把中间件/传输抛出的错误包进 `OpenAPIRuntime.ClientError`
    /// （`underlyingError` 才是原始错误），这里逐层拆开。
    public static func normalized(_ error: Error) -> EliCloudError {
        if let eliCloudError = error as? EliCloudError {
            return eliCloudError
        }
        if let clientError = error as? ClientError {
            return normalized(clientError.underlyingError)
        }
        if error is CancellationError {
            return EliCloudError(kind: .cancelled)
        }
        if let urlError = error as? URLError {
            return fromTransport(urlError)
        }
        // 剩下的基本是生成器内部的编码/解码失败（端点是 `internal`，按文字归到 decoding）。
        return EliCloudError(kind: .decoding, underlying: String(describing: error))
    }

    private struct Envelope: Decodable {
        let error: String?
        let errorDescription: String?

        enum CodingKeys: String, CodingKey {
            case error
            case errorDescription = "error_description"
        }
    }

    /// 只认 `{error, error_description}`；其它形态一律返回 nil（只保留状态码）。
    private static func decodeEnvelope(_ body: Data) -> (error: String?, description: String?)? {
        guard !body.isEmpty else { return nil }
        guard let envelope = try? JSONDecoder().decode(Envelope.self, from: body) else { return nil }
        guard envelope.error != nil || envelope.errorDescription != nil else { return nil }
        return (envelope.error, envelope.errorDescription)
    }
}

// MARK: - Retry-After

/// `Retry-After` 解析：RFC 9110 允许秒数或 HTTP-date。
public enum RetryAfter {

    public static func parse(headerFields: [String: String]) -> TimeInterval? {
        // 头名大小写不敏感。
        guard let raw = headerFields.first(where: { $0.key.caseInsensitiveCompare("Retry-After") == .orderedSame })?.value
        else { return nil }
        return parse(rawValue: raw)
    }

    public static func parse(rawValue raw: String, now: Date = Date()) -> TimeInterval? {
        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        if let seconds = TimeInterval(trimmed) {
            return max(0, seconds)
        }
        guard let date = Self.httpDateFormatter.date(from: trimmed) else { return nil }
        return max(0, date.timeIntervalSince(now))
    }

    /// RFC 9110 IMF-fixdate（`Wed, 21 Oct 2026 07:28:00 GMT`）。
    /// 不用 `ISO8601DateFormatter`：它不接受这种格式。
    private static let httpDateFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "EEE, dd MMM yyyy HH:mm:ss zzz"
        return formatter
    }()
}
