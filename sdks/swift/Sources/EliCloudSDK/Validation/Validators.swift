//
//  Validators.swift
//  EliCloudSDK
//
//  契约 §6（validation.md）里的客户端校验规则。放在 SDK 里而不是各端 UI 里：
//  四个端必须判定一致，否则会出现“A 端能存、B 端报错”的分叉。
//
//  这里只做**格式**校验；真正的拒绝仍以服务端为准（400 invalid_request + 字段码）。
//

import Foundation

/// 字段级校验失败。文案由各端按 key 映射，SDK 不带中文。
public enum EliCloudValidationError: Error, Sendable, Equatable {
    /// 昵称：trim + NFC 后 1–32 个码点，不能为空、不能含控制/格式字符。
    case nicknameInvalid
    /// 简介：≤500 字符（纯文本，控制字符拒绝）。
    case bioTooLong
    /// 简介含控制字符/换行。
    case bioInvalid
    /// MC 用户名：`^[A-Za-z0-9_]{3,16}$`。
    case mcNameInvalid
    /// 备注：1–200 字符，不能为空、不能含控制字符。
    case noteInvalid
    /// 图片类型不在 PNG/JPEG/WebP 白名单里。
    case imageTypeUnsupported
    /// 图片超过大小上限（头像 5 MiB、头图 10 MiB）。
    case imageTooLarge
}

public enum EliCloudValidation {

    /// 昵称上限（码点）。
    public static let nicknameMaxLength = 32
    /// 简介上限（字符）。
    public static let bioMaxLength = 500
    /// 备注上下限。
    public static let noteMinLength = 1
    public static let noteMaxLength = 200
    /// 头像 / 头图大小上限（字节）。
    public static let avatarMaxBytes = 5 * 1024 * 1024
    public static let bannerMaxBytes = 10 * 1024 * 1024

    /// 昵称：trim + NFC → 1–32 码点，禁止控制/格式/代理/私用/未分配字符，不能换行。
    public static func normalizeNickname(_ raw: String) throws -> String {
        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines).precomposedStringWithCanonicalMapping
        guard !trimmed.isEmpty, trimmed.unicodeScalars.count <= nicknameMaxLength else {
            throw EliCloudValidationError.nicknameInvalid
        }
        guard trimmed.unicodeScalars.allSatisfy(isDisplayableCharacter) else {
            throw EliCloudValidationError.nicknameInvalid
        }
        return trimmed
    }

    /// 简介：纯文本，≤500 字符；空串等价于清空（返回 nil）。
    public static func normalizeBio(_ raw: String?) throws -> String? {
        guard let raw else { return nil }
        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        guard trimmed.unicodeScalars.count <= bioMaxLength else {
            throw EliCloudValidationError.bioTooLong
        }
        guard trimmed.unicodeScalars.allSatisfy({ !$0.isControlCharacter }) else {
            throw EliCloudValidationError.bioInvalid
        }
        return trimmed
    }

    /// MC 用户名：`^[A-Za-z0-9_]{3,16}$`；服务端会归一为小写，这里只校验原样。
    public static func validateMcName(_ raw: String) throws -> String {
        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard (3...16).contains(trimmed.count),
            trimmed.allSatisfy({ $0.isASCII && ($0.isLetter || $0.isNumber || $0 == "_") })
        else {
            throw EliCloudValidationError.mcNameInvalid
        }
        return trimmed
    }

    /// 备注：1–200 字符，不能为空、不能含控制字符。
    public static func validateNote(_ raw: String) throws -> String {
        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard (noteMinLength...noteMaxLength).contains(trimmed.count),
            trimmed.unicodeScalars.allSatisfy({ !$0.isControlCharacter })
        else {
            throw EliCloudValidationError.noteInvalid
        }
        return trimmed
    }

    /// 上传前的类型/大小校验（MIME 必须是服务端白名单里的三种）。
    public static func validateImage(mimeType: String, byteCount: Int, for kind: ProfileImageKind) throws {
        guard kind.allowedMimeTypes.contains(mimeType.lowercased()) else {
            throw EliCloudValidationError.imageTypeUnsupported
        }
        guard byteCount <= kind.maxBytes else {
            throw EliCloudValidationError.imageTooLarge
        }
    }

    private static func isDisplayableCharacter(_ scalar: Unicode.Scalar) -> Bool {
        switch scalar.properties.generalCategory {
        case .control, .format, .surrogate, .privateUse, .unassigned, .lineSeparator, .paragraphSeparator:
            return false
        default:
            return true
        }
    }
}

extension Unicode.Scalar {
    /// 控制字符（C0/C1）与换行类字符。
    var isControlCharacter: Bool {
        switch properties.generalCategory {
        case .control, .format, .lineSeparator, .paragraphSeparator:
            return true
        default:
            return false
        }
    }
}

/// 资料图片的两种种类（对应契约 `components.parameters.ProfileKind`）。
public enum ProfileImageKind: String, Sendable, CaseIterable {
    case avatar
    case banner

    /// 服务端接受的 MIME 白名单。
    public var allowedMimeTypes: Set<String> {
        ["image/png", "image/jpeg", "image/webp"]
    }

    /// 大小上限（契约 §6：头像 5 MB、头图 10 MB）。
    public var maxBytes: Int {
        switch self {
        case .avatar: return EliCloudValidation.avatarMaxBytes
        case .banner: return EliCloudValidation.bannerMaxBytes
        }
    }

    /// 展示用比例：头像 1:1、头图 5:1。
    public var aspectRatio: Double {
        switch self {
        case .avatar: return 1
        case .banner: return 5
        }
    }
}
