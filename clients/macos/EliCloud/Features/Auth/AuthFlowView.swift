import SwiftUI

/// 未登录时的流程容器：登录 → 注册 → 验证邮箱。
struct AuthFlowView: View {
    @EnvironmentObject private var app: AppState
    @StateObject private var model: AuthModel

    init(app: AppState) {
        _model = StateObject(
            wrappedValue: AuthModel(client: app.client) { [weak app] in
                await app?.refreshAfterSignIn()
            }
        )
    }

    var body: some View {
        ZStack {
            DesignTokens.colorSurfaceBase
                .ignoresSafeArea()

            VStack(spacing: DesignTokens.space4) {
                brandHeader
                card
            }
            .frame(width: 431)
            .padding(DesignTokens.space6)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .onDisappear { model.stop() }
    }

    private var brandHeader: some View {
        VStack(spacing: DesignTokens.space1) {
            Text(Copy.text("app.title"))
                .font(DesignTokens.typeTitle)
                .foregroundStyle(DesignTokens.colorTextPrimary)
            Text(Copy.text("app.subtitle"))
                .font(DesignTokens.typeCaption)
                .foregroundStyle(DesignTokens.colorTextSecondary)
        }
        .accessibilityElement(children: .combine)
    }

    @ViewBuilder
    private var card: some View {
        switch model.screen {
        case .login:
            LoginView(model: model)
        case .register:
            RegisterView(model: model)
        case let .verify(_, emailMasked, _):
            VerifyEmailView(model: model, emailMasked: emailMasked)
        }
    }
}

// MARK: - 登录

struct LoginView: View {
    @ObservedObject var model: AuthModel
    @FocusState private var focusedField: Field?

    private enum Field { case identifier, password }

    var body: some View {
        VStack(alignment: .leading, spacing: DesignTokens.space4) {
            fieldLabel("login.usernameOrEmail.label")

            TextField(
                "",
                text: $model.username,
                prompt: Text(Copy.text("login.usernameOrEmail.placeholder"))
            )
            .textFieldStyle(.roundedBorder)
            .focused($focusedField, equals: .identifier)
            .onSubmit { Task { await model.submitLogin() } }
            .accessibilityLabel(Copy.text("login.usernameOrEmail.label"))

            fieldLabel("login.password.label")

            SecureField("", text: $model.password, prompt: Text(Copy.text("login.password.placeholder")))
                .textFieldStyle(.roundedBorder)
                .focused($focusedField, equals: .password)
                .onSubmit { Task { await model.submitLogin() } }
                .accessibilityLabel(Copy.text("login.password.label"))

            if let message = model.message {
                InlineError(message: message)
            }

            Button {
                Task { await model.submitLogin() }
            } label: {
                Text(model.isBusy ? Copy.text("login.busy") : Copy.text("login.submit"))
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .disabled(model.isBusy || model.username.isEmpty || model.password.isEmpty)
            .accessibilityLabel(Copy.text("login.submit"))

            Divider()

            Button(Copy.text("login.switchToRegister")) {
                model.message = nil
                model.screen = .register
            }
            .buttonStyle(.link)
            .accessibilityLabel(Copy.text("login.switchToRegister"))
        }
        .padding(DesignTokens.space6)
        .glassBackground(.card)
        .onAppear { focusedField = .identifier }
    }

    private func fieldLabel(_ key: String) -> some View {
        Text(Copy.text(key))
            .font(DesignTokens.typeCaption)
            .foregroundStyle(DesignTokens.colorTextSecondary)
    }
}

// MARK: - 注册

struct RegisterView: View {
    @ObservedObject var model: AuthModel
    @FocusState private var focusedField: Field?

    private enum Field { case username, email, password }

    var body: some View {
        VStack(alignment: .leading, spacing: DesignTokens.space3) {
            fieldLabel("login.username.label")
            TextField("", text: $model.username, prompt: Text(Copy.text("login.username.placeholder")))
                .textFieldStyle(.roundedBorder)
                .focused($focusedField, equals: .username)
                .accessibilityLabel(Copy.text("login.username.label"))
            FieldHint(text: Copy.text("login.username.hint"))

            fieldLabel("login.email.label")
            TextField("", text: $model.email, prompt: Text(Copy.text("login.email.placeholder")))
                .textFieldStyle(.roundedBorder)
                .focused($focusedField, equals: .email)
                .accessibilityLabel(Copy.text("login.email.label"))

            fieldLabel("login.password.label")
            SecureField("", text: $model.password, prompt: Text(Copy.text("login.password.placeholder")))
                .textFieldStyle(.roundedBorder)
                .focused($focusedField, equals: .password)
                .onSubmit { Task { await model.submitRegistration() } }
                .accessibilityLabel(Copy.text("login.password.label"))

            if let message = model.message {
                InlineError(message: message)
            }

            Button {
                Task { await model.submitRegistration() }
            } label: {
                Text(model.isBusy ? Copy.text("login.register.busy") : Copy.text("login.register.submit"))
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .disabled(model.isBusy || model.username.isEmpty || model.email.isEmpty || model.password.isEmpty)
            .accessibilityLabel(Copy.text("login.register.submit"))

            Divider()

            Button(Copy.text("login.switchToLogin")) {
                model.message = nil
                model.screen = .login
            }
            .buttonStyle(.link)
            .keyboardShortcut(.cancelAction)
            .accessibilityLabel(Copy.text("login.switchToLogin"))
        }
        .padding(DesignTokens.space6)
        .glassBackground(.card)
        .onAppear { focusedField = .username }
    }

    private func fieldLabel(_ key: String) -> some View {
        Text(Copy.text(key))
            .font(DesignTokens.typeCaption)
            .foregroundStyle(DesignTokens.colorTextSecondary)
    }
}

// MARK: - 验证邮箱

struct VerifyEmailView: View {
    @ObservedObject var model: AuthModel
    let emailMasked: String

    var body: some View {
        VStack(alignment: .leading, spacing: DesignTokens.space4) {
            Text(Copy.text("verify.title"))
                .font(DesignTokens.typeTitle)
                .foregroundStyle(DesignTokens.colorTextPrimary)

            Text(Copy.text("verify.sent", ["email": emailMasked]))
                .font(DesignTokens.typeBody)
                .foregroundStyle(DesignTokens.colorTextPrimary)
                .fixedSize(horizontal: false, vertical: true)

            Text(Copy.text("verify.instructions"))
                .font(DesignTokens.typeCaption)
                .foregroundStyle(DesignTokens.colorTextSecondary)
                .fixedSize(horizontal: false, vertical: true)

            statusLine

            if let message = model.message {
                InlineError(message: message)
            }

            HStack(spacing: DesignTokens.space3) {
                resendButton
                Button(Copy.text("verify.backToLogin")) {
                    Task { await model.cancelVerification() }
                }
                .keyboardShortcut(.cancelAction)
                .accessibilityLabel(Copy.text("verify.backToLogin"))
            }
        }
        .padding(DesignTokens.space6)
        .glassBackground(.card)
        .accessibilityElement(children: .contain)
    }

    @ViewBuilder
    private var statusLine: some View {
        switch model.verifyState {
        case .waiting:
            HStack(spacing: DesignTokens.space2) {
                ProgressView()
                    .controlSize(.small)
                Text(Copy.text("verify.waiting"))
                    .font(DesignTokens.typeCaption)
                    .foregroundStyle(DesignTokens.colorTextSecondary)
            }
            .accessibilityElement(children: .combine)
        case .expired:
            InlineError(message: Copy.text("verify.expired"))
        case .failed(let reason):
            InlineError(message: terminalMessage(for: reason))
        case .invalidated:
            InlineError(message: Copy.text("verify.invalidated"))
        case .confirmed:
            Text(Copy.text("verify.confirmedSignIn"))
                .font(DesignTokens.typeCaption)
                .foregroundStyle(DesignTokens.colorTextSecondary)
        }
    }

    @ViewBuilder
    private var resendButton: some View {
        if model.resendRemaining > 0 {
            Text(Copy.text("verify.resendCountdown", ["seconds": String(model.resendRemaining)]))
                .font(DesignTokens.typeCaption)
                .foregroundStyle(DesignTokens.colorTextSecondary)
                .accessibilityLabel(Copy.text("verify.resendCountdown", ["seconds": String(model.resendRemaining)]))
        } else {
            Button(Copy.text("verify.resend")) {
                Task { await model.resendVerification() }
            }
            .disabled(model.isBusy)
            .accessibilityLabel(Copy.text("verify.resend"))
        }
    }

    private func terminalMessage(for reason: String?) -> String {
        switch reason {
        case "username_taken": return Copy.text("verify.usernameTaken")
        case "email_taken": return Copy.text("verify.emailTaken")
        case "email_delivery_failed", "email_service_unavailable": return Copy.text("verify.incomplete")
        default: return Copy.text("verify.terminalDetail")
        }
    }
}
