//
//  EliCloudTransport.swift
//  EliCloudSDK
//
//  契约 §6 的传输层：URLSession + 按 operationId 分类的超时 + User-Agent。
//  **不含**鉴权与错误映射 —— 那两件事在 Middlewares.swift 里用官方
//  `ClientMiddleware` 实现，这样契约回放测试（注入 stub transport）也能覆盖
//  “非 2xx → EliCloudError”的真实路径。
//

import Foundation
import HTTPTypes
import OpenAPIRuntime

#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

/// 走 URLSession 的 `ClientTransport` 实现。
public struct EliCloudTransport: ClientTransport {

    public var userAgent: String
    public var timeouts: RequestTimeouts
    public var session: URLSession

    public init(
        userAgent: String,
        timeouts: RequestTimeouts = .contract,
        session: URLSession = .shared
    ) {
        self.userAgent = userAgent
        self.timeouts = timeouts
        self.session = session
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

            let (data, urlResponse) = try await perform(urlRequest)
            let response = try Self.makeHTTPResponse(urlResponse)
            SDKLog.response(operationID, status: response.status.code)
            return (response, data.isEmpty ? nil : HTTPBody(data))
        } catch {
            let mapped = EliCloudError.fromTransport(error)
            if mapped.kind != .cancelled, !(error is EliCloudError) {
                SDKLog.failure(operationID, status: nil, code: nil)
            }
            throw mapped
        }
    }
}

// MARK: - 请求 / 响应转换

extension EliCloudTransport {

    /// 把 `HTTPBody` 读成 `Data`（请求体与测试都要用）。
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

    /// 把 `HTTPRequest` + baseURL 组装成 `URLRequest`（path 由生成器按契约填好）。
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

    /// `HTTPFields` → `[String: String]`（给 `Retry-After` 之类的解析用）。
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
        self.task = task
    }

    func cancel() {
        lock.lock()
        defer { lock.unlock() }
        task?.cancel()
    }
}
