//
//  StubTransport.swift
//  EliCloudSDKTests
//
//  用契约 fixture 回放来驱动真实生成的 Client（而不是绕开它写假数据）：
//  fixture 的 response 变成 HTTP 应答，编码/解码仍走 OpenAPIRuntime 的转换器。
//

import Foundation
import HTTPTypes
import OpenAPIRuntime
@testable import EliCloudSDK

/// 记录请求、按 operationId 返回预置应答的传输层。
final class StubTransport: ClientTransport, @unchecked Sendable {

    struct Reply {
        var status: Int
        var headers: [String: String]
        var body: Data

        init(status: Int, headers: [String: String] = ["Content-Type": "application/json"], body: Data = Data()) {
            self.status = status
            self.headers = headers
            self.body = body
        }

        init(fixture: Fixture) {
            self.init(status: fixture.status, headers: fixture.headers, body: fixture.body)
        }
    }

    struct RecordedRequest: Sendable {
        var operationID: String
        var method: String
        /// 生成器把 path 与 query 一起放进 `HTTPRequest.path`。
        var path: String
        var headers: [String: String]
        var body: Data
    }

    private let lock = NSRecursiveLock()
    /// 每个 operationId 一个应答队列：同一端点被调用多次时按顺序返回（注册轮询要用）。
    private var replies: [String: [Reply]]
    private var recorded: [RecordedRequest] = []
    private var fallback: Reply?

    init(replies: [String: Reply] = [:], fallback: Reply? = nil) {
        self.replies = replies.mapValues { [$0] }
        self.fallback = fallback
    }

    /// 按顺序追加应答（同一 operationId 的多次调用）。
    func enqueue(_ reply: Reply, for operationID: String) {
        lock.withLock {
            replies[operationID, default: []].append(reply)
        }
    }

    /// 当前收到的所有请求（按到达顺序）。
    var requests: [RecordedRequest] {
        lock.lock()
        defer { lock.unlock() }
        return recorded
    }

    func reply(for operationID: String) -> Reply? {
        lock.withLock {
            if var queue = replies[operationID], !queue.isEmpty {
                let next = queue.removeFirst()
                replies[operationID] = queue
                return next
            }
            return fallback
        }
    }

    func send(
        _ request: HTTPRequest,
        body: HTTPBody?,
        baseURL: URL,
        operationID: String
    ) async throws -> (HTTPResponse, HTTPBody?) {
        let payload = try await EliCloudTransport.collect(body)
        var headers: [String: String] = [:]
        for field in request.headerFields {
            headers[field.name.rawName] = field.value
        }
        let record = RecordedRequest(
            operationID: operationID,
            method: request.method.rawValue,
            path: request.path ?? "",
            headers: headers,
            body: payload
        )
        guard let reply = reply(for: operationID) else {
            throw EliCloudError(kind: .transport, underlying: "no stubbed reply for \(operationID)")
        }
        lock.withLock {
            recorded.append(record)
        }

        var response = HTTPResponse(status: .init(code: reply.status))
        for (name, value) in reply.headers {
            guard let fieldName = HTTPField.Name(name) else { continue }
            response.headerFields.append(HTTPField(name: fieldName, value: value))
        }
        return (response, reply.body.isEmpty ? nil : HTTPBody(reply.body))
    }
}
