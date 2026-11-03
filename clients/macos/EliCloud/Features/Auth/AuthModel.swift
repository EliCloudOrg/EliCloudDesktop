import EliCloudSDK
import Foundation
import SwiftUI

/// 登录 / 注册 / 验证邮箱的状态机（与 `contracts/auth-flow.md` 一致）。
@MainActor
final class AuthModel: ObservableObject {

    enum Screen: Equatable {
        case login
        case register
        case verify(registrationID: String, emailMasked: String, password: String)
    }

    enum VerifyState: Equatable {
        case waiting
        case expired
        case failed(reason: String?)
        case confirmed
        case invalidated

        var isTerminal: Bool {
            switch self {
            case .waiting, .confirmed: return false
            case .expired, .failed, .invalidated: return true
            }
        }
    }

    @Published var screen: Screen = .login
    @Published var username = ""
    @Published var email = ""
    @Published var password = ""
    @Published var isBusy = false
    @Published var message: String?
    @Published var resendRemaining = 0
    @Published private(set) var verifyState: VerifyState = .waiting

    private let client: EliCloudClient
    private let onSignedIn: @MainActor () async -> Void
    private var pollTask: Task<Void, Never>?
    private var countdownTask: Task<Void, Never>?

    init(client: EliCloudClient, onSignedIn: @MainActor @escaping () async -> Void) {
        self.client = client
        self.onSignedIn = onSignedIn
    }

    /// 视图消失时停掉轮询/倒计时（不留后台任务）。
    func stop() {
        pollTask?.cancel()
        countdownTask?.cancel()
    }

    // MARK: 登录

    func submitLogin() async {
        guard !isBusy, !username.isEmpty, !password.isEmpty else { return }
        isBusy = true
        message = nil
        defer { isBusy = false }
        do {
            try await client.sso.loginAndLoadIdentity(username: username, password: password)
            password = ""
            await onSignedIn()
        } catch {
            message = Copy.error(EliCloudError.normalized(error))
        }
    }

    // MARK: 注册

    func submitRegistration() async {
        guard !isBusy, !username.isEmpty, !email.isEmpty, !password.isEmpty else { return }
        isBusy = true
        message = nil
        defer { isBusy = false }
        do {
            let pending = try await client.sso.startRegistration(
                username: username,
                password: password,
                email: email
            )
            screen = .verify(
                registrationID: pending.registrationId,
                emailMasked: pending.emailMasked,
                password: password
            )
            verifyState = .waiting
            startCountdown(seconds: pending.resendAfter)
            startPolling(registrationID: pending.registrationId, password: password)
        } catch {
            message = Copy.error(EliCloudError.normalized(error))
        }
    }

    // MARK: 验证轮询

    private func startPolling(registrationID: String, password: String) {
        pollTask?.cancel()
        pollTask = Task { [weak self] in
            guard let self else { return }
            do {
                let status = try await client.sso.pollRegistration(registrationID: registrationID)
                guard !Task.isCancelled else { return }
                switch status.status {
                case .verified:
                    verifyState = .confirmed
                    resendRemaining = 0
                    await signInAfterVerification(password: password)
                case .expired:
                    verifyState = .expired
                case .failed:
                    verifyState = .failed(reason: status.reason)
                case .pending:
                    break
                }
            } catch let error as EliCloudError where error.isCancelled {
                // 用户取消/返回：不提示
            } catch {
                message = Copy.error(EliCloudError.normalized(error))
            }
        }
    }

    private func signInAfterVerification(password: String) async {
        do {
            try await client.sso.loginAndLoadIdentity(username: username, password: password)
            self.password = ""
            await onSignedIn()
        } catch {
            // 契约 §5：自动登录失败时留在验证页，提示用原密码手动登录。
            message = Copy.text("verify.confirmedSignIn")
        }
    }

    private func startCountdown(seconds: Int) {
        countdownTask?.cancel()
        resendRemaining = max(0, seconds)
        guard resendRemaining > 0 else { return }
        countdownTask = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(1))
                guard let self, !Task.isCancelled else { return }
                if resendRemaining > 0 { resendRemaining -= 1 } else { return }
            }
        }
    }

    func resendVerification() async {
        guard case let .verify(registrationID, _, _) = screen, !isBusy else { return }
        isBusy = true
        message = nil
        defer { isBusy = false }
        do {
            let pending = try await client.sso.resendRegistrationEmail(registrationID: registrationID)
            startCountdown(seconds: pending.resendAfter)
            verifyState = .waiting
            startPolling(registrationID: registrationID, password: password)
        } catch let error as EliCloudError {
            if let retryAfter = error.retryAfter {
                startCountdown(seconds: Int(retryAfter.rounded(.up)))
            }
            message = Copy.error(error)
        } catch {
            message = Copy.error(EliCloudError.normalized(error))
        }
    }

    func cancelVerification() async {
        guard case let .verify(registrationID, _, _) = screen else { return }
        pollTask?.cancel()
        countdownTask?.cancel()
        _ = try? await client.sso.cancelRegistration(registrationID: registrationID)
        verifyState = .waiting
        screen = .login
    }

    func backToLogin() {
        pollTask?.cancel()
        countdownTask?.cancel()
        screen = .login
        verifyState = .waiting
    }

    func retryResend() async {
        await resendVerification()
    }
}
