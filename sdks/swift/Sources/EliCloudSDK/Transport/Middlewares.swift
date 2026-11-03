//
//  Middlewares.swift
//  EliCloudSDK
//
//  两个官方 `ClientMiddleware`，夹在生成的 Client 与传输层之间：
//    1. EliCloudAuthorizationMiddleware —— 按契约 `security` 注入 Bearer；
//    2. EliCloudErrorMiddleware —— 非 2xx 统一抛 `EliCloudError`
//       （解析 `{error,error_description}` 与 `Retry-After`）。
//
//  放在中间件而不是传输层的理由：契约回放测试注入的是 stub transport，
//  只有中间件才会被真实执行，错误映射与鉴权头才能被测到。
//

import Foundation
import HTTPTypes
import OpenAPIRuntime

/// 给需要认证的操作加 `Authorization: Bearer <token>`。
public struct EliCloudAuthorizationMiddleware: ClientMiddleware {

    public typealias TokenProvider = @Sendable () async -> String?

    /// 契约中 `security: []` 的 operationId —— 这些端点不携带 Authorization。
    ///
    /// 与 `contracts/openapi.yaml` 一一对应；新增匿名端点时同步改这里
    /// （`TransportPolicyTests` 会兜底）。
    public static let anonymousOperations: Set<String> = [
        "registerStart",
        "getRegistrationStatus",
        "resendRegistrationEmail",
        "cancelRegistration",
        "login",
        "refreshToken",
        "getOidcDiscovery",
        "getJwks",
        "authorize",
        "token",
        "deviceAuthorization",
        "logout",
        "mcHealth",
        "listCoreServices",
    ]

    /// 未知端点按“需要令牌”处理（宁可多带，不要漏带）。
    public static func requiresAuthorization(operationID: String) -> Bool {
        !anonymousOperations.contains(operationID)
    }

    private let tokenProvider: TokenProvider?

    public init(tokenProvider: TokenProvider?) {
        self.tokenProvider = tokenProvider
    }

    public func intercept(
        _ request: HTTPRequest,
        body: HTTPBody?,
        baseURL: URL,
        operationID: String,
        next: @Sendable (HTTPRequest, HTTPBody?, URL) async throws -> (HTTPResponse, HTTPBody?)
    ) async throws -> (HTTPResponse, HTTPBody?) {
        var request = request
        if Self.requiresAuthorization(operationID: operationID),
            request.headerFields[.authorization] == nil,
            let token = await tokenProvider?(),
            !token.isEmpty
        {
            request.headerFields[.authorization] = "Bearer \(token)"
        }
        return try await next(request, body, baseURL)
    }
}

/// 把非 2xx 响应统一变成 `EliCloudError`。
///
/// 例外：`mcHealth` 的 503 是「降级但有完整 body」，必须原样放行给上层解析。
public struct EliCloudErrorMiddleware: ClientMiddleware {

    public init() {}

    /// `mcHealth` 的 503 是健康快照（降级），不是传输错误。
    public static func passesThroughErrorStatus(operationID: String, status: Int) -> Bool {
        operationID == "mcHealth" && status == 503
    }

    public func intercept(
        _ request: HTTPRequest,
        body: HTTPBody?,
        baseURL: URL,
        operationID: String,
        next: @Sendable (HTTPRequest, HTTPBody?, URL) async throws -> (HTTPResponse, HTTPBody?)
    ) async throws -> (HTTPResponse, HTTPBody?) {
        let (response, responseBody) = try await next(request, body, baseURL)
        let status = response.status.code
        guard status >= 400, !Self.passesThroughErrorStatus(operationID: operationID, status: status) else {
            return (response, responseBody)
        }
        do {
            let payload = try await EliCloudTransport.collect(responseBody)
            let error = EliCloudError.fromResponse(
                status: status,
                headerFields: EliCloudTransport.headerDictionary(response),
                body: payload
            )
            SDKLog.failure(operationID, status: status, code: error.code)
            throw error
        } catch let error as EliCloudError {
            throw error
        } catch {
            throw EliCloudError.fromTransport(error)
        }
    }
}
