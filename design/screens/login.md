# 登录 / 注册

## 目的

用用户名或邮箱登录；没有账号时发起注册（先验证邮箱，不直接建号）。

## 结构

1. 产品名（`app.productName`，`type.display`）；
2. 登录标识字段（登录：`login.usernameOrEmail.*`；注册：`login.username.*`）；
3. 注册模式下多一行邮箱字段（`login.email.*`）；
4. 密码字段（`login.password.*`）；
5. 主按钮（`login.submit` / `login.register.submit`）；
6. 次级按钮（`login.switchToRegister` / `login.switchToLogin`）；
7. 忙碌时进度条 + `login.busy` / `login.register.busy`；
8. 错误条（`login.failed` / `login.register.failed` + `errors.json` 的 copyKey）。

## 字段与规则

| 字段 | 登录 | 注册 |
|---|---|---|
| 标识 | 用户名或邮箱，trim 后非空 | 用户名，trim 后非空；**不能含 `@`**；提示 `login.username.hint` |
| 邮箱 | 不显示 | trim 后非空；只用于接收验证邮件 |
| 密码 | 非空 | 非空 |
| 提交可用 | 标识 + 密码都非空且不在忙碌中 | 三者都非空且不在忙碌中 |

- 登录失败不区分“账号不存在/密码错”，文案统一走 `error.auth.invalid_grant`。
- 注册成功后进入验证页；密码只在内存里，页面切换后原输入框清空。
- 不显示服务器地址，不提供“环境切换”。
- 记住上次登录标识（仅标识，不含密码）。

## 状态

| 状态 | 表现 |
|---|---|
| 初始 | 表单可编辑，主按钮按可用条件禁用 |
| 忙碌 | 控件禁用，显示进度与忙碌文案；重复提交被忽略 |
| 失败 | 顶部错误条；表单内容保留，密码保留（方便重试） |
| 注册成功 | 切到验证页，携带 registration_id（capability，不当用户 ID） |

## 平台差异

- Windows：431 宽卡片，窗口居中；标题栏由外壳统一托管。
- macOS：同宽卡片；Enter 提交；Tab 顺序 = 视觉顺序。
- Linux：`Adw.Clamp` 居中；错误用 `Adw.Banner` 或错误行。
- Android：Compact 全宽 + 16dp 边距；键盘弹起时表单可滚动；IME action 为“下一步/完成”。

## 无障碍

- 每个输入框有可读名称（标签文本，而不是占位符）；
- 密码框不朗读内容；错误条用 `aria-live` / `AccessibilityLiveRegion` / 对应平台机制播报一次；
- 焦点进入页面时落在标识字段（或上次标识已填时落在密码）。

## 文案 key

`login.*`、`app.productName`、`error.auth.*`、`error.transport.*`、`error.client.*`。
