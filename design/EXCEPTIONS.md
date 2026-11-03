# 例外登记（官方样式优先）

> 规则：**官方已有 Style / 主题资源 / 控件时，不允许手写。** 唯一允许的两类例外是：
>
> 1. **品牌资产**：设计稿明确给出的 Hero 渐变、品牌粉、插画、Logo 等，平台没有对应语义令牌；
> 2. **平台缺控件/缺样式**：例如自绘裁切器、特定 Hero 布局、官方确实没有的组件。
>
> 没有登记的裸值一律视为违规，`tools/audit-official-styles` 会在 CI 里失败。

本文有两部分：

- **§1 令牌例外**：由 `design/tokens.json` 的 `source: "custom"` 自动生成（运行 `python tools/gen-tokens/gen_tokens.py`）；
- **§2 代码例外**：人手维护的允许清单，审计工具按 `file` + `line` + `match` 精确匹配。

## 1. 令牌例外（生成，勿手改）

<!-- BEGIN GENERATED: token-exceptions -->
| 语义键 | 平台 | 值 | 理由 | 批准人 | 作用域 |
|---|---|---|---|---|---|
| `color.status.success` | android | `#FF2E7D32` | M3 colorScheme 没有 success 角色；先按 Material 绿色取一个扩展色，Stage 5 再按亮/暗成对定义 | design-owner | `status.extended` |
| `color.status.warning` | android | `#FFED6C02` | M3 colorScheme 没有 warning 角色；先按 Material 橙色取一个扩展色，Stage 5 再按亮/暗成对定义 | design-owner | `status.extended` |
| `color.status.neutral` | android | `#FF757575` | M3 colorScheme 没有 neutral 状态色；先按 Material 灰取一个扩展色，Stage 5 再按亮/暗成对定义 | design-owner | `status.extended` |
| `type.display.size` | windows | `68` | 官方文本样式不暴露单独的字号令牌；68 是 WinUI DisplayTextBlockStyle 的字号，见 tokens.official.md | design-owner | `type.display` |
| `type.display.size` | macos | `26` | 官方文本样式不暴露单独的字号令牌；26 是 macOS largeTitle 的字号 | design-owner | `type.display` |
| `type.display.size` | android | `57` | 官方文本样式不暴露单独的字号令牌；57 是 M3 displayLarge 的字号 | design-owner | `type.display` |
| `type.display.size` | linux | `24` | 官方文本样式不暴露单独的字号令牌；24 是 Adwaita large-title 的字号 | design-owner | `type.display` |
| `type.display.weight` | windows | `600` | 官方文本样式不暴露单独的字重令牌；600 是 WinUI DisplayTextBlockStyle 的 Semibold | design-owner | `type.display` |
| `type.display.weight` | macos | `400` | 官方文本样式不暴露单独的字重令牌；400 是 macOS largeTitle 的 Regular | design-owner | `type.display` |
| `type.display.weight` | android | `400` | 官方文本样式不暴露单独的字重令牌；400 是 M3 displayLarge 的 Regular | design-owner | `type.display` |
| `type.display.weight` | linux | `800` | 官方文本样式不暴露单独的字重令牌；800 是 Adwaita large-title 的 ExtraBold | design-owner | `type.display` |
| `type.display.lineHeight` | windows | `92` | 官方文本样式不暴露单独的行高令牌；92 是 WinUI DisplayTextBlockStyle 的行高 | design-owner | `type.display` |
| `type.display.lineHeight` | macos | `32` | 官方文本样式不暴露单独的行高令牌；32 是 macOS largeTitle 的建议行高 | design-owner | `type.display` |
| `type.display.lineHeight` | android | `64` | 官方文本样式不暴露单独的行高令牌；64 是 M3 displayLarge 的行高 | design-owner | `type.display` |
| `type.display.lineHeight` | linux | `32` | 官方文本样式不暴露单独的行高令牌；32 是 Adwaita large-title 的建议行高 | design-owner | `type.display` |
| `space.1` | windows | `4` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.1` | macos | `4` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.1` | android | `4` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.1` | linux | `4` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.2` | windows | `8` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.2` | macos | `8` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.2` | android | `8` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.2` | linux | `8` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.3` | windows | `12` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.3` | macos | `12` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.3` | android | `12` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.3` | linux | `12` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.4` | windows | `16` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.4` | macos | `16` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.4` | android | `16` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.4` | linux | `16` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.6` | windows | `24` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.6` | macos | `24` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.6` | android | `24` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.6` | linux | `24` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.8` | windows | `32` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.8` | macos | `32` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.8` | android | `32` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `space.8` | linux | `32` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 | design-owner | `layout.spacing` |
| `radius.control` | macos | `10` | AppKit 标准控件圆角随系统变化，没有命名令牌；取 10 近似标准控件 | design-owner | `control.chrome` |
| `radius.control` | linux | `6` | macOS/GTK 没有命名控件圆角令牌；桌面沿用官方控件的近似值 | design-owner | `control.chrome` |
| `radius.card` | macos | `12` | macOS/GTK 没有命名卡片圆角令牌；用 12 与官方卡片观感对齐 | design-owner | `surface.card` |
| `radius.card` | linux | `12` | macOS/GTK 没有命名卡片圆角令牌；用 12 与官方卡片观感对齐 | design-owner | `surface.card` |
| `radius.window` | linux | `12` | GTK4/Adwaita 窗口圆角由主题决定、没有可读令牌；取 12 与 Adwaita 窗口观感对齐 | design-owner | `window.chrome` |
| `motion.fast` | macos | `200` | SwiftUI 没有命名时长令牌；按 macOS HIG 取 200ms | design-owner | `motion.feedback` |
| `motion.fast` | android | `200` | M3 规范 short4=200ms，但 Compose 未暴露命名令牌 | design-owner | `motion.feedback` |
| `motion.fast` | linux | `150` | libadwaita 的时长写在样式表里，没有可读令牌；取 150ms | design-owner | `motion.feedback` |
| `motion.base` | macos | `300` | SwiftUI 没有命名时长令牌；按 macOS HIG 取 300ms | design-owner | `motion.feedback` |
| `motion.base` | android | `300` | M3 规范 medium2=300ms，但 Compose 未暴露命名令牌 | design-owner | `motion.feedback` |
| `motion.base` | linux | `200` | libadwaita 的时长写在样式表里，没有可读令牌；取 200ms | design-owner | `motion.feedback` |
| `motion.slow` | windows | `350` | Fluent 慢速动效时长；四端都没有可寻址的命名令牌 | design-owner | `motion.feedback` |
| `motion.slow` | macos | `350` | SwiftUI 没有命名时长令牌；按 macOS HIG 取 350ms | design-owner | `motion.feedback` |
| `motion.slow` | android | `500` | M3 规范 long2=500ms，但 Compose 未暴露命名令牌 | design-owner | `motion.feedback` |
| `motion.slow` | linux | `250` | libadwaita 的时长写在样式表里，没有可读令牌；取 250ms | design-owner | `motion.feedback` |
| `density.row` | windows | `40` | 四端都没有命名行高令牌；桌面沿用 WinUI NavigationView 的 40px 行高，Android 用 48dp 触摸目标 | design-owner | `density.list` |
| `density.row` | macos | `44` | 四端都没有命名行高令牌；macOS HIG 建议 44pt 行高 | design-owner | `density.list` |
| `density.row` | android | `48` | M3 官方最小触摸目标 48dp，但没有命名令牌 | design-owner | `density.list` |
| `density.row` | linux | `40` | 四端都没有命名行高令牌；Adwaita 列表行取 40px | design-owner | `density.list` |
| `density.gutter` | windows | `16` | 四端都没有命名沟槽令牌；采用 16 的统一内容间隔 | design-owner | `layout.spacing` |
| `density.gutter` | macos | `16` | 四端都没有命名沟槽令牌；采用 16 的统一内容间隔 | design-owner | `layout.spacing` |
| `density.gutter` | android | `16` | 四端都没有命名沟槽令牌；采用 16 的统一内容间隔 | design-owner | `layout.spacing` |
| `density.gutter` | linux | `16` | 四端都没有命名沟槽令牌；采用 16 的统一内容间隔 | design-owner | `layout.spacing` |
| `brand.hero.gradient.start` | windows | `#FFF9F9F9` | 设计稿品牌渐变；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.gradient.start` | macos | `#FFF9F9F9` | 设计稿品牌渐变；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.gradient.start` | android | `#FFF9F9F9` | 设计稿品牌渐变；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.gradient.start` | linux | `#FFF9F9F9` | 设计稿品牌渐变；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.gradient.end` | windows | `#66FFA2E0` | 设计稿品牌渐变终点色（40% 不透明度）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.gradient.end` | macos | `#66FFA2E0` | 设计稿品牌渐变终点色（40% 不透明度）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.gradient.end` | android | `#66FFA2E0` | 设计稿品牌渐变终点色（40% 不透明度）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.gradient.end` | linux | `#66FFA2E0` | 设计稿品牌渐变终点色（40% 不透明度）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.start` | windows | `#FF262626` | 设计稿深色 Hero 渐变；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.start` | macos | `#FF262626` | 设计稿深色 Hero 渐变；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.start` | android | `#FF262626` | 设计稿深色 Hero 渐变；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.start` | linux | `#FF262626` | 设计稿深色 Hero 渐变；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.mid` | windows | `#BF4B3443` | 设计稿深色 Hero 渐变中段（75%）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.mid` | macos | `#BF4B3443` | 设计稿深色 Hero 渐变中段（75%）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.mid` | android | `#BF4B3443` | 设计稿深色 Hero 渐变中段（75%）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.mid` | linux | `#BF4B3443` | 设计稿深色 Hero 渐变中段（75%）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.end` | windows | `#66FFA2E0` | 设计稿深色 Hero 渐变终点（40%）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.end` | macos | `#66FFA2E0` | 设计稿深色 Hero 渐变终点（40%）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.end` | android | `#66FFA2E0` | 设计稿深色 Hero 渐变终点（40%）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.dark.gradient.end` | linux | `#66FFA2E0` | 设计稿深色 Hero 渐变终点（40%）；平台无对应语义令牌 | design-owner | `home.hero` |
| `brand.hero.scrim` | windows | `#80000000` | 设计稿 Hero 文字压暗层（50%）；品牌布局的一部分，平台无对应令牌 | design-owner | `home.hero` |
| `brand.hero.scrim` | macos | `#80000000` | 设计稿 Hero 文字压暗层（50%）；品牌布局的一部分，平台无对应令牌 | design-owner | `home.hero` |
| `brand.hero.scrim` | android | `#80000000` | 设计稿 Hero 文字压暗层（50%）；品牌布局的一部分，平台无对应令牌 | design-owner | `home.hero` |
| `brand.hero.scrim` | linux | `#80000000` | 设计稿 Hero 文字压暗层（50%）；品牌布局的一部分，平台无对应令牌 | design-owner | `home.hero` |
<!-- END GENERATED: token-exceptions -->

## 2. 代码例外（手工维护）

```json
{
  "version": 1,
  "entries": [
    {
      "rule": "naked-color",
      "file": "src/EliCloudDesktop/Ui/Widgets.cs",
      "line": 490,
      "match": "Color.FromArgb(0x80",
      "token": "brand.hero.scrim",
      "reason": "设计稿 Hero 文字压暗层（50% 黑）；品牌布局的一部分，平台无对应语义令牌",
      "approvedBy": "design-owner",
      "scope": "home.hero",
      "expiry": "2027-06-30"
    },
    {
      "rule": "naked-color",
      "file": "src/EliCloudDesktop/Ui/Widgets.cs",
      "line": 517,
      "match": "Color.FromArgb(0xFF, 0xF9, 0xF9, 0xF9)",
      "token": "brand.hero.gradient.start",
      "reason": "设计稿浅色 Hero 渐变起点；品牌资产",
      "approvedBy": "design-owner",
      "scope": "home.hero",
      "expiry": "2027-06-30"
    },
    {
      "rule": "naked-color",
      "file": "src/EliCloudDesktop/Ui/Widgets.cs",
      "line": 522,
      "match": "Color.FromArgb(0x66, 0xFF, 0xA2, 0xE0)",
      "token": "brand.hero.gradient.end",
      "reason": "设计稿浅色 Hero 渐变终点（40% 粉）；品牌资产",
      "approvedBy": "design-owner",
      "scope": "home.hero",
      "expiry": "2027-06-30"
    },
    {
      "rule": "naked-color",
      "file": "src/EliCloudDesktop/Ui/Widgets.cs",
      "line": 537,
      "match": "Color.FromArgb(0xFF, 0x26, 0x26, 0x26)",
      "token": "brand.hero.dark.gradient.start",
      "reason": "设计稿深色 Hero 渐变起点；品牌资产",
      "approvedBy": "design-owner",
      "scope": "home.hero",
      "expiry": "2027-06-30"
    },
    {
      "rule": "naked-color",
      "file": "src/EliCloudDesktop/Ui/Widgets.cs",
      "line": 542,
      "match": "Color.FromArgb(0xBF, 0x4B, 0x34, 0x43)",
      "token": "brand.hero.dark.gradient.mid",
      "reason": "设计稿深色 Hero 渐变中段（75%）；品牌资产",
      "approvedBy": "design-owner",
      "scope": "home.hero",
      "expiry": "2027-06-30"
    },
    {
      "rule": "naked-color",
      "file": "src/EliCloudDesktop/Ui/Widgets.cs",
      "line": 547,
      "match": "Color.FromArgb(0x66, 0xFF, 0xA2, 0xE0)",
      "token": "brand.hero.dark.gradient.end",
      "reason": "设计稿深色 Hero 渐变终点（40% 粉）；品牌资产",
      "approvedBy": "design-owner",
      "scope": "home.hero",
      "expiry": "2027-06-30"
    }
  ]
}
```

### 2.1 怎么加一条代码例外

1. 先确认官方真的没有：查 `tokens.official.md` 的映射表与平台文档；
2. 在 `tokens.json` 里加 `source: "custom"` 的令牌（含 `reason/approvedBy/scope`），跑生成器，把令牌登记进 §1；
3. 在 §2 的 JSON 里加一条：`rule`（规则名见 `tools/audit-official-styles/audit.py` 的 `RULES`）、`file`、`line`、`match`（该行必须包含的片段）、`reason`、`approvedBy`、`scope`、`expiry`；
4. 跑 `python tools/audit-official-styles/audit.py` 确认识别；
5. `expiry` 到期后，例外自动失效（审计会重新报违规），必须清理或重新批准。

### 2.2 存量违规

Stage 1 的 Windows 代码里有历史裸值（例如 `Ui/DesignTokens.cs` 的字号/行高、`NavFooterProfileItem` 的内联字号、`ProfilePage` 的圆角）。这些**不是批准的新例外**，而是迁移债务：

- 记录在 `tools/audit-official-styles/baseline/*.json`，只允许减少、不允许增加；
- 每减少一处，运行 `--update-baseline` 收紧基线；
- Stage 3（Windows parity）负责把它们迁到 `design/tokens.json` 的官方引用或登记例外。

## 3. Code review checklist（新增 UI 时）

- [ ] 先问「官方有没有？」——查 `tokens.official.md`、平台文档/示例，再决定写代码；
- [ ] 官方控件能做的（卡片/按钮/标题栏/列表行/表单/材质），不手绘；
- [ ] 颜色/字号/圆角/字体族没有裸值：来自生成物或 §1 令牌；
- [ ] 动效时长来自 `motion.*`，不写 magic number；
- [ ] 确需手写：§1 有对应令牌、§2 有 `file/line/match` 例外，且有 `expiry`；
- [ ] 跑一遍 `python tools/audit-official-styles/audit.py --platform <platform>`，没有新增违规；
- [ ] 没有为了「看起来统一」而破坏平台 HIG（标题栏、导航、密度、触摸目标）。
