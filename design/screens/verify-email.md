# 验证邮箱

## 目的

等待用户点击邮件里的确认链接；确认后自动用内存中的密码登录。

## 结构

1. 产品名 + 页标题 `verify.title`；
2. 说明 `verify.sent`（含 `{email}`）；
3. 操作指引 `verify.instructions`；
4. 等待指示 `verify.waiting` + 进度条；
5. 重发按钮：`verify.resend` / 冷却时 `verify.resendCountdown`（含 `{seconds}`）；
6. 次级动作：`verify.changeEmail`（取消注册）、`verify.backToLogin`；
7. 错误/终止条：`verify.failedTitle`、`verify.terminalTitle` / `verify.terminalDetail`。

## 行为

- 进入页面立即查询一次状态，之后每 2 秒轮询；网络失败退避到最多 10 秒，并显示 `verify.networkRetry`。
- `verified` → 用内存密码登录；成功进入主页，失败回登录页并提示 `verify.confirmedSignIn`。
- `expired` / `failed`：终止轮询，显示 `verify.expired` / `verify.invalidated` / 按原因
  （`verify.usernameTaken`、`verify.emailTaken`、`verify.incomplete`），仍允许重发。
- 重发受 `resend_after` 冷却；服务端 429 时按 `Retry-After` 更新倒计时。
- “换一个邮箱” = 取消这次注册（幂等），回到注册表单；“返回登录”不做取消（服务端自然过期）。

## 状态

| 状态 | 表现 |
|---|---|
| 等待 | 进度条 + 等待文案；重发按钮按冷却禁用 |
| 忙碌 | 重发/取消禁用，显示 `common.processing` |
| 终止 | 错误条 + 可重发/返回 |
| 确认成功 | 直接离开本页 |

## 平台差异

仅容器不同：Windows/macOS 卡片居中；Linux `Adw.StatusPage`；Android Compact 全宽。

## 无障碍

- 倒计时变化不逐秒播报（避免噪音）；点击重发后播报一次结果。
- 终止状态必须同时有文字与图标，不能只靠颜色。
- 等待状态有可读名称（`verify.waiting`）。

## 文案 key

`verify.*`、`common.processing`、`error.auth.*`、`error.registration.*`、`error.transport.*`。
