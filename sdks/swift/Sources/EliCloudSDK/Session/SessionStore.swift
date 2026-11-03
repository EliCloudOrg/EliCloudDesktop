//
//  SessionStore.swift
//  EliCloudSDK
//
//  契约 §5：会话只存在内存（actor 保护），进程结束即丢弃；
//  默认不自动刷新（可选特性见 Stage 4 §1.2），过期就重新登录。
//

import Foundation

/// 内存会话容器。所有读写都在 actor 上串行，避免并发刷新/退出时的竞态。
public actor SessionStore {

    private var state: SessionState?

    public init(initial: SessionState? = nil) {
        self.state = initial
    }

    /// 当前会话（未登录为 nil）。
    public var current: SessionState? { state }

    /// 整体替换（登录成功、刷新成功、退出都为 nil）。
    public func adopt(_ state: SessionState?) {
        self.state = state
    }

    /// 退出登录：清空内存，不写磁盘。
    public func clear() {
        state = nil
    }

    /// 可用的 access token；过期时返回 nil（本阶段不自动刷新）。
    public func accessToken(now: Date = Date()) -> String? {
        guard let state, state.isUsable(now: now) else { return nil }
        return state.accessToken
    }

    /// 合并新的身份信息（`/auth/userinfo` 的结果），令牌保持不变。
    public func mergeIdentity(
        subject: String?,
        username: String?,
        email: String?,
        emailVerified: Bool?,
        scope: [String]?
    ) {
        guard var state else { return }
        if let subject { state.subject = subject }
        if let username { state.username = username }
        if let email { state.email = email }
        if let emailVerified { state.emailVerified = emailVerified }
        if let scope { state.scope = scope }
        self.state = state
    }
}
