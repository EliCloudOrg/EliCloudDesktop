//
//  SsoClient.swift
//  EliCloudSDK
//
//  契约 Auth 段：注册（202 + 轮询）、登录、刷新、UserInfo、退出。
//  状态机见 contracts/auth-flow.md；这里只负责把它写成可取消的 async 方法。
//

import Foundation
import OpenAPIRuntime

/// 账号中心（`/auth`）。
public struct SsoClient: Sendable {

    private let client: Client
    private let sessionStore: SessionStore
    private let refresher: RefreshCoordinator

    init(client: Client, sessionStore: SessionStore) {
        self.client = client
        self.sessionStore = sessionStore
        self.refresher = RefreshCoordinator()
    }

    // MARK: 登录

    /// 密码直连登录。成功后令牌进内存会话（不发 `/auth/userinfo`）。
    @discardableResult
    public func login(username: String, password: String) async throws -> Components.Schemas.LoginResult {
        let input = Operations.Login.Input(
            body: .json(.init(username: username, password: password))
        )
        let output = try await client.login(input)
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("login")
        }
        let result = try ok.body.json
        await adopt(result)
        return result
    }

    /// 登录 + 拉 `/auth/userinfo`。
    ///
    /// 契约 §5：登录后**必须**拉一次 UserInfo；但 UserInfo 失败**不**踢回登录页
    /// （令牌已经到手），所以这里吞掉身份失败只保留令牌会话。
    @discardableResult
    public func loginAndLoadIdentity(
        username: String,
        password: String
    ) async throws -> Components.Schemas.LoginResult {
        let result = try await login(username: username, password: password)
        _ = try? await userInfo()
        return result
    }

    // MARK: 注册（202 + 轮询）

    /// 发起注册：只发验证邮件，不创建账号。
    public func startRegistration(
        username: String,
        password: String,
        email: String
    ) async throws -> Components.Schemas.RegistrationPending {
        let input = Operations.RegisterStart.Input(
            body: .json(.init(username: username, password: password, email: email))
        )
        let output = try await client.registerStart(input)
        guard case .accepted(let accepted) = output else {
            throw EliCloudEndpoint.unexpected("registerStart")
        }
        return try accepted.body.json
    }

    /// 查询待验证注册的状态（轮询单次）。
    public func registrationStatus(
        registrationID: String
    ) async throws -> Components.Schemas.RegistrationStatus {
        let input = Operations.GetRegistrationStatus.Input(
            body: .json(.init(registrationId: registrationID))
        )
        let output = try await client.getRegistrationStatus(input)
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("getRegistrationStatus")
        }
        return try ok.body.json
    }

    /// 重发验证邮件（受 `resend_after` 冷却限制；冷却中服务端返回 429 + `Retry-After`）。
    public func resendRegistrationEmail(
        registrationID: String
    ) async throws -> Components.Schemas.RegistrationPending {
        let input = Operations.ResendRegistrationEmail.Input(
            body: .json(.init(registrationId: registrationID))
        )
        let output = try await client.resendRegistrationEmail(input)
        guard case .accepted(let accepted) = output else {
            throw EliCloudEndpoint.unexpected("resendRegistrationEmail")
        }
        return try accepted.body.json
    }

    /// 取消待验证注册（幂等；已验证返回 409 `already_verified`）。
    public func cancelRegistration(registrationID: String) async throws {
        let input = Operations.CancelRegistration.Input(
            body: .json(.init(registrationId: registrationID))
        )
        let output = try await client.cancelRegistration(input)
        guard case .noContent = output else {
            throw EliCloudEndpoint.unexpected("cancelRegistration")
        }
    }

    /// 轮询注册状态直到终态（`verified` / `expired` / `failed`）。
    ///
    /// 退避与契约 §5 一致：2s 起、每次 ×2、上限 10s；任务取消立即退出。
    public func pollRegistration(
        registrationID: String,
        options: RegistrationPollingOptions = .init()
    ) async throws -> Components.Schemas.RegistrationStatus {
        let deadline = Date().addingTimeInterval(options.deadline)
        var interval = max(0.1, options.initialInterval)
        while true {
            let status = try await registrationStatus(registrationID: registrationID)
            switch status.status {
            case .verified, .expired, .failed:
                return status
            case .pending:
                guard Date() < deadline else {
                    throw EliCloudError(
                        kind: .http,
                        code: "registration_timeout",
                        description: "registration polling deadline exceeded"
                    )
                }
                try await Task.sleep(for: .seconds(interval))
                interval = min(interval * 2, options.maximumInterval)
            }
        }
    }

    // MARK: UserInfo / 刷新 / 退出

    /// 读取身份（权威来源），并把 `sub`/用户名/邮箱/scope 合并进会话。
    @discardableResult
    public func userInfo() async throws -> Components.Schemas.UserInfoResponse {
        let output = try await client.getUserInfo(.init())
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("getUserInfo")
        }
        let info = try ok.body.json
        await sessionStore.mergeIdentity(
            subject: info.sub,
            username: info.preferredUsername,
            email: info.email,
            emailVerified: info.emailVerified,
            scope: info.scope.map(ScopeParser.parse)
        )
        return info
    }

    /// 私有刷新（轮换 refresh token）。
    ///
    /// 契约 §5：重放旧令牌会撤销整链，所以**不能并发刷新**；这里用 actor 串行化，
    /// 并发调用会共享同一个在途结果。
    @discardableResult
    public func refresh() async throws -> Components.Schemas.RefreshResult {
        guard let token = await sessionStore.current?.refreshToken, !token.isEmpty else {
            throw EliCloudError(
                kind: .http,
                status: 401,
                code: "invalid_grant",
                description: "no refresh token in session"
            )
        }
        return try await refresher.perform { [client, sessionStore] in
            let output = try await client.refreshToken(.init(body: .json(.init(refreshToken: token))))
            guard case .ok(let ok) = output else {
                throw EliCloudEndpoint.unexpected("refreshToken")
            }
            let result = try ok.body.json
            if var current = await sessionStore.current {
                current.accessToken = result.accessToken
                current.refreshToken = result.refreshToken
                current.expiresAt = Date().addingTimeInterval(TimeInterval(result.expiresIn))
                current.issuedAt = Date()
                await sessionStore.adopt(current)
            }
            return result
        }
    }

    /// 退出登录：清空内存会话（不调用服务端，契约的 `/auth/logout` 是 RP-initiated 跳转）。
    public func signOut() async {
        await sessionStore.clear()
    }

    private func adopt(_ result: Components.Schemas.LoginResult) async {
        let state = SessionState(
            accessToken: result.accessToken,
            refreshToken: result.refreshToken,
            scope: ScopeParser.parse(result.scope),
            expiresAt: Date().addingTimeInterval(TimeInterval(result.expiresIn)),
            issuedAt: Date()
        )
        await sessionStore.adopt(state)
    }
}

/// 注册轮询参数（契约 §5 的退避）。
public struct RegistrationPollingOptions: Sendable, Equatable {
    /// 首次轮询间隔（秒）。
    public var initialInterval: TimeInterval
    /// 最大轮询间隔（秒）。
    public var maximumInterval: TimeInterval
    /// 总时限（秒）；超过即抛 `registration_timeout`。
    public var deadline: TimeInterval

    public init(
        initialInterval: TimeInterval = 2,
        maximumInterval: TimeInterval = 10,
        deadline: TimeInterval = 15 * 60
    ) {
        self.initialInterval = initialInterval
        self.maximumInterval = maximumInterval
        self.deadline = deadline
    }
}

/// `scope` 是空格分隔的字符串。
enum ScopeParser {
    static func parse(_ raw: String?) -> [String] {
        guard let raw else { return [] }
        return raw.split(whereSeparator: { $0 == " " || $0 == "\t" }).map(String.init)
    }
}

/// 串行化 refresh：并发调用共享同一个在途任务，避免重放旧 refresh token。
actor RefreshCoordinator {
    private var inFlight: Task<Components.Schemas.RefreshResult, Error>?

    func perform(
        _ operation: @Sendable @escaping () async throws -> Components.Schemas.RefreshResult
    ) async throws -> Components.Schemas.RefreshResult {
        if let inFlight {
            return try await inFlight.value
        }
        let task = Task { try await operation() }
        inFlight = task
        defer { inFlight = nil }
        return try await task.value
    }
}
