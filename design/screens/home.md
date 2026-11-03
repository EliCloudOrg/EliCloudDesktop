# 主页

## 目的

一眼看到所有服务，以及每个服务当前能不能用；能用的直接进入。

## 结构

1. **Hero**：产品名 `home.hero.title` + 说明 `home.hero.description`，压在品牌横幅上；
2. **分组**（每组 = 标题 + 说明 + 卡片列表）：
   - 新服务：`home.section.new.*`
   - 正在工作：`home.section.working.*`
   - 停止服务：`home.section.stopped.*`
3. **服务卡片**：图标 + `service.<id>.title` + `service.<id>.description` + 状态文案 + 可选“打开”；
4. 加载中：`home.loading`；空：`home.empty`。

## 分组规则

| 服务状态 | 分组 | 卡片右侧 |
|---|---|---|
| `Available` | 正在工作 | 状态文案 + 动作（打开） |
| `ComingSoon` | 新服务 | 状态文案 |
| `Maintenance` / `Unavailable` | 停止服务 | 状态文案 |
| `Unknown`（尚未探完） | 不显示 | — |

- **整组为空时连表头一起不渲染**（不要留空标题）。
- 状态来自真实探测（MC 走 `/mc/healthz`，8 秒超时）；不能把“还没探测”当成“不可用”。
- 卡片点击 = 打开；卡片本身不是按钮时，动作必须仍是键盘可达的。

## 状态

| 状态 | 表现 |
|---|---|
| 探活中 | 顶部 `home.loading` + 进度；不把服务归到“停止服务” |
| 全部可用/部分不可用 | 按分组渲染 |
| 全不可用 | 只渲染“停止服务”组 |
| 空 | `home.empty` |

## 平台差异

- Windows：Hero 932×156，粉色渐变按 `brand.hero.*` 令牌；Nav 320。
- macOS：Hero 全宽压在原生工具栏下；卡片用 `List`/`Form` 风格。
- Linux：Hero 全宽；分组用 `Adw.PreferencesGroup` 或官方卡片。
- Android：Hero 16:9 裁切，顶部大标题可折叠；Compact 单列，Expanded 可双列。

## 无障碍

- 卡片的状态必须能被读屏读到（`status.serviceStatus` 等价的可读名称，不能只给色点）。
- 分组标题是 heading 级语义（平台对应 API）。
- Hero 图片是装饰：不重复朗读标题里已有的信息。

## 文案 key

`home.*`、`service.*`、`status.*`、`common.open`。
