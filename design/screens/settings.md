# 设置

## 目的

查看当前账号、切换主题、退出登录、查看版本。

## 结构

1. 页标题 `settings.title` + 说明 `settings.description`；
2. **当前账号** `settings.account.title`：
   - 已登录：显示账号标识（邮箱或用户名）+ 退出按钮 `settings.account.signOut`；
   - 未登录：`settings.account.signedOut`；
3. **外观** `settings.appearance.title` / `settings.appearance.description`：
   - 选项 `settings.appearance.light` / `settings.appearance.dark` / `settings.appearance.system`；
   - 默认 `system`；切换立即生效并持久化（只存主题，不存凭据）。
4. **关于** `settings.about.title`：`settings.about.version`（`{version}` 来自构建配置）；
   - 不显示入口地址、主机名、内部版本号以外的构建细节。

## 规则

- “退出登录”是危险动作：二次确认（或平台惯用的确认弹窗），确认后清空内存会话并回登录页。
- 设置页不提供“服务器地址”输入框、不提供“调试日志”开关。
- 主题选择持久化到本地设置文件；重启后恢复。
- 版本号来自 `design/build-config.json` 的 `appVersion`（构建时注入），不是运行时检测。

## 状态

| 状态 | 表现 |
|---|---|
| 已登录 | 显示账号 + 退出 |
| 未登录 | `settings.account.signedOut`，无退出按钮 |
| 退出中 | 按钮禁用，操作幂等 |

## 平台差异

- Windows：`SettingsCard` 列表。
- macOS：独立 `Settings` 场景（⌘,）。
- Linux：独立 Preferences 窗口（`Adw.PreferencesWindow`/`Adw.PreferencesDialog`）。
- Android：普通设置页；主题切换走系统深色模式或 App 内选择。

## 无障碍

- 主题选择是单选组，读屏能读到当前值；
- 退出确认对话框的默认焦点在“取消”；
- “关于”里的版本号可被读屏读到（不是纯装饰文本）。

## 文案 key

`settings.*`、`common.cancel`。
