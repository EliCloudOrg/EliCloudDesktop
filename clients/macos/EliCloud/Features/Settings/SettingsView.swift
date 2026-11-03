import SwiftUI

/// 设置（`Settings` scene）：账户 / 外观 / 关于 / 退出登录。
///
/// 不显示入口地址、令牌、scope（Stage 4 §4.4 / §7）。
struct SettingsView: View {
    @EnvironmentObject private var app: AppState

    var body: some View {
        Form {
            Section {
                LabeledContent {
                    Text(app.usernameLine.isEmpty ? "—" : app.usernameLine)
                } label: {
                    Text(Copy.text("profile.username"))
                }
                LabeledContent {
                    Text(app.emailLine.isEmpty ? Copy.text("profile.emailUnbound") : app.emailLine)
                } label: {
                    Text(Copy.text("profile.email"))
                }
                LabeledContent {
                    Text(app.emailVerified ? Copy.text("status.emailVerified") : Copy.text("status.emailUnverified"))
                } label: {
                    Text(Copy.text("profile.emailStatus"))
                }

                Button(Copy.text("settings.account.signOut"), role: .destructive) {
                    Task { await app.signOut() }
                }
                .accessibilityLabel(Copy.text("settings.account.signOut"))
            } header: {
                Text(Copy.text("settings.account.title"))
            }

            Section {
                Picker(selection: $app.theme) {
                    ForEach(ThemePreference.allCases) { preference in
                        Text(Copy.text(preference.copyKey)).tag(preference)
                    }
                } label: {
                    Text(Copy.text("settings.appearance.title"))
                }
                .pickerStyle(.segmented)
                .accessibilityLabel(Copy.text("settings.appearance.title"))
            } header: {
                Text(Copy.text("settings.appearance.title"))
            }

            Section {
                LabeledContent {
                    Text(Format.versionLine())
                } label: {
                    Text(Copy.text("settings.about.title"))
                }
                LabeledContent {
                    Text(BuildConfig.environment)
                } label: {
                    Text(Copy.text("app.subtitle"))
                }
            } header: {
                Text(Copy.text("settings.about.title"))
            }
        }
        .formStyle(.grouped)
        .frame(minWidth: 460, minHeight: 420)
        .onAppear { app.applyAppearance() }
    }
}
