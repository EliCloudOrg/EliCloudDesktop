//
//  ContractReplayTests.swift
//  EliCloudSDKTests
//
//  契约测试：直接回放 contracts/fixtures（四端共用同一套），驱动真实生成的 Client。
//  覆盖：登录成功/凭证错误/限流/邮箱未验证、注册四状态、userinfo、MC 健康降级、
//  503 放行、非 JSON 错误体、图片元数据与 304、可空字段解码、Bearer 注入。
//

import XCTest
@testable import EliCloudSDK

final class ContractReplayTests: XCTestCase {

    private let baseURL = URL(string: "https://api.example.com")!

    // MARK: 工具

    private func makeClient(
        _ fixtures: [String],
        sessionStore: SessionStore = SessionStore()
    ) throws -> (EliCloudClient, StubTransport) {
        var queues: [String: [StubTransport.Reply]] = [:]
        for relative in fixtures {
            let fixture = try Fixture.load(relative)
            queues[fixture.operationID, default: []].append(.init(fixture: fixture))
        }
        let stub = StubTransport()
        for (operationID, replies) in queues {
            for reply in replies {
                stub.enqueue(reply, for: operationID)
            }
        }
        let client = EliCloudClient(
            baseURL: baseURL,
            userAgent: "EliCloudSDKTests/1.0 (test)",
            transport: stub,
            sessionStore: sessionStore
        )
        return (client, stub)
    }

    private func error(
        from body: () async throws -> Void,
        file: StaticString = #filePath,
        line: UInt = #line
    ) async -> EliCloudError? {
        do {
            try await body()
            XCTFail("期望抛出 EliCloudError", file: file, line: line)
            return nil
        } catch let error as EliCloudError {
            return error
        } catch {
            XCTFail("抛出了非 EliCloudError：\(error)", file: file, line: line)
            return nil
        }
    }

    // MARK: 登录

    func testLoginSuccessAdoptsSession() async throws {
        let (client, stub) = try makeClient(["auth/login-success.json"])
        let result = try await client.sso.login(username: "alice", password: "S3cret!pass")

        XCTAssertEqual(result.tokenType, .bearer)
        XCTAssertEqual(result.expiresIn, 3600)
        let session = await client.sessionStore.current
        XCTAssertEqual(session?.accessToken, "eyJhbGciOiJSUzI1NiIsImtpZCI6IjIwMjYtMDEifQ.example.signature")
        XCTAssertEqual(session?.refreshToken, "rt_example_refresh_token")
        XCTAssertEqual(session?.scope, ["openid", "profile", "email", "mc:whitelist", "userinfo:read", "userinfo:write"])
        XCTAssertNotNil(session?.expiresAt)
        XCTAssertTrue(session?.isUsable() ?? false)

        // 登录是匿名端点：绝不能带 Bearer。
        let login = try XCTUnwrap(stub.requests.first(where: { $0.operationID == "login" }))
        XCTAssertEqual(login.method, "POST")
        XCTAssertEqual(login.path, "/auth/login")
        XCTAssertNil(login.headers["Authorization"])
    }

    func testLoginInvalidGrantMapsToErrorCode() async throws {
        let (client, _) = try makeClient(["auth/login-invalid-grant.json"])
        let failure = await error {
            _ = try await client.sso.login(username: "alice", password: "wrong")
        }
        XCTAssertEqual(failure?.status, 401)
        XCTAssertEqual(failure?.code, "invalid_grant")
        XCTAssertTrue(failure?.isUnauthorized ?? false)
        let session = await client.sessionStore.current
        XCTAssertNil(session, "登录失败不能留下会话")
    }

    func testLoginRateLimitedCarriesRetryAfter() async throws {
        let (client, _) = try makeClient(["auth/login-rate-limited.json"])
        let failure = await error {
            _ = try await client.sso.login(username: "alice", password: "wrong-password")
        }
        XCTAssertEqual(failure?.code, "too_many_requests")
        XCTAssertEqual(failure?.retryAfter, 90)
        XCTAssertTrue(failure?.isRateLimited ?? false)
        XCTAssertTrue(failure?.isRetryable ?? false)
    }

    func testLoginEmailNotVerified() async throws {
        let (client, _) = try makeClient(["auth/login-email-not-verified.json"])
        let failure = await error {
            _ = try await client.sso.login(username: "alice", password: "S3cret!pass")
        }
        XCTAssertEqual(failure?.status, 403)
        XCTAssertEqual(failure?.code, "email_not_verified")
    }

    // MARK: 注册

    func testRegistrationStartAndStatusMachine() async throws {
        let (client, _) = try makeClient([
            "register/start-pending.json",
            "register/status-pending.json",
            "register/status-verified.json",
            "register/status-expired.json",
            "register/status-failed.json",
        ])

        let pending = try await client.sso.startRegistration(
            username: "alice",
            password: "S3cret!pass",
            email: "alice@example.com"
        )
        XCTAssertEqual(pending.status, .pending)
        XCTAssertEqual(pending.registrationId, "reg_examplepending")
        XCTAssertEqual(pending.emailMasked, "a***@example.com")

        var status = try await client.sso.registrationStatus(registrationID: pending.registrationId)
        XCTAssertEqual(status.status, .pending)
        status = try await client.sso.registrationStatus(registrationID: pending.registrationId)
        XCTAssertEqual(status.status, .verified)
        status = try await client.sso.registrationStatus(registrationID: pending.registrationId)
        XCTAssertEqual(status.status, .expired)
        XCTAssertNotNil(status.reason)
        status = try await client.sso.registrationStatus(registrationID: pending.registrationId)
        XCTAssertEqual(status.status, .failed)
    }

    func testRegistrationCancelAndResend() async throws {
        let (client, _) = try makeClient(["register/cancel.json", "register/resend.json"])
        try await client.sso.cancelRegistration(registrationID: "reg_examplepending")
        let resent = try await client.sso.resendRegistrationEmail(registrationID: "reg_examplepending")
        XCTAssertEqual(resent.resendAfter, 61)
    }

    // MARK: userinfo

    func testUserInfoMergesIdentityIntoSession() async throws {
        let store = SessionStore()
        let (client, _) = try makeClient(["auth/login-success.json", "userinfo/full.json"], sessionStore: store)
        _ = try await client.sso.login(username: "alice", password: "S3cret!pass")
        let info = try await client.sso.userInfo()

        XCTAssertEqual(info.sub, "u_example01")
        let session = await store.current
        XCTAssertEqual(session?.subject, "u_example01")
        XCTAssertEqual(session?.username, "alice")
        XCTAssertEqual(session?.email, "alice@example.com")
        XCTAssertEqual(session?.emailVerified, true)
        XCTAssertEqual(session?.accessToken, "eyJhbGciOiJSUzI1NiIsImtpZCI6IjIwMjYtMDEifQ.example.signature")
    }

    func testAuthenticatedRequestCarriesBearer() async throws {
        let (client, stub) = try makeClient(["auth/login-success.json", "userinfo/full.json"])
        _ = try await client.sso.login(username: "alice", password: "S3cret!pass")
        _ = try await client.sso.userInfo()

        let info = try XCTUnwrap(stub.requests.first(where: { $0.operationID == "getUserInfo" }))
        XCTAssertEqual(
            info.headers["Authorization"],
            "Bearer eyJhbGciOiJSUzI1NiIsImtpZCI6IjIwMjYtMDEifQ.example.signature"
        )
    }

    func testUserInfoWithoutEmailScope() async throws {
        let (client, _) = try makeClient(["userinfo/no-profile-scope.json"])
        let info = try await client.sso.userInfo()
        XCTAssertEqual(info.sub, "u_example01")
        XCTAssertNil(info.email)
    }

    func testUnauthorizedWithoutSession() async throws {
        let (client, _) = try makeClient(["userinfo/unauthorized.json"])
        let failure = await error {
            _ = try await client.sso.userInfo()
        }
        XCTAssertEqual(failure?.status, 401)
    }

    // MARK: 资料与图片

    func testEmptyProfileDecodesNullableImageFields() async throws {
        let (client, _) = try makeClient(["userinfo/profile-empty.json"])
        let profile = try await client.userInfo.myProfile()
        XCTAssertEqual(profile.userId, "u_example01")
        XCTAssertNil(profile.nickname)
        XCTAssertNil(profile.avatar, "契约里为 null，生成的类型必须是可选")
        XCTAssertNil(profile.banner)
    }

    func testProfileUpdateAndPublicProfile() async throws {
        let (client, _) = try makeClient(["userinfo/profile-update.json", "userinfo/profile-other.json"])
        let updated = try await client.userInfo.updateProfile(nickname: "  秋风  ", bio: " 白天写代码 ")
        XCTAssertEqual(updated.nickname, "秋风")
        let other = try await client.userInfo.profile(sub: "u_example02")
        XCTAssertEqual(other.userId, "u_example02")
    }

    func testProfileValidationRunsBeforeNetwork() async throws {
        let (client, stub) = try makeClient(["userinfo/profile-update.json"])
        await XCTAssertThrowsErrorAsync {
            _ = try await client.userInfo.updateProfile(nickname: " ", bio: nil)
        }
        XCTAssertTrue(stub.requests.isEmpty, "校验失败不应发请求")
    }

    func testImageMetadataAndNotModified() async throws {
        let (client, _) = try makeClient(["userinfo/get-avatar.json"])
        let image = try await client.userInfo.myImage(kind: .avatar)
        XCTAssertEqual(image?.contentType, "image/png")
        XCTAssertNotNil(image?.etag)
        XCTAssertFalse((image?.data ?? Data()).isEmpty)

        let (notModifiedClient, _) = try makeClient(["userinfo/get-avatar-not-modified.json"])
        let cached = try await notModifiedClient.userInfo.myImage(kind: .avatar, ifNoneMatch: "\"etag\"")
        XCTAssertNil(cached, "304 应返回 nil（走缓存）")
    }

    func testUploadImageTooLarge() async throws {
        let (client, _) = try makeClient(["userinfo/image-too-large.json"])
        let failure = await error {
            _ = try await client.userInfo.uploadImage(kind: .avatar, pngData: Data([0x89, 0x50, 0x4E, 0x47]))
        }
        XCTAssertEqual(failure?.status, 413)
        XCTAssertEqual(failure?.code, "image_too_large")
    }

    // MARK: MC

    func testHealthHealthyAndDegraded() async throws {
        let (healthy, _) = try makeClient(["mc/health-ok.json"])
        let ok = try await healthy.mc.health()
        XCTAssertEqual(ok.status, .ok)
        XCTAssertEqual(ok.rcon?.reachable, true)
        XCTAssertEqual(ok.whitelistCount, 7)

        let (degraded, _) = try makeClient(["mc/health-degraded-503.json"])
        let bad = try await degraded.mc.health()
        XCTAssertEqual(bad.status, .degraded, "503 是降级快照，必须照常解析")
        XCTAssertEqual(bad.rcon?.reachable, false)
        XCTAssertNil(bad.whitelistCount)
    }

    func testNamesListAndQuota() async throws {
        let (client, _) = try makeClient(["mc/names.json"])
        let list = try await client.mc.names(includeRemoved: false)
        XCTAssertEqual(list.sub, "u_example01")
        XCTAssertEqual(list.quota?.remaining, 1)
        XCTAssertEqual(list.names.first?.nameDisplay, "Alice_Star")
    }

    func testAddNameCreatedAndIdempotent() async throws {
        let (created, _) = try makeClient(["mc/add-name.json"])
        let first = try await created.mc.add(name: "Alice_Star", note: "主账号")
        XCTAssertEqual(first.id, "mn_0001")

        let (idempotent, _) = try makeClient(["mc/add-name-idempotent.json"])
        let again = try await idempotent.mc.add(name: "Alice_Star", note: "主账号")
        XCTAssertEqual(again.id, "mn_0001")
    }

    func testAddNameQuotaExceeded() async throws {
        let (client, _) = try makeClient(["mc/quota-exceeded.json"])
        let failure = await error {
            _ = try await client.mc.add(name: "Alice_Star", note: "主账号")
        }
        XCTAssertEqual(failure?.status, 409)
        XCTAssertEqual(failure?.code, "quota_exceeded")
    }

    func testRemoveNameAndNotFound() async throws {
        let (client, _) = try makeClient(["mc/remove.json"])
        let removed = try await client.mc.remove(id: "mn_0001")
        XCTAssertEqual(removed.status, .removed)

        let (missing, _) = try makeClient(["mc/not-found.json"])
        let failure = await error {
            _ = try await missing.mc.remove(id: "mn_9999")
        }
        XCTAssertEqual(failure?.status, 404)
        XCTAssertEqual(failure?.code, "not_found")
    }

    // MARK: 错误信封

    func testNonJSONErrorBodyKeepsOnlyStatus() async throws {
        let (client, _) = try makeClient(["errors/non-json.json"])
        let failure = await error {
            _ = try await client.mc.names()
        }
        XCTAssertEqual(failure?.status, 502)
        XCTAssertNil(failure?.code, "非 JSON 错误体只保留状态码")
        XCTAssertNil(failure?.description)
    }

    func testUnicodeErrorDescriptionDecodes() async throws {
        let (client, _) = try makeClient(["errors/unicode.json"])
        let failure = await error {
            _ = try await client.userInfo.updateProfile(nickname: "秋风", bio: nil)
        }
        XCTAssertEqual(failure?.status, 400)
        XCTAssertNotNil(failure?.description)
    }

    func testIdentityAndServiceList() async throws {
        let (client, _) = try makeClient(["mc/identity.json"])
        let identity = try await client.mc.identity()
        XCTAssertEqual(identity.sub, "u_example01")
    }
}

// MARK: - 小工具

/// 断言某个 async 闭包抛错（XCTAssertThrowsError 不支持 async）。
func XCTAssertThrowsErrorAsync(
    _ expression: () async throws -> Void,
    file: StaticString = #filePath,
    line: UInt = #line
) async {
    do {
        try await expression()
        XCTFail("期望抛错", file: file, line: line)
    } catch {
        // 正常
    }
}
