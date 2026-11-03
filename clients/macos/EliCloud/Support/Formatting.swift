import Foundation

enum Format {

    /// 日期显示（只在客户端展示层用，不参与契约）。
    static func date(_ value: Date?) -> String {
        guard let value else { return "—" }
        return value.formatted(date: .abbreviated, time: .shortened)
    }

    static func date(_ value: Date?, fallback: String) -> String {
        guard let value else { return fallback }
        return date(value)
    }

    /// 关于页版本行（`settings.about.version` 带 `{version}` 占位符）。
    static func versionLine() -> String {
        Copy.text("settings.about.version", ["version": BuildConfig.appVersion])
    }
}
