import Foundation

/// `contracts/errors.json` 的 `codes.<code>.copyKey` 映射。
///
/// `errors.json` 作为资源打进 App bundle（见 `project.yml`），运行时解析——
/// 不在 Swift 里手写第二份 code→key 映射。
enum ErrorCopyKeys {

    /// 找不到映射时返回 nil，由调用方回落到 `error.client.unknown`。
    static func copyKey(for code: String?) -> String? {
        guard let code, !code.isEmpty else { return nil }
        return mapping[code]
    }

    static var allCodes: [String] { Array(mapping.keys) }

    private static let mapping: [String: String] = loadMapping()

    private static func loadMapping() -> [String: String] {
        guard let data = errorsJSONData(),
            let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
            let codes = root["codes"] as? [String: Any]
        else {
            return [:]
        }
        var result: [String: String] = [:]
        for (code, raw) in codes {
            guard let entry = raw as? [String: Any], let copyKey = entry["copyKey"] as? String else { continue }
            result[code] = copyKey
        }
        return result
    }

    private static func errorsJSONData() -> Data? {
        if let url = Bundle.main.url(forResource: "errors", withExtension: "json") {
            return try? Data(contentsOf: url)
        }
        // 单元测试里 `Bundle.main` 是 App bundle，通常能命中；找不到时尝试测试 bundle。
        let testBundle = Bundle(for: BundleToken.self)
        if let url = testBundle.url(forResource: "errors", withExtension: "json") {
            return try? Data(contentsOf: url)
        }
        return nil
    }
}

private final class BundleToken {}
