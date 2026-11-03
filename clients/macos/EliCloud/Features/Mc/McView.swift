import EliCloudSDK
import SwiftUI

/// MC 白名单页：状态卡 + 名额 + 我的条目 + 添加/撤回。
struct McView: View {
    @EnvironmentObject private var app: AppState

    @State private var isAdding = false
    @State private var isRefreshing = false
    @State private var message: String?
    @State private var pendingRemoval: Components.Schemas.McNameEntry?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: DesignTokens.space6) {
                SectionHeader(
                    title: Copy.text("mc.title"),
                    subtitle: Copy.text("mc.description")
                )

                if let message {
                    InlineError(message: message)
                }

                healthCard
                quotaCard
                namesCard
            }
            .padding(DesignTokens.space6)
            .frame(maxWidth: 980, alignment: .leading)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .background(DesignTokens.colorSurfaceBase)
        .navigationTitle(Copy.text("mc.title"))
        .toolbar {
            ToolbarItem(placement: .primaryAction) {
                Button(Copy.text("mc.addOpen")) { isAdding = true }
                    .disabled(quotaRemaining == 0)
                    .accessibilityLabel(Copy.text("mc.addOpen"))
            }
        }
        .sheet(isPresented: $isAdding) {
            AddMcNameSheet()
                .environmentObject(app)
        }
        .confirmationDialog(
            Copy.text("mc.names.removeNamed", ["name": pendingRemoval?.nameDisplay ?? ""]),
            isPresented: Binding(
                get: { pendingRemoval != nil },
                set: { if !$0 { pendingRemoval = nil } }
            ),
            titleVisibility: .visible,
            presenting: pendingRemoval
        ) { entry in
            Button(Copy.text("mc.names.remove"), role: .destructive) {
                Task { await remove(entry) }
            }
            Button(Copy.text("common.cancel"), role: .cancel) {}
        } message: { _ in
            Text(Copy.text("mc.names.removeHint"))
        }
        .task { await load() }
    }

    // MARK: 卡片

    private var healthCard: some View {
        Card {
            VStack(alignment: .leading, spacing: DesignTokens.space3) {
                HStack {
                    Text(Copy.text("mc.server.title"))
                        .font(DesignTokens.typeBody)
                    Spacer()
                    if app.isProbing {
                        ProgressView()
                            .controlSize(.small)
                    } else {
                        StatusBadge(text: healthText, level: healthLevel)
                    }
                }

                if let health = app.mcHealth, let rcon = health.rcon {
                    HStack(spacing: DesignTokens.space6) {
                        metric(
                            Copy.text("mc.server.latency"),
                            rcon.latencyMs.map { Copy.text("mc.server.latencyValue", ["milliseconds": String($0)]) }
                                ?? Copy.text("mc.server.unreachable")
                        )
                        metric(
                            Copy.text("mc.whitelist.count", ["count": String(health.whitelistCount ?? 0)]),
                            health.version ?? "—"
                        )
                    }
                } else {
                    Text(Copy.text("mc.server.unreachable"))
                        .font(DesignTokens.typeCaption)
                        .foregroundStyle(DesignTokens.colorTextSecondary)
                }
            }
        }
    }

    private var quotaCard: some View {
        Card {
            VStack(alignment: .leading, spacing: DesignTokens.space2) {
                Text(Copy.text("mc.quota.title"))
                    .font(DesignTokens.typeBody)
                if let quota = app.mcNames?.quota {
                    Text(
                        quota.remaining > 0
                            ? Copy.text("mc.quota.remaining", ["remaining": String(quota.remaining), "limit": String(quota.limit)])
                            : Copy.text("mc.quota.exhausted", ["limit": String(quota.limit)])
                    )
                    .font(DesignTokens.typeCaption)
                    .foregroundStyle(DesignTokens.colorTextSecondary)
                    if quota.remaining == 0 {
                        Text(Copy.text("mc.quota.exceededDetail", ["limit": String(quota.limit)]))
                            .font(DesignTokens.typeCaption)
                            .foregroundStyle(DesignTokens.colorTextSecondary)
                    }
                } else {
                    Text(Copy.text("mc.names.loading"))
                        .font(DesignTokens.typeCaption)
                        .foregroundStyle(DesignTokens.colorTextSecondary)
                }
            }
        }
    }

    private var namesCard: some View {
        Card {
            VStack(alignment: .leading, spacing: DesignTokens.space3) {
                Text(Copy.text("mc.names.title"))
                    .font(DesignTokens.typeBody)

                if let names = app.mcNames?.names, !names.isEmpty {
                    VStack(spacing: DesignTokens.space2) {
                        ForEach(names, id: \.id) { entry in
                            nameRow(entry)
                            if entry.id != names.last?.id {
                                Divider()
                            }
                        }
                    }
                } else if let names = app.mcNames?.names, names.isEmpty {
                    Text(Copy.text("mc.names.empty"))
                        .font(DesignTokens.typeCaption)
                        .foregroundStyle(DesignTokens.colorTextSecondary)
                } else {
                    Text(Copy.text("mc.names.loading"))
                        .font(DesignTokens.typeCaption)
                        .foregroundStyle(DesignTokens.colorTextSecondary)
                }
            }
        }
    }

    private func nameRow(_ entry: Components.Schemas.McNameEntry) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: DesignTokens.space3) {
            VStack(alignment: .leading, spacing: DesignTokens.space1) {
                Text(entry.nameDisplay.isEmpty ? Copy.text("mc.names.nameless") : entry.nameDisplay)
                    .font(DesignTokens.typeBody)
                    .foregroundStyle(DesignTokens.colorTextPrimary)
                Text(entry.note)
                    .font(DesignTokens.typeCaption)
                    .foregroundStyle(DesignTokens.colorTextSecondary)
                if let createdAt = entry.createdAt {
                    Text(Copy.text("mc.names.addedAt", ["created": Format.date(createdAt)]))
                        .font(DesignTokens.typeCaption)
                        .foregroundStyle(DesignTokens.colorTextDisabled)
                }
            }
            Spacer(minLength: DesignTokens.space2)
            Button(Copy.text("mc.names.remove")) {
                pendingRemoval = entry
            }
            .accessibilityLabel(Copy.text("mc.names.removeNamed", ["name": entry.nameDisplay]))
        }
        .accessibilityElement(children: .contain)
    }

    private func metric(_ label: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: DesignTokens.space1) {
            Text(label)
                .font(DesignTokens.typeCaption)
                .foregroundStyle(DesignTokens.colorTextSecondary)
            Text(value)
                .font(DesignTokens.typeBody)
                .foregroundStyle(DesignTokens.colorTextPrimary)
        }
        .accessibilityElement(children: .combine)
    }

    // MARK: 状态与动作

    private var quotaRemaining: Int? {
        app.mcNames?.quota?.remaining
    }

    private var healthText: String {
        guard let health = app.mcHealth else { return Copy.text("mc.server.unreachable") }
        return health.status == .ok ? Copy.text("mc.server.health") : Copy.text("mc.server.degraded")
    }

    private var healthLevel: StatusBadge.Level {
        guard let health = app.mcHealth else { return .danger }
        return health.status == .ok ? .success : .warning
    }

    private func load() async {
        isRefreshing = true
        defer { isRefreshing = false }
        message = nil
        await app.probeServices()
        do {
            try await app.loadMcNames(includeRemoved: false)
        } catch {
            message = Copy.error(EliCloudError.normalized(error))
        }
    }

    private func remove(_ entry: Components.Schemas.McNameEntry) async {
        pendingRemoval = nil
        message = nil
        do {
            _ = try await app.client.mc.remove(id: entry.id)
            try await app.loadMcNames(includeRemoved: false)
        } catch {
            message = Copy.error(EliCloudError.normalized(error))
        }
    }
}

/// 添加用户名 sheet：客户端先按契约 §6 校验，再提交。
struct AddMcNameSheet: View {
    @EnvironmentObject private var app: AppState
    @Environment(\.dismiss) private var dismiss

    @State private var name = ""
    @State private var note = ""
    @State private var isSaving = false
    @State private var message: String?

    var body: some View {
        VStack(alignment: .leading, spacing: DesignTokens.space4) {
            Text(Copy.text("mc.addTitle"))
                .font(DesignTokens.typeTitle)

            VStack(alignment: .leading, spacing: DesignTokens.space1) {
                Text(Copy.text("mc.add.nameLabel"))
                    .font(DesignTokens.typeCaption)
                    .foregroundStyle(DesignTokens.colorTextSecondary)
                TextField("", text: $name, prompt: Text(Copy.text("mc.add.namePlaceholder")))
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel(Copy.text("mc.add.nameLabel"))
                FieldHint(text: Copy.text("mc.add.nameHint"))
            }

            VStack(alignment: .leading, spacing: DesignTokens.space1) {
                Text(Copy.text("mc.add.noteLabel"))
                    .font(DesignTokens.typeCaption)
                    .foregroundStyle(DesignTokens.colorTextSecondary)
                TextField("", text: $note, prompt: Text(Copy.text("mc.add.notePlaceholder")))
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel(Copy.text("mc.add.noteLabel"))
                FieldHint(text: Copy.text("mc.add.noteHint", ["maxLength": "200"]))
            }

            if let message {
                InlineError(message: message)
            }

            HStack {
                Spacer()
                Button(Copy.text("common.cancel")) { dismiss() }
                    .keyboardShortcut(.cancelAction)
                Button(isSaving ? Copy.text("mc.adding") : Copy.text("mc.addSubmit")) {
                    Task { await submit() }
                }
                .buttonStyle(.borderedProminent)
                .keyboardShortcut(.defaultAction)
                .disabled(isSaving || !isValid)
            }
        }
        .padding(DesignTokens.space6)
        .frame(minWidth: 420)
    }

    private var isValid: Bool {
        (try? EliCloudValidation.validateMcName(name)) != nil
            && (try? EliCloudValidation.validateNote(note)) != nil
    }

    private func submit() async {
        do {
            _ = try await app.client.mc.add(name: name, note: note)
            try await app.loadMcNames(includeRemoved: false)
            dismiss()
        } catch {
            message = Copy.error(EliCloudError.normalized(error))
        }
    }
}
