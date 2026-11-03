//
//  RequestTimeouts.swift
//  EliCloudSDK
//
//  契约 §6「超时：普通 20s、探活 8s、资料保存 30s、图片上传 60s」。
//  超时按 operationId 分类，而不是全局一个值——探活要快、上传要慢。
//

import Foundation

/// 请求超时策略（秒）。
public struct RequestTimeouts: Sendable, Equatable {

    /// 普通请求（登录、列表、读取资料……）。
    public var standard: TimeInterval
    /// 探活：`mcHealth`。
    public var health: TimeInterval
    /// 资料保存：`updateMyProfile`。
    public var profileSave: TimeInterval
    /// 图片上传：`uploadMyProfileImage`。
    public var imageUpload: TimeInterval

    public init(
        standard: TimeInterval = 20,
        health: TimeInterval = 8,
        profileSave: TimeInterval = 30,
        imageUpload: TimeInterval = 60
    ) {
        self.standard = standard
        self.health = health
        self.profileSave = profileSave
        self.imageUpload = imageUpload
    }

    /// 默认值即契约值。
    public static let contract = RequestTimeouts()

    /// 按 operationId 取超时；未知 operationId 一律按普通请求处理。
    public func timeout(for operationID: String) -> TimeInterval {
        switch operationID {
        case "mcHealth": return health
        case "updateMyProfile": return profileSave
        case "uploadMyProfileImage": return imageUpload
        default: return standard
        }
    }
}
