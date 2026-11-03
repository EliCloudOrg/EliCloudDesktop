//
//  Fixtures.swift
//  EliCloudSDKTests
//
//  契约 fixtures（contracts/fixtures）回放支撑：
//    - 直接从仓库的 contracts/fixtures 读取，不复制副本（避免两份真相）；
//    - 把 fixture 的 response 变成一个 ClientTransport 的应答；
//    - 断言用 fixture 里的 expected（错误码 / Retry-After）。
//

import Foundation
@testable import EliCloudSDK

/// 一个契约 fixture。
struct Fixture {
    let name: String
    let operationID: String
    let requestMethod: String
    let requestPath: String
    let status: Int
    let headers: [String: String]
    let body: Data
    let expectedErrorCode: String?
    let expectedRetryAfter: TimeInterval?
    let expectedNonJSON: Bool

    /// `contracts/fixtures` 的绝对路径。
    ///
    /// 用 `#filePath` 定位仓库根：`<repo>/sdks/swift/Tests/EliCloudSDKTests/Support/Fixtures.swift`。
    /// `swift test` 在 CI（macOS/Linux）和本地都是同一个相对布局，所以不需要把 fixture 复制进 target。
    static let fixturesRoot: URL = {
        var url = URL(fileURLWithPath: #filePath)
        // Fixtures.swift → Support → EliCloudSDKTests → Tests → swift → sdks → 仓库根
        for _ in 0..<6 { url.deleteLastPathComponent() }
        return url.appendingPathComponent("contracts/fixtures", isDirectory: true)
    }()

    static func load(_ relativePath: String) throws -> Fixture {
        let url = fixturesRoot.appendingPathComponent(relativePath)
        let data = try Data(contentsOf: url)
        guard let root = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw FixtureError.invalidFormat(relativePath)
        }
        guard let request = root["request"] as? [String: Any],
            let response = root["response"] as? [String: Any],
            let status = response["status"] as? Int
        else {
            throw FixtureError.invalidFormat(relativePath)
        }
        let expected = root["expected"] as? [String: Any] ?? [:]

        var body = Data()
        if let json = response["json"] {
            body = try JSONSerialization.data(withJSONObject: json, options: [.sortedKeys])
        } else if let rawFile = response["rawFile"] as? String {
            body = try Data(contentsOf: url.deletingLastPathComponent().appendingPathComponent(rawFile))
        }

        let headers = (response["headers"] as? [String: String]) ?? [:]
        return Fixture(
            name: (root["name"] as? String) ?? relativePath,
            operationID: (root["operationId"] as? String) ?? "",
            requestMethod: (request["method"] as? String) ?? "GET",
            requestPath: (request["path"] as? String) ?? "/",
            status: status,
            headers: headers,
            body: body,
            expectedErrorCode: expected["errorCode"] as? String,
            expectedRetryAfter: (expected["retryAfterSeconds"] as? NSNumber)?.doubleValue,
            expectedNonJSON: (expected["nonJson"] as? Bool) ?? false
        )
    }

    enum FixtureError: Error {
        case invalidFormat(String)
    }
}
