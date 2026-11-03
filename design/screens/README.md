# 页面规格索引

> 每页一份，描述**内容与行为**；窗口/布局差异见 [`../windows.md`](../windows.md)，
> 视觉值来自 [`../tokens.json`](../tokens.json) 的官方引用，文案 key 来自
> [`../../contracts/copy/zh-CN.json`](../../contracts/copy/zh-CN.json)。

| 页面 | 规格 | 主要文案 key 前缀 |
|---|---|---|
| 登录 / 注册 | [login.md](login.md) | `login.*` |
| 验证邮箱 | [verify-email.md](verify-email.md) | `verify.*` |
| 主页 | [home.md](home.md) | `home.*`、`service.*`、`status.*` |
| 个人资料 | [profile.md](profile.md) | `profile.*` |
| MC 服务 | [mc.md](mc.md) | `mc.*` |
| 设置 | [settings.md](settings.md) | `settings.*` |

## 写页面规格的格式

每个页面必须写清：

1. **目的**：用户来这里做什么；
2. **结构**：区域、顺序；
3. **字段/动作**：标签、占位符、默认值、可用条件、动作结果；
4. **状态**：初始/加载/空/错误/成功/离线；
5. **平台差异**：只允许布局差异，不允许内容差异；
6. **无障碍**：可读名称、焦点顺序、状态播报；
7. **文案**：只引用 `contracts/copy/zh-CN.json` 的 key，不抄字符串（抄了就会漂移）。

新增页面时复制一个现有文件，并在本索引登记。
