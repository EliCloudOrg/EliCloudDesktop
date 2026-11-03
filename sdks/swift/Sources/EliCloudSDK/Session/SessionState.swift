//
//  SessionState.swift
//  EliCloudSDK
//
//  内存会话（契约 §5：密码与 refresh token 只在内存，不落盘）。
//  字段与 Stage 4 文档 §4.2 一致：access/refresh/scope/sub/username/email/emailVerified/expiresAt。
//

import Foundation

/// 一次登录后的会话快照。
///
/// - 令牌来自 `/auth/login`（或 `/auth/refresh`）；
/// - 身份（`subject` 及以下）以 `/auth/userinfo` 为准，不用登录响应里的便利字段。
public struct SessionState: Sendable, Equatable {

    /// access token（Bearer）。
    public var accessToken: String
    /// refresh token（轮换式；重放旧令牌会撤销整链，所以只能串行刷新）。
    public var refreshToken: String?
    /// 空格分隔 scope 解析后的集合。
    public var scope: [String]
    /// JWT 的 `sub`。
    public var subject: String?
    /// UserInfo 的 `preferred_username`。
    public var username: String?
    public var email: String?
    public var emailVerified: Bool?
    /// access token 过期时刻。
    public var expiresAt: Date?
    /// 本次会话建立时刻。
    public var issuedAt: Date

    public init(
        accessToken: String,
        refreshToken: String? = nil,
        scope: [String] = [],
        subject: String? = nil,
        username: String? = nil,
        email: String? = nil,
        emailVerified: Bool? = nil,
        expiresAt: Date? = nil,
        issuedAt: Date = Date()
    ) {
        self.accessToken = accessToken
        self.refreshToken = refreshToken
        self.scope = scope
        self.subject = subject
        self.username = username
        self.email = email
        self.emailVerified = emailVerified
        self.expiresAt = expiresAt
        self.issuedAt = issuedAt
    }

    /// 是否已过期（`leeway` 秒内视为仍有效，留给时钟偏差）。
    public func isExpired(now: Date = Date(), leeway: TimeInterval = 0) -> Bool {
        guard let expiresAt else { return false }
        return now.addingTimeInterval(leeway) >= expiresAt
    }

    public func hasScope(_ scope: String) -> Bool {
        self.scope.contains(scope)
    }

    /// 令牌是否还能用于请求：过期即视为不可用（本阶段默认不自动刷新）。
    public func isUsable(now: Date = Date()) -> Bool {
        !accessToken.isEmpty && !isExpired(now: now)
    }
}
