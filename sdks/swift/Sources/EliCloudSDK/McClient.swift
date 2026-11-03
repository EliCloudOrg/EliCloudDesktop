//
//  McClient.swift
//  EliCloudSDK
//
//  契约 MC 段：健康快照（200/503 都是正常响应）、白名单条目、身份自检。
//

import Foundation

/// MC 白名单（`/mc`）。
public struct McClient: Sendable {

    private let client: Client

    init(client: Client) {
        self.client = client
    }

    /// 健康快照。
    ///
    /// 契约 §6：`/mc/healthz` 的 503 是「降级但仍带完整 body」，不是错误——
    /// 传输层放行，这里两种状态都返回解析后的快照，由 `status`/`rcon` 判断可用性。
    public func health() async throws -> Components.Schemas.McHealthStatus {
        let output = try await client.mcHealth(.init())
        switch output {
        case .ok(let ok):
            return try ok.body.json
        case .serviceUnavailable(let degraded):
            return try degraded.body.json
        default:
            throw EliCloudEndpoint.unexpected("mcHealth")
        }
    }

    /// 本账号的条目与名额。默认只返回 `active`，`includeRemoved` 时含历史。
    public func names(includeRemoved: Bool = false) async throws -> Components.Schemas.McNameListResponse {
        let input = Operations.ListMyMcNames.Input(query: .init(includeRemoved: includeRemoved))
        let output = try await client.listMyMcNames(input)
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("listMyMcNames")
        }
        return try ok.body.json
    }

    /// 提交一个 MC 用户名（幂等：首次 201，重复提交 200 + 已有条目）。
    ///
    /// 先用契约 §6 规则校验用户名与备注，不合法抛 `EliCloudValidationError`。
    public func add(name: String, note: String) async throws -> Components.Schemas.McNameEntry {
        let normalizedName = try EliCloudValidation.validateMcName(name)
        let normalizedNote = try EliCloudValidation.validateNote(note)
        let input = Operations.AddMcName.Input(
            body: .json(.init(name: normalizedName, note: normalizedNote))
        )
        let output = try await client.addMcName(input)
        switch output {
        case .created(let created):
            return try created.body.json
        case .ok(let ok):
            return try ok.body.json
        default:
            throw EliCloudEndpoint.unexpected("addMcName")
        }
    }

    /// 撤回自己的条目（幂等；已撤回再删仍是 200）。
    public func remove(id: String) async throws -> Components.Schemas.McNameEntry {
        let output = try await client.removeMcName(.init(path: .init(id: id)))
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("removeMcName")
        }
        return try ok.body.json
    }

    /// 当前身份自检（只读令牌 claim，不查 SSO）。
    public func identity() async throws -> Components.Schemas.McIdentity {
        let output = try await client.mcIdentity(.init())
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("mcIdentity")
        }
        return try ok.body.json
    }
}
