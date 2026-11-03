import XCTest
@testable import EliCloudSDK

final class ValidatorsTests: XCTestCase {

    func testNicknameTrimsAndNormalizes() throws {
        XCTAssertEqual(try EliCloudValidation.normalizeNickname("  秋风  "), "秋风")
        // NFC：e + 组合重音 → é
        XCTAssertEqual(try EliCloudValidation.normalizeNickname("e\u{0301}"), "é")
    }

    func testNicknameRejectsEmptyAndTooLong() {
        XCTAssertThrowsError(try EliCloudValidation.normalizeNickname("   "))
        XCTAssertThrowsError(try EliCloudValidation.normalizeNickname(String(repeating: "秋", count: 33)))
        XCTAssertNoThrow(try EliCloudValidation.normalizeNickname(String(repeating: "秋", count: 32)))
    }

    func testNicknameRejectsControlAndNewline() {
        XCTAssertThrowsError(try EliCloudValidation.normalizeNickname("秋\u{0000}风"))
        XCTAssertThrowsError(try EliCloudValidation.normalizeNickname("秋\n风"))
    }

    func testBio() throws {
        XCTAssertNil(try EliCloudValidation.normalizeBio("   "))
        XCTAssertEqual(try EliCloudValidation.normalizeBio("  白天写代码  "), "白天写代码")
        XCTAssertThrowsError(try EliCloudValidation.normalizeBio(String(repeating: "a", count: 501)))
    }

    func testMcName() throws {
        XCTAssertEqual(try EliCloudValidation.validateMcName(" Alice_Star "), "Alice_Star")
        XCTAssertEqual(try EliCloudValidation.validateMcName("abc"), "abc")
        XCTAssertThrowsError(try EliCloudValidation.validateMcName("ab"))
        XCTAssertThrowsError(try EliCloudValidation.validateMcName("a-b"))
        XCTAssertThrowsError(try EliCloudValidation.validateMcName(String(repeating: "a", count: 17)))
    }

    func testNote() throws {
        XCTAssertEqual(try EliCloudValidation.validateNote(" 主账号 "), "主账号")
        XCTAssertThrowsError(try EliCloudValidation.validateNote("  "))
        XCTAssertThrowsError(try EliCloudValidation.validateNote(String(repeating: "a", count: 201)))
    }

    func testImageLimits() throws {
        XCTAssertNoThrow(
            try EliCloudValidation.validateImage(
                mimeType: "image/png",
                byteCount: ProfileImageKind.avatar.maxBytes,
                for: .avatar
            )
        )
        XCTAssertThrowsError(
            try EliCloudValidation.validateImage(
                mimeType: "image/png",
                byteCount: ProfileImageKind.avatar.maxBytes + 1,
                for: .avatar
            )
        )
        XCTAssertThrowsError(
            try EliCloudValidation.validateImage(mimeType: "image/gif", byteCount: 10, for: .banner)
        )
        XCTAssertEqual(ProfileImageKind.avatar.aspectRatio, 1)
        XCTAssertEqual(ProfileImageKind.banner.aspectRatio, 5)
        XCTAssertEqual(ProfileImageKind.banner.maxBytes, 10 * 1024 * 1024)
    }
}
