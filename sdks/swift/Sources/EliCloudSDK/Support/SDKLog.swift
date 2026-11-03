//
//  SDKLog.swift
//  EliCloudSDK
//
//  日志脱敏（契约 §4 与 Stage 4 §4.2）：
//    - 只记录 operationId / HTTP 状态 / 错误码；
//    - **不**记录令牌、密码、请求/响应体、URL 与 query（refresh token 在 query 里出现过）；
//    - Darwin 用 `os.Logger`，日志字段标 privacy；其它平台静默（SDK 不往 stdout 打东西）。
//

import Foundation

#if canImport(os)
import os
#endif

enum SDKLog {

    #if canImport(os)
    private static let logger = os.Logger(subsystem: "cloud.elipese.EliCloud", category: "sdk")
    #endif

    /// 发出请求（不含路径参数与 query，避免泄漏 capability）。
    static func request(_ operationID: String) {
        #if canImport(os)
        logger.debug("request \(operationID, privacy: .public)")
        #endif
    }

    /// 收到响应。
    static func response(_ operationID: String, status: Int) {
        #if canImport(os)
        logger.debug("response \(operationID, privacy: .public) status=\(status, privacy: .public)")
        #endif
    }

    /// 失败：只记状态与错误码，不记说明文字（可能含服务端细节）。
    static func failure(_ operationID: String, status: Int?, code: String?) {
        #if canImport(os)
        logger.error(
            "failure \(operationID, privacy: .public) status=\(status ?? -1, privacy: .public) code=\(code ?? "-", privacy: .public)"
        )
        #endif
    }
}
