import EliCloudSDK
import Foundation

/// 文案访问（唯一真源是 `contracts/copy/zh-CN.json` → `Localizable.strings`）。
///
/// `.strings` 用 `{name}` 占位符（与其它平台一致），SwiftUI 的 `Text("key")` 不会替换它，
/// 所以统一走这里：取本地化字符串 + 手工替换占位符。
enum Copy {

    /// 只发布 zh-Hans 一套文案；系统语言不是中文时也回退到它，而不是把 key 显示给用户。
    private static let localizedBundle: Bundle = {
        if let path = Bundle.main.path(forResource: "zh-Hans", ofType: "lproj"),
            let bundle = Bundle(path: path)
        {
            return bundle
        }
        return .main
    }()

    static func text(_ key: String, _ arguments: [String: String] = [:]) -> String {
        var value = localizedBundle.localizedString(forKey: key, value: key, table: nil)
        for (name, replacement) in arguments {
            value = value.replacingOccurrences(of: "{\(name)}", with: replacement)
        }
        return value
    }

    /// 错误 → 用户文案：`errors.json` 的 code → copyKey → 本地化字符串。
    ///
    /// 界面永不出现错误码/接口名/scope（契约 §4）。
    static func error(_ error: EliCloudError) -> String {
        let code = resolvedCode(for: error)
        let key = ErrorCopyKeys.copyKey(for: code) ?? "error.client.unknown"
        return text(key, retryAfterArguments(error))
    }

    /// 本地合成一个与契约无关的兜底错误（例如未知异常）。
    static func genericError() -> String {
        text("error.client.unknown")
    }

    private static func resolvedCode(for error: EliCloudError) -> String {
        if let code = error.code, ErrorCopyKeys.copyKey(for: code) != nil {
            return code
        }
        switch error.kind {
        case .transport:
            return error.isNetworkProblem ? "network_error" : "timeout"
        case .cancelled:
            return "http_error"
        case .decoding:
            return "invalid_response"
        case .http:
            if error.status == 429 { return "rate_limited" }
            if let status = error.status, (500...599).contains(status) { return "server_error" }
            return "http_error"
        }
    }

    private static func retryAfterArguments(_ error: EliCloudError) -> [String: String] {
        guard let retryAfter = error.retryAfter else { return [:] }
        return ["retryAfterSeconds": String(Int(retryAfter.rounded(.up)))]
    }
}
