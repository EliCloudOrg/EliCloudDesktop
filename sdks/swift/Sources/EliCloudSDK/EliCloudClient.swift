//
//  EliCloudClient.swift
//  EliCloudSDK
//
//  门面：一个 baseURL + 一个传输层 + 一个内存会话，拆成 Sso / UserInfo / MC 三个客户端。
//  生成的 `Client`（Generated/Client.swift）只负责按契约编码/解码与状态分派，
//  行为（超时、鉴权头、错误信封、会话）都在这里和 Transport/ 下。
//

import Foundation
import OpenAPIRuntime

#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

/// EliCloud 客户端 SDK 的入口。
///
/// ```swift
/// let client = EliCloudClient(baseURL: url, userAgent: "EliCloud/1.0 (macos)")
/// try await client.sso.login(username: "alice", password: "S3cret!pass")
/// let me = try await client.sso.userInfo()
/// ```
public struct EliCloudClient: Sendable {

    public let baseURL: URL
    public let userAgent: String
    public let sessionStore: SessionStore
    /// 底层生成的客户端（需要契约里未封装的操作时直接用）。
    public let generatedClient: Client
    public let sso: SsoClient
    public let userInfo: UserInfoClient
    public let mc: McClient

    /// 生产装配：URLSession 传输 + 契约超时 + 从会话取 Bearer。
    public init(
        baseURL: URL,
        userAgent: String,
        timeouts: RequestTimeouts = .contract,
        urlSession: URLSession = .shared,
        sessionStore: SessionStore = SessionStore()
    ) {
        let transport = EliCloudTransport(
            userAgent: userAgent,
            timeouts: timeouts,
            session: urlSession
        )
        self.init(
            baseURL: baseURL,
            userAgent: userAgent,
            transport: transport,
            sessionStore: sessionStore
        )
    }

    /// 测试 / 自定义传输装配（契约测试用 `StubTransport` 回放 fixtures）。
    ///
    /// 中间件与生产装配一致：错误信封映射 + 按契约注入 Bearer —— 换传输层不换行为。
    public init(
        baseURL: URL,
        userAgent: String,
        transport: any ClientTransport,
        sessionStore: SessionStore = SessionStore()
    ) {
        let tokenProvider: EliCloudAuthorizationMiddleware.TokenProvider = {
            await sessionStore.accessToken()
        }
        let client = Client(
            serverURL: baseURL,
            transport: transport,
            middlewares: [
                EliCloudErrorMiddleware(),
                EliCloudAuthorizationMiddleware(tokenProvider: tokenProvider),
            ]
        )
        self.baseURL = baseURL
        self.userAgent = userAgent
        self.sessionStore = sessionStore
        self.generatedClient = client
        self.sso = SsoClient(client: client, sessionStore: sessionStore)
        self.userInfo = UserInfoClient(client: client)
        self.mc = McClient(client: client)
    }
}

// MARK: - 共享工具

enum EliCloudEndpoint {

    /// 契约里没有、但确实收到了的响应（例如方法/状态码不匹配）。
    /// 不把响应体塞进 description：里面可能有令牌。
    static func unexpected(_ operationID: String, status: Int? = nil) -> EliCloudError {
        EliCloudError(
            kind: .http,
            status: status,
            code: "unexpected_response",
            description: "unexpected response for \(operationID)"
        )
    }
}
