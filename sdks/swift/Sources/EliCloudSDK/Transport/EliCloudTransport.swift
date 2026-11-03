//
//  EliCloudTransport.swift
//  EliCloudSDK
//
//  契约 §4/§6 的“行为层”，手写在这里（生成的 Client 只负责编码/解码与状态分派）：
//    1. 超时按 operationId 分类（20/8/30/60s），官方 URLSessionTransport 表达不了；
//    2. 统一注入 `User-Agent` 与 `Authorization: Bearer`（`security: []` 的端点不加）；
//    3. 非 2xx/3xx 直接抛 `EliCloudError`（解析 `{error,error_description}` 与 `Retry-After`）；
//       例外：`mcHealth` 的 503 是“降级但有 body”，必须交给上层解析；
//    4. 不打印 URL / query / 令牌：日志只有 operationId 与状态码。
//

import Foundation
import HTTPTypes
import OpenAPIRuntime

#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

/// 走 URLSession 的 `ClientTransport` 实现。
///
/// 用法：
/// ```swift
/// let transport = EliCloudTransport(userAgent: "EliCloud/1.0 (macos)")
/// let client = Client(serverURL: baseURL, transport: transport)
/// ```
public struct EliCloudTransport: ClientTransport {

    /// 提供当前 access token；没有会话时返回 nil。
    public typealias TokenProvider = @Sendable () async -> String?

    /// 契约中 `security: []` 的 operationId —— 这些端点不携带 Authorization。
    ///
    /// 与 `contracts/openapi.yaml` 一一对应；新增匿名端点时同步改这里（契约测试会兜底）。
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

    public var userAgent: String
    public var timeouts: RequestTimeouts
    public var tokenProvider: TokenProvider?
    public var session: URLSession

    public init(
        userAgent: String,
        timeouts: RequestTimeouts = .contract,
        session: URLSession = .shared,
        tokenProvider: TokenProvider? = nil
    ) {
        self.userAgent = userAgent
        self.timeouts = timeouts
        self.session = session
        self.tokenProvider = tokenProvider
    }

    /// 该 operationId 是否需要 Bearer 令牌（未知端点按“需要”处理，宁可多带）。
    public static func requiresAuthorization(operationID: String) -> Bool {
        !anonymousOperations.contains(operationID)
    }

    /// `mcHealth` 的 503 是健康快照（降级），不是传输错误。
    public static func passesThroughErrorStatus(operationID: String, status: Int) -> Bool {
        operationID == "mcHealth" && status == 503
    }

    public func send(
        _ request: HTTPRequest,
        body: HTTPBody?,
        baseURL: URL,
        operationID: String
    ) async throws -> (HTTPResponse, HTTPBody?) {
        do {
            SDKLog.request(operationID)
            let payload = try await Self.collect(body)
            var urlRequest = try Self.makeURLRequest(request, baseURL: baseURL, operationID: operationID)
            urlRequest.timeoutInterval = timeouts.timeout(for: operationID)
            if !payload.isEmpty {
                urlRequest.httpBody = payload
            }
            if urlRequest.value(forHTTPHeaderField: HTTPField.Name.userAgent.rawName) == nil {
                urlRequest.setValue(userAgent, forHTTPHeaderField: HTTPField.Name.userAgent.rawName)
            }
            if Self.requiresAuthorization(operationID: operationID),
                let token = await tokenProvider?(),
                !token.isEmpty
            {
                urlRequest.setValue("Bearer \(token)", forHTTPHeaderField: HTTPField.Name.authorization.rawName)
            }

            let (data, urlResponse) = try await perform(urlRequest)
            let response = try Self.makeHTTPResponse(urlResponse)
            let status = response.status.code

            guard status < 400 || Self.passesThroughErrorStatus(operationID: operationID, status: status) else {
                let error = EliCloudError.fromResponse(
                    status: status,
                    headerFields: Self.headerDictionary(response),
                    body: data
                )
                SDKLog.failure(operationID, status: status, code: error.code)
                throw error
            }

            SDKLog.response(operationID, status: status)
            return (response, data.isEmpty ? nil : HTTPBody(data))
        } catch {
            let mapped = EliCloudError.fromTransport(error)
            if mapped.kind != .cancelled, !(error is EliCloudError) {
                SDKLog.failure(operationID, status: mapped.status, code: mapped.code)
            }
            throw mapped
        }
    }
}

// MARK: - 请求 / 响应转换

extension EliCloudTransport {

    static func collect(_ body: HTTPBody?) async throws -> Data {
        guard let body else { return Data() }
        var data = Data()
        if case .known(let length) = body.length, length > 0, length < 64 * 1024 * 1024 {
            data.reserveCapacity(Int(length))
        }
        for try await chunk in body {
            data.append(contentsOf: chunk)
        }
        return data
    }

    /// 把 `HTTPRequest` + baseURL 组装成 `URLRequest`（path 已由生成器按契约填好）。
    static func makeURLRequest(_ request: HTTPRequest, baseURL: URL, operationID: String) throws -> URLRequest {
        guard var baseComponents = URLComponents(string: baseURL.absoluteString),
            let requestComponents = URLComponents(string: request.path ?? "")
        else {
            throw EliCloudError(
                kind: .transport,
                underlying: "invalid request URL for \(operationID)"
            )
        }
        baseComponents.percentEncodedPath += requestComponents.percentEncodedPath
        baseComponents.percentEncodedQuery = requestComponents.percentEncodedQuery
        guard let url = baseComponents.url else {
            throw EliCloudError(kind: .transport, underlying: "invalid request URL for \(operationID)")
        }
        var urlRequest = URLRequest(url: url)
        urlRequest.httpMethod = request.method.rawValue
        for field in request.headerFields {
            urlRequest.setValue(field.value, forHTTPHeaderField: field.name.rawName)
        }
        return urlRequest
    }

    static func makeHTTPResponse(_ urlResponse: URLResponse) throws -> HTTPResponse {
        guard let httpURLResponse = urlResponse as? HTTPURLResponse else {
            throw EliCloudError(kind: .transport, underlying: "non-HTTP response")
        }
        guard (0...999).contains(httpURLResponse.statusCode) else {
            throw EliCloudError(kind: .transport, underlying: "invalid status code \(httpURLResponse.statusCode)")
        }
        var response = HTTPResponse(status: .init(code: httpURLResponse.statusCode))
        if let fields = httpURLResponse.allHeaderFields as? [String: String] {
            for (name, value) in fields {
                guard let fieldName = HTTPField.Name(name) else { continue }
                response.headerFields.append(HTTPField(name: fieldName, value: value))
            }
        }
        return response
    }

    static func headerDictionary(_ response: HTTPResponse) -> [String: String] {
        var headers: [String: String] = [:]
        for field in response.headerFields {
            headers[field.name.rawName] = field.value
        }
        return headers
    }

    private func perform(_ request: URLRequest) async throws -> (Data, URLResponse) {
        let box = TaskBox()
        return try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<(Data, URLResponse), Error>) in
                let task = session.dataTask(with: request) { data, response, error in
                    if let error {
                        continuation.resume(throwing: error)
                        return
                    }
                    guard let response else {
                        continuation.resume(
                            throwing: EliCloudError(kind: .transport, underlying: "no response")
                        )
                        return
                    }
                    continuation.resume(returning: (data ?? Data(), response))
                }
                box.set(task)
                task.resume()
            }
        } onCancel: {
            box.cancel()
        }
    }
}

/// 持有 URLSession task 以便取消请求（`URLSession` 的回调与取消来自不同域）。
private final class TaskBox: @unchecked Sendable {

    private let lock = NSLock()
    private var task: URLSessionTask?

    func set(_ task: URLSessionTask) {
        lock.lock()
        defer { lock.unlock() }
        if let existing = self.task, existing.state == .cancelling {
            task.cancel()
        }
        self.task = task
    }

    func cancel() {
        lock.lock()
        defer { lock.unlock() }
        task?.cancel()
    }
}
