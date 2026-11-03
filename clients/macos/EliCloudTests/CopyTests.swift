import EliCloudSDK
import XCTest
@testable import EliCloud

/// 错误码 → 文案 key → 本地化：界面永不出现错误码。
final class CopyTests: XCTestCase {

    func testErrorsJSONIsBundledAndMapsKnownCodes() {
        XCTAssertNotNil(ErrorCopyKeys.copyKey(for: "invalid_grant"))
        XCTAssertEqual(ErrorCopyKeys.copyKey(for: "invalid_grant"), "error.auth.invalid_grant")
        XCTAssertEqual(ErrorCopyKeys.copyKey(for: "quota_exceeded"), "error.mc.quota_exceeded")
    }

    func testUnknownCodeHasNoMapping() {
        XCTAssertNil(ErrorCopyKeys.copyKey(for: "definitely_not_a_code"))
        XCTAssertNil(ErrorCopyKeys.copyKey(for: nil))
    }

    func testRateLimitedErrorCarriesRetryAfterIntoCopy() {
        let error = EliCloudError(
            kind: .http,
            status: 429,
            code: "too_many_requests",
            retryAfter: 90
        )
        let text = Copy.error(error)
        XCTAssertTrue(text.contains("90"), "文案里应带上重试秒数，实际：\(text)")
        XCTAssertFalse(text.contains("too_many_requests"))
        XCTAssertFalse(text.contains("{retryAfterSeconds}"))
    }

    func testFallbackCopyNeverLeaksCode() {
        let error = EliCloudError(kind: .http, status: 418, code: "teapot_unknown")
        let text = Copy.error(error)
        XCTAssertFalse(text.isEmpty)
        XCTAssertFalse(text.contains("teapot_unknown"))
        XCTAssertFalse(text.hasPrefix("error."))
    }

    func testTransportErrorMapsToNetworkCopy() {
        let error = EliCloudError(kind: .transport)
        let text = Copy.error(error)
        XCTAssertFalse(text.isEmpty)
        XCTAssertFalse(text.hasPrefix("error."))
    }

    func testPlaceholderSubstitution() {
        let text = Copy.text("mc.quota.remaining", ["remaining": "1", "limit": "2"])
        XCTAssertTrue(text.contains("1"))
        XCTAssertTrue(text.contains("2"))
        XCTAssertFalse(text.contains("{"))
    }
}
