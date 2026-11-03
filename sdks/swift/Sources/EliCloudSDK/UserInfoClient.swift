//
//  UserInfoClient.swift
//  EliCloudSDK
//
//  契约 UserInfo 段：资料读写、头像/头图（multipart 上传 + 二进制读取 + ETag）。
//  图片的裁切/缩放/PNG 编码属于客户端 App（Stage 4 §4.6），SDK 只负责上传字节。
//

import Foundation
import OpenAPIRuntime

/// 一张资料图片的字节与元数据。
public struct ProfileImage: Sendable, Equatable {
    public var data: Data
    /// `image/png` / `image/jpeg` / `image/webp`。
    public var contentType: String
    /// 服务端 `ETag`（图片内容的 sha256，带双引号）；可用于 `If-None-Match`。
    public var etag: String?

    public init(data: Data, contentType: String, etag: String? = nil) {
        self.data = data
        self.contentType = contentType
        self.etag = etag
    }
}

/// 个人资料（`/userinfo`）。
public struct UserInfoClient: Sendable {

    private let client: Client

    init(client: Client) {
        self.client = client
    }

    // MARK: 资料

    /// 读取自己的资料。没有记录也是 200，`nickname`/`bio`/图片字段为 nil。
    public func myProfile() async throws -> Components.Schemas.UserProfile {
        let output = try await mapErrors { try await client.getMyProfile(.init()) }
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("getMyProfile")
        }
        return try ok.body.json
    }

    /// 全量保存昵称与简介（不是 PATCH）。
    ///
    /// 先用契约 §6 的规则做客户端校验（trim + NFC、1–32 码点、≤500 字符），
    /// 再发请求；不合法抛 `EliCloudValidationError`，不发网络请求。
    public func updateProfile(
        nickname: String,
        bio: String?
    ) async throws -> Components.Schemas.UserProfile {
        let normalizedNickname = try EliCloudValidation.normalizeNickname(nickname)
        let normalizedBio = try EliCloudValidation.normalizeBio(bio)
        let input = Operations.UpdateMyProfile.Input(
            body: .json(.init(nickname: normalizedNickname, bio: normalizedBio))
        )
        let output = try await mapErrors { try await client.updateMyProfile(input) }
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("updateMyProfile")
        }
        return try ok.body.json
    }

    /// 读取其他用户的公开资料（没有记录 → 404 `profile_not_found`）。
    public func profile(sub: String) async throws -> Components.Schemas.UserProfile {
        let output = try await mapErrors { try await client.getProfile(.init(path: .init(sub: sub))) }
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("getProfile")
        }
        return try ok.body.json
    }

    // MARK: 图片

    /// 读取自己的头像 / 头图；`If-None-Match` 命中时返回 nil（304）。
    public func myImage(
        kind: ProfileImageKind,
        ifNoneMatch: String? = nil
    ) async throws -> ProfileImage? {
        let input = Operations.GetMyProfileImage.Input(
            path: .init(kind: Components.Parameters.ProfileKind(kind)),
            headers: .init(ifNoneMatch: ifNoneMatch)
        )
        let output = try await mapErrors { try await client.getMyProfileImage(input) }
        switch output {
        case .ok(let ok):
            return try await Self.profileImage(from: ok.body, etag: ok.headers.eTag)
        case .notModified:
            return nil
        default:
            throw EliCloudEndpoint.unexpected("getMyProfileImage")
        }
    }

    /// 上传并替换头像 / 头图（multipart，字段 `file`，part 类型 `image/png`）。
    ///
    /// 调用方负责先裁切/缩放/编码成 PNG；这里只做类型与大小校验。
    public func uploadImage(
        kind: ProfileImageKind,
        pngData: Data,
        filename: String? = nil
    ) async throws -> Components.Schemas.UserProfile {
        try EliCloudValidation.validateImage(mimeType: "image/png", byteCount: pngData.count, for: kind)
        let part = Operations.UploadMyProfileImage.Input.Body.MultipartFormPayload.file(
            .init(
                payload: .init(body: HTTPBody(pngData)),
                filename: filename ?? "\(kind.rawValue).png"
            )
        )
        let input = Operations.UploadMyProfileImage.Input(
            path: .init(kind: Components.Parameters.ProfileKind(kind)),
            body: .multipartForm([part])
        )
        let output = try await mapErrors { try await client.uploadMyProfileImage(input) }
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("uploadMyProfileImage")
        }
        return try ok.body.json
    }

    /// 删除头像 / 头图（幂等：本来就没有时 `changed == false`）。
    public func deleteImage(kind: ProfileImageKind) async throws -> Components.Schemas.DeleteImageResponse {
        let output = try await mapErrors {
            try await client.deleteMyProfileImage(
                .init(path: .init(kind: Components.Parameters.ProfileKind(kind)))
            )
        }
        guard case .ok(let ok) = output else {
            throw EliCloudEndpoint.unexpected("deleteMyProfileImage")
        }
        return try ok.body.json
    }

    /// 读取其他用户的头像 / 头图；304 返回 nil。
    public func image(sub: String, kind: ProfileImageKind) async throws -> ProfileImage? {
        let output = try await mapErrors {
            try await client.getProfileImage(
                .init(path: .init(sub: sub, kind: Components.Parameters.ProfileKind(kind)))
            )
        }
        switch output {
        case .ok(let ok):
            return try await Self.profileImage(from: ok.body, etag: ok.headers.eTag)
        case .notModified:
            return nil
        default:
            throw EliCloudEndpoint.unexpected("getProfileImage")
        }
    }

    private static func profileImage(
        from body: Operations.GetMyProfileImage.Output.Ok.Body,
        etag: String?
    ) async throws -> ProfileImage {
        switch body {
        case .png(let httpBody):
            return ProfileImage(data: try await EliCloudTransport.collect(httpBody), contentType: "image/png", etag: etag)
        case .jpeg(let httpBody):
            return ProfileImage(data: try await EliCloudTransport.collect(httpBody), contentType: "image/jpeg", etag: etag)
        case .imageWebp(let httpBody):
            return ProfileImage(data: try await EliCloudTransport.collect(httpBody), contentType: "image/webp", etag: etag)
        }
    }

    /// 公开图片端点是另一个生成的 Body 枚举（内容类型相同），单独适配一次。
    private static func profileImage(
        from body: Operations.GetProfileImage.Output.Ok.Body,
        etag: String?
    ) async throws -> ProfileImage {
        switch body {
        case .png(let httpBody):
            return ProfileImage(data: try await EliCloudTransport.collect(httpBody), contentType: "image/png", etag: etag)
        case .jpeg(let httpBody):
            return ProfileImage(data: try await EliCloudTransport.collect(httpBody), contentType: "image/jpeg", etag: etag)
        case .imageWebp(let httpBody):
            return ProfileImage(data: try await EliCloudTransport.collect(httpBody), contentType: "image/webp", etag: etag)
        }
    }
}

extension Components.Parameters.ProfileKind {
    /// `ProfileImageKind` → 契约里的 `components.parameters.ProfileKind`。
    init(_ kind: ProfileImageKind) {
        switch kind {
        case .avatar: self = .avatar
        case .banner: self = .banner
        }
    }
}
