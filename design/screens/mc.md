# Minecraft 服务

## 目的

查看 MC 服务器健康与白名单名额，添加/撤回自己的用户名。

## 结构

1. 页标题 `mc.title` + 说明 `mc.description`；
2. **服务器状态卡**：`mc.server.title` / `mc.server.status` / `mc.server.statusDescription`；
   - 健康：`mc.server.health`；降级：`mc.server.degraded`；RCON 不可达：`mc.server.unreachable`；
   - `mc.server.latency` + `mc.server.latencyValue`（含 `{milliseconds}`）；
   - `mc.whitelist.title` + `mc.whitelist.count`（含 `{count}`）。
3. **名额卡**：`mc.quota.title`；`mc.quota.remaining`（`{remaining}`/`{limit}`）或 `mc.quota.exhausted`。
4. **我的白名单**：`mc.names.title`；空态 `mc.names.empty`；
   - 每行：用户名（或 `mc.names.nameless`）、`mc.names.addedAt`（`{created}`）、撤回按钮
     （`mc.names.remove`，可读名称 `mc.names.removeNamed`），撤回前提示 `mc.names.removeHint`。
5. **添加**：`mc.addOpen`；表单字段 `mc.add.nameLabel`/`mc.add.namePlaceholder`/`mc.add.nameHint`，
   备注 `mc.add.noteLabel`/`mc.add.notePlaceholder`/`mc.add.noteHint`（`{maxLength}`）；
   主按钮 `mc.addSubmit`，忙碌 `mc.adding`，失败 `mc.addFailed`。

## 规则

- MC 名：`^[A-Za-z0-9_]{3,16}$`；服务端归一为小写。
- 备注：1–200 字符，不能为空。
- 添加/撤回幂等；别人的条目不显示（`not_found` 按“已经不在”处理）。
- 名额用尽时添加直接禁用，并给 `mc.quota.exceededDetail`。
- `/mc/healthz` 的 200/503 **都带完整 body**；只有其它状态码才当故障。
- `endpoint`、RCON、主机名、scope 一律不进界面。

## 状态

| 状态 | 表现 |
|---|---|
| 探活中 | `common.loading`；不改其它卡片内容 |
| 探活失败 | `mc.server.readFailed` + `error.*` 文案，不显示原始异常 |
| 白名单加载失败 | `mc.server.readFailed`，仍可看状态卡 |
| 添加成功 | 刷新列表与名额，关闭表单 |
| 添加失败 | 保留表单内容 + 错误条 |

## 平台差异

- Windows：状态卡 + `ContentDialog` 表单；列表行高 40。
- macOS：`Form` + sheet；行高 44。
- Linux：`Adw.PreferencesPage` + `Adw.Dialog`；行高 40。
- Android：`LazyColumn` + `ModalBottomSheet`；行高 48dp；触摸目标 ≥48dp。

## 无障碍

- 状态卡的可读名称包含状态文字（不能只有色点）；
- 撤回是危险动作：确认按钮文案明确（`mc.names.remove`），焦点默认在“取消”；
- 添加表单的错误与字段关联。

## 文案 key

`mc.*`、`status.*`、`common.loading`、`common.refresh`、`common.add`、`common.cancel`、`error.mc.*`、`error.client.*`。
