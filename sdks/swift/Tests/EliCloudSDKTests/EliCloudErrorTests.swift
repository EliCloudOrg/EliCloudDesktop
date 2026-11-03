import XCTest
@testable import EliCloudSDK

final class EliCloudErrorTests: XCTestCase {

    func testEnvelopeParsedFromJSON() {
        let body = Data(#"{"error":"invalid_grant","error_description":"账号或密码错误"}"#.utf8)
        let error = EliCloudError.fromResponse(status: 401, headerFields: [:], body: body)
        XCTAssertEqual(error.kind, .http)
        XCTAssertEqual(error.status, 401)
        XCTAssertEqual(error.code, "invalid_grant")
        XCTAssertEqual(error.description, "账号或密码错误")
        XCTAssertTrue(error.isUnauthorized)
        XCTAssertFalse(error.isRetryable)
    }

    func testNonJSONBodyOnlyKeepsStatus() {
        let body = Data("<html><body>bad gateway</body></html>".utf8)
        let error = EliCloudError.fromResponse(status: 502, headerFields: ["Content-Type": "text/html"], body: body)
        XCTAssertEqual(error.status, 502)
        XCTAssertNil(error.code)
        XCTAssertNil(error.description)
        XCTAssertTrue(error.isServiceUnavailable)
    }

    func testRetryAfterSeconds() {
        let body = Data(#"{"error":"too_many_requests","error_description":"稍后再试"}"#.utf8)
        let error = EliCloudError.fromResponse(
            status: 429,
            headerFields: ["Retry-After": "90", "Content-Type": "application/json"],
            body: body
        )
        XCTAssertEqual(error.retryAfter, 90)
        XCTAssertTrue(error.isRateLimited)
        XCTAssertTrue(error.isRetryable)
    }

    func testRetryAfterHTTPDate() {
        let now = ISO8601DateFormatter().date(from: "2026-10-21T07:28:00Z")!
        let later = ISO8601DateFormatter().date(from: "2026-10-21T07:29:00Z")!
        let parsed = RetryAfter.parse(rawValue: "Wed, 21 Oct 2026 07:29:00 GMT", now: now)
        XCTAssertNotNil(parsed)
        XCTAssertEqual(parsed!, 60, accuracy: 1)
        _ = later
    }

    func testRetryAfterHeaderIsCaseInsensitive() {
        XCTAssertEqual(RetryAfter.parse(headerFields: ["retry-after": "15"]), 15)
        XCTAssertNil(RetryAfter.parse(headerFields: ["Retry-After": "not-a-date"]))
    }

    func testTransportErrorMapping() {
        let error = EliCloudError.fromTransport(URLError(.cannotConnectToHost))
        XCTAssertEqual(error.kind, .transport)
        XCTAssertTrue(error.isNetworkProblem)
        XCTAssertFalse(error.isCancelled)
    }

    func testCancellationIsNotAnError() {
        XCTAssertTrue(EliCloudError.fromTransport(CancellationError()).isCancelled)
        XCTAssertTrue(EliCloudError.fromTransport(URLError(.cancelled)).isCancelled)
    }
}
