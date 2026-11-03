# 个人资料

## 目的

查看账号事实，编辑昵称、简介、头像、头图。

## 结构

1. **页头**：头像（64）+ 昵称（`type.title`）+ 邮箱（`type.body`，次要色）+ 编辑按钮 `profile.edit.open`；
   - 有头图时显示 5:1 banner；没有时退回普通页头（粉色渐变只属于主页 Hero）。
   - 昵称为空时用用户名兜底（展示与编辑一致）。
2. **正文**：只读账号事实：`profile.username`、`profile.email`、`profile.emailStatus`、`profile.loginStatus`；
   - 邮箱状态：`status.emailVerified` / `status.emailUnverified` / `status.emailUnbound` / `status.emailPending`；
   - 简介为空显示 `profile.emptyBio`。
3. **编辑弹窗/sheet**：头像（`profile.edit.avatar` / `profile.edit.change`）、头图
   （`profile.edit.banner` / `profile.edit.changeBanner` / `profile.edit.removeBanner`）、
   昵称（`profile.edit.nickname*`）、简介（`profile.edit.bio*`）、保存/取消。

## 字段与校验

| 字段 | 规则 | 错误文案 key |
|---|---|---|
| 昵称 | trim + NFC；1–32 个码点；禁止控制/格式/代理/私用/未分配字符；不能换行；默认用户名 | `error.profile.invalid_nickname` |
| 简介 | ≤500 字符；纯文本；拒绝控制字符 | `error.profile.invalid_bio` |
| 头像 | PNG/JPEG/WebP；≤5 MB；裁成 1:1，缩到 512×512 内重编码 PNG | `error.profile.*image*` |
| 头图 | PNG/JPEG/WebP；≤10 MB；按 5:1 裁，缩到 1600 宽内重编码 PNG | `error.profile.*image*` |

- 保存顺序：**图片先、文字后**；部分成功时刷新服务端快照，只提交成功的那部分。
- 取消 = 丢弃所有未保存预览。
- 昵称不合法时“保存”直接禁用（`common.save`），不要提交后才提示。
- 不显示 `sub`、`scope`、令牌等术语。

## 状态

| 状态 | 表现 |
|---|---|
| 加载中 | 骨架屏或 `common.loading`；不闪空资料 |
| 无资料 | 展示用户名/邮箱兜底，不报错 |
| 保存中 | `common.saving`，按钮禁用 |
| 保存失败 | 错误条 + 保留已填内容 |
| 图片读取/裁切失败 | 页面内提示，不回退到“保存成功” |

## 平台差异

- Windows：页头 banner 5:1，编辑用 `ContentDialog` + Toolkit `ImageCropper`。
- macOS：`Form` 风格；编辑用 sheet；裁剪用系统图片编辑体验或等价控件。
- Linux：`Adw.PreferencesPage`；编辑用 `Adw.Dialog`。
- Android：顶部 banner + 头像；编辑用 `ModalBottomSheet`；图片选择用系统 Photo Picker。

## 无障碍

- 头像有可读名称（“头像，<昵称>”）；
- 裁剪选区的拖动手柄必须能用键盘移动（或提供数值/预设替代）；
- 保存失败后焦点回到错误来源字段。

## 文案 key

`profile.*`、`status.*`、`common.save`、`common.saving`、`common.cancel`、`error.profile.*`。
