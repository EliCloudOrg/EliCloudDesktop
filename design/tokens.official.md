# 设计令牌 → 四端官方样式 映射表

> 唯一源是 `design/tokens.json`，本文是它的**人读视图 + 规则说明**。
> 生成表由 `python tools/gen-tokens/gen_tokens.py` 维护；`--check` 会验证本文与 JSON 一致（CI 必跑）。
>
> 第一原则：**能复用平台官方 Style / 主题资源 / 控件，就绝不手动写。**
> `tokens.json` 是“官方引用的别名 + 登记例外”，不是一套平行设计系统。

## 1. 每个平台允许用什么

| 平台 | 官方来源（优先使用） | 不允许的手写 |
|---|---|---|
| **Windows / WinUI 3** | `ThemeResource`（`TextFillColorPrimaryBrush`、`CardBackgroundFillColorDefaultBrush`、`ControlCornerRadius`…）、`SettingsCard`、`NavigationView`、`ContentDialog`、`MicaBackdrop` / `DesktopAcrylicBackdrop` | 裸 `Color.FromArgb` / `#RRGGBB`；官方已有控件时自绘卡片/按钮；手写标题栏（除非官方 `TitleBar` 表达不了） |
| **macOS / SwiftUI** | `.font(.title/.body/.caption)`、`.foregroundStyle(.primary/.secondary)`、`Material`、`.buttonStyle`、`NavigationSplitView`、`Form`/`List`、SF Symbols、标准 sheet/toolbar | 裸 `Color(red:…)` / `NSColor`；官方控件能做的自绘按钮/卡片；绕过系统材质的“假玻璃” |
| **Android / Compose M3** | `MaterialTheme.colorScheme`、`MaterialTheme.typography`、`MaterialTheme.shapes`、`CardDefaults`/`ButtonDefaults`、`NavigationSuiteScaffold`、Material Symbols | 裸 `Color(0xFF…)`；官方 M3 组件能做的自绘卡片/按钮；硬编码字号/圆角 |
| **Linux / GTK + Adwaita** | Adwaita CSS 命名颜色（`@theme_fg_color`、`@window_bg_color`、`@accent_bg_color`、`@card_bg_color`、`@borders`…）、style class（`title-1`…`title-4`、`large-title`、`dim-label`、`card`、`boxed-list`）、`Adw.HeaderBar` / `Adw.PreferencesPage` / `Adw.StatusPage` / `Adw.ToastOverlay` | 裸 `Gdk.RGBA` / hex；Adwaita 已有样式时自己写 CSS；官方 Adw 控件能做的自绘卡片 |

## 2. 令牌模型（design/tokens.json）

每个叶子是**按平台解析**的：

```json
"color": {
  "text": {
    "primary": {
      "source": "official",
      "platforms": {
        "windows": { "ref": "TextFillColorPrimaryBrush" },
        "macos":   { "ref": ".primary" },
        "android": { "ref": "MaterialTheme.colorScheme.onSurface" },
        "linux":   { "ref": "@theme_fg_color" }
      }
    }
  }
}
```

- 叶子上的字段是四端默认值，`platforms.<platform>` 覆盖它；解析后每端都必须得到 `source`。
- `source: "official"`：`ref` 必须是该端**可解析的官方引用**——资源键、SwiftUI 样式表达式、`MaterialTheme.*`、GTK CSS 变量或 style class。
  - `emit: "comment"`：官方只有机制（例如控件自带 hover、窗口圆角归系统），没有可寻址令牌；生成物只输出一行注释，不产生符号，**不要手绘**。
- `source: "custom"`：必须同时有 `value` / `reason` / `approvedBy` / `scope`，并由生成器登记到 [`EXCEPTIONS.md`](EXCEPTIONS.md)。颜色写 `#RRGGBB`，需要透明度时另写 `alpha`（0–1），生成物统一成 `#AARRGGBB`。
- 缺失 `source`、`official` 没有 `ref`、`custom` 缺任一登记字段、任一端缺失 → **生成器直接失败**。
- **ref 形态校验**：生成器按平台检查 ref 的形态（WinUI 资源键/字体族、SwiftUI 官方样式表达式、
  Compose/M3 官方引用、GTK CSS 变量或已登记的 Adwaita style class），拦住“把裸值或拼错的形态写进
  ref”。平台资源库里是否真有这个键由各端编译/运行验证，reviewer 也要对照 §2 的表。

## 3. 令牌 → 官方样式（生成，勿手改）

<!-- BEGIN GENERATED: official-map -->
| 语义键 | Windows / WinUI 3 | macOS / SwiftUI | Android / Compose M3 | Linux / GTK + Adwaita |
|---|---|---|---|---|
| `color.surface.base` | `LayerFillColorDefaultBrush` | `BackgroundStyle.background` | `MaterialTheme.colorScheme.surface` | `@window_bg_color` |
| `color.surface.raised` | `CardBackgroundFillColorDefaultBrush` | `Material.thin` | `MaterialTheme.colorScheme.surfaceContainer` | `@card_bg_color` |
| `color.surface.overlay` | `AcrylicInAppFillColorDefaultBrush` | `Material.ultraThin` | `MaterialTheme.colorScheme.surfaceContainerHigh` | `@popover_bg_color` |
| `color.text.primary` | `TextFillColorPrimaryBrush` | `HierarchicalShapeStyle.primary` | `MaterialTheme.colorScheme.onSurface` | `@theme_fg_color` |
| `color.text.secondary` | `TextFillColorSecondaryBrush` | `HierarchicalShapeStyle.secondary` | `MaterialTheme.colorScheme.onSurfaceVariant` | `dim-label` |
| `color.text.disabled` | `TextFillColorDisabledBrush` | `HierarchicalShapeStyle.tertiary` | `MaterialTheme.colorScheme.onSurface.copy(alpha = 0.38f)` | `@insensitive_fg_color` |
| `color.stroke.default` | `CardStrokeColorDefaultBrush` | `Color(nsColor: .separatorColor)` | `MaterialTheme.colorScheme.outlineVariant` | `@borders` |
| `color.stroke.strong` | `ControlStrongStrokeColorDefaultBrush` | `Color(nsColor: .gridColor)` | `MaterialTheme.colorScheme.outline` | `@shade_color` |
| `color.accent.default` | `AccentFillColorDefaultBrush` | `Color.accentColor` | `MaterialTheme.colorScheme.primary` | `@accent_bg_color` |
| `color.accent.hover` | `AccentFillColorSecondaryBrush` | `官方控件 hoverEffect`（仅标注） | `MaterialTheme.colorScheme.primary.copy(alpha = 0.08f)` | `Adwaita :hover 自动加深`（仅标注） |
| `color.status.success` | `SystemFillColorSuccessBrush` | `Color(nsColor: .systemGreen)` | custom `#FF2E7D32` | `@success_color` |
| `color.status.warning` | `SystemFillColorCautionBrush` | `Color(nsColor: .systemOrange)` | custom `#FFED6C02` | `@warning_color` |
| `color.status.danger` | `SystemFillColorCriticalBrush` | `Color(nsColor: .systemRed)` | `MaterialTheme.colorScheme.error` | `@error_color` |
| `color.status.neutral` | `SystemFillColorNeutralBrush` | `Color(nsColor: .secondaryLabelColor)` | custom `#FF757575` | `@insensitive_fg_color` |
| `type.display.family` | `Segoe UI Variable Display` | `.system`（仅标注） | `FontFamily.Default` | `Cantarell` |
| `type.display.size` | custom `68` | custom `26` | custom `57` | custom `24` |
| `type.display.weight` | custom `600` | custom `400` | custom `400` | custom `800` |
| `type.display.lineHeight` | custom `92` | custom `32` | custom `64` | custom `32` |
| `type.title` | `TitleTextBlockStyle` | `Font.title` | `MaterialTheme.typography.titleLarge` | `title-1` |
| `type.body` | `BodyTextBlockStyle` | `Font.body` | `MaterialTheme.typography.bodyLarge` | `body` |
| `type.caption` | `CaptionTextBlockStyle` | `Font.caption` | `MaterialTheme.typography.labelSmall` | `caption` |
| `type.mono` | `Cascadia Mono` | `Font.system(.body, design: .monospaced)` | `MaterialTheme.typography.bodyMedium.copy(fontFamily = FontFamily.Monospace)` | `monospace` |
| `space.1` | custom `4` | custom `4` | custom `4` | custom `4` |
| `space.2` | custom `8` | custom `8` | custom `8` | custom `8` |
| `space.3` | custom `12` | custom `12` | custom `12` | custom `12` |
| `space.4` | custom `16` | custom `16` | custom `16` | custom `16` |
| `space.6` | custom `24` | custom `24` | custom `24` | custom `24` |
| `space.8` | custom `32` | custom `32` | custom `32` | custom `32` |
| `radius.control` | `ControlCornerRadius` | custom `10` | `MaterialTheme.shapes.small` | custom `6` |
| `radius.card` | `ControlCornerRadius` | custom `12` | `MaterialTheme.shapes.medium` | custom `12` |
| `radius.window` | `Windows 11 DWM 窗口圆角`（仅标注） | `AppKit 窗口圆角`（仅标注） | `MaterialTheme.shapes.extraLarge` | custom `12` |
| `motion.fast` | `ControlFastAnimationDuration` | custom `200` | custom `200` | custom `150` |
| `motion.base` | `ControlNormalAnimationDuration` | custom `300` | custom `300` | custom `200` |
| `motion.slow` | custom `350` | custom `350` | custom `500` | custom `250` |
| `density.row` | custom `40` | custom `44` | custom `48` | custom `40` |
| `density.gutter` | custom `16` | custom `16` | custom `16` | custom `16` |
| `material.chrome` | `MicaBackdrop` | `Material.regular` | `MaterialTheme.colorScheme.surface` | `@headerbar_bg_color` |
| `brand.hero.gradient.start` | custom `#FFF9F9F9` | custom `#FFF9F9F9` | custom `#FFF9F9F9` | custom `#FFF9F9F9` |
| `brand.hero.gradient.end` | custom `#66FFA2E0` | custom `#66FFA2E0` | custom `#66FFA2E0` | custom `#66FFA2E0` |
| `brand.hero.dark.gradient.start` | custom `#FF262626` | custom `#FF262626` | custom `#FF262626` | custom `#FF262626` |
| `brand.hero.dark.gradient.mid` | custom `#BF4B3443` | custom `#BF4B3443` | custom `#BF4B3443` | custom `#BF4B3443` |
| `brand.hero.dark.gradient.end` | custom `#66FFA2E0` | custom `#66FFA2E0` | custom `#66FFA2E0` | custom `#66FFA2E0` |
| `brand.hero.scrim` | custom `#80000000` | custom `#80000000` | custom `#80000000` | custom `#80000000` |
<!-- END GENERATED: official-map -->

## 4. 说明与已知缺口

- **官方覆盖率不是 100%，而且不应该追求 100%**：`space` / `radius` / `motion` / `density` 这些数值令牌在四个平台上都没有命名资源（WinUI、SwiftUI、Compose、Adwaita 都只提供完整控件或样式，不暴露间距/时长/行高标尺）。这类值来自设计稿，已逐条登记为 `custom`，并在 [`EXCEPTIONS.md`](EXCEPTIONS.md) 写明理由。
- **颜色/文本样式/材质**：四端全部走官方引用（`color` 13 个键、`type` 的 title/body/caption/mono、`material.chrome`）。
- **Android 的 status.success / warning / neutral**：M3 `colorScheme` 没有这三个角色。先登记为扩展色（Stage 5 再按亮/暗成对定义），不复用语义错误的官方角色。
- **Windows 的 `Cascadia Mono` / `Segoe UI Variable Display`**：是 Windows 11 随附字体族名，不是手写设计值；生成物中的 `*Key` 表示“资源键或字体族名”。
- **Linux 的 style class**（`dim-label`、`title-1`、`body`…）不是颜色值：生成物里的字符串要交给 `add_css_class()`，颜色要交给 CSS 变量解析，两者不要混用。
- **`ControlFastAnimationDuration` / `ControlNormalAnimationDuration`**：WinUI 主题资源键；如果后续 WinUI 版本移除，Stage 3 应改为 `motion.*` 的登记值，而不是在页面里写数字。
- **覆盖率报告**：[`generated/official-coverage.md`](generated/official-coverage.md)。
