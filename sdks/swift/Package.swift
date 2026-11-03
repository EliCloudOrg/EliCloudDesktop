// swift-tools-version: 6.0
// EliCloudSDK：EliCloud 客户端契约（contracts/openapi.yaml）的 Swift 实现。
//
// 依赖只取官方包：
//   - swift-openapi-runtime  生成的 Types/Client 的运行时（DTO + 编解码 + Client 基类）
//   - swift-http-types       生成的代码直接 `import HTTPTypes`
// 不含 SwiftUI/AppKit，可被 macOS 与 iOS 客户端复用。
//
// 传输层是本 SDK 自己的 EliCloudTransport：官方 URLSessionTransport 无法表达
// 契约要求的分类超时（20/8/30/60s）与“错误信封 → EliCloudError”映射。
//
// 生成代码（Sources/EliCloudSDK/Generated/）**入库**，由
// `python tools/gen-swift-sdk/gen_swift_sdk.py` 生成，禁止手改；CI 会校验漂移。
import PackageDescription

let package = Package(
    name: "EliCloudSDK",
    platforms: [
        .macOS(.v14),
        .iOS(.v17),
    ],
    products: [
        .library(name: "EliCloudSDK", targets: ["EliCloudSDK"]),
    ],
    dependencies: [
        // 与 tools/gen-swift-sdk/gen_swift_sdk.py 的 GENERATOR_VERSION 同步升级。
        .package(url: "https://github.com/apple/swift-openapi-runtime", exact: "1.9.0"),
        .package(url: "https://github.com/apple/swift-http-types", exact: "1.8.0"),
    ],
    targets: [
        .target(
            name: "EliCloudSDK",
            dependencies: [
                .product(name: "OpenAPIRuntime", package: "swift-openapi-runtime"),
                .product(name: "HTTPTypes", package: "swift-http-types"),
            ]
        ),
        .testTarget(
            name: "EliCloudSDKTests",
            dependencies: ["EliCloudSDK"]
        ),
    ],
    swiftLanguageModes: [.v6]
)
