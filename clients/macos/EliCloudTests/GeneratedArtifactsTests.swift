import XCTest
@testable import EliCloud

/// 生成物自检：tokens / copy / build-config 必须能被 macOS 端加载。
final class GeneratedArtifactsTests: XCTestCase {
    func testTokensVersion() {
        XCTAssertEqual(DesignTokens.tokensVersion, 2)
    }

    func testTokenValues() {
        XCTAssertEqual(DesignTokens.space4, 16)
        XCTAssertEqual(DesignTokens.radiusControl, 10)
        XCTAssertEqual(DesignTokens.densityRow, 44)
        XCTAssertEqual(DesignTokens.motionFast, 0.2, accuracy: 0.0001)
        XCTAssertFalse(DesignTokens.typeDisplaySize <= 0)
        XCTAssertFalse(DesignTokens.typeDisplayLineHeight <= 0)
    }

    func testBuildConfigIsInjected() {
        XCTAssertFalse(BuildConfig.baseAddress.isEmpty)
        let scheme = URL(string: BuildConfig.baseAddress)?.scheme
        XCTAssertTrue(["http", "https"].contains(scheme ?? ""))
        XCTAssertEqual(BuildConfig.userAgent, "EliCloud/\(BuildConfig.appVersion) (macos)")
        XCTAssertFalse(BuildConfig.userAgent.contains("://"))
        XCTAssertTrue(["local", "prod"].contains(BuildConfig.environment))
    }

    func testCopyResourcesCoverAllKeys() throws {
        let root = try repoRoot()
        let keyMapURL = root.appendingPathComponent("design/generated/copy-keys.json")
        let stringsURL = root.appendingPathComponent("clients/macos/Resources/zh-Hans.lproj/Localizable.strings")

        let keyData = try Data(contentsOf: keyMapURL)
        let keyObject = try JSONSerialization.jsonObject(with: keyData) as? [String: Any]
        guard let keyDict = keyObject?["keys"] as? [String: Any] else {
            XCTFail("copy-keys.json 里没有 keys")
            return
        }
        let expected = Set(keyDict.keys)
        XCTAssertFalse(expected.isEmpty)

        let strings = try String(contentsOf: stringsURL, encoding: .utf8)
        let actual = Set(strings.split(separator: "\n").compactMap { line -> String? in
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            guard trimmed.hasPrefix("\"") else { return nil }
            let body = trimmed.dropFirst()
            guard let end = body.firstIndex(of: "\"") else { return nil }
            return String(body[..<end])
        })
        XCTAssertTrue(expected.subtracting(actual).isEmpty, "Localizable.strings 缺少 \(expected.subtracting(actual).count) 个 key")
        XCTAssertTrue(actual.subtracting(expected).isEmpty, "Localizable.strings 多出 \(actual.subtracting(expected).count) 个 key")
    }

    private func repoRoot() throws -> URL {
        var url = URL(fileURLWithPath: #filePath)
        for _ in 0..<10 {
            url.deleteLastPathComponent()
            if FileManager.default.fileExists(atPath: url.appendingPathComponent("design/tokens.json").path) {
                return url
            }
        }
        throw XCTSkip("找不到仓库根目录")
    }
}
