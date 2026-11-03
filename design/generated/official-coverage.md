# 官方来源覆盖率（from design/tokens.json）

> 由 `python tools/gen-tokens/gen_tokens.py` 生成，不要手改。

## 汇总

- 语义键：43 个；平台取值条目：172 个
- 官方来源：86 个（50.0%），其中 5 个是“官方只有机制/无可寻址令牌”的标注
- 登记例外（custom）：86 个（50.0%）

## 按类别

| 类别 | 条目 | 官方 | custom | 官方占比 |
|---|---|---|---|---|
| color | 56 | 53 | 3 | 94.6% |
| type | 32 | 20 | 12 | 62.5% |
| space | 24 | 0 | 24 | 0.0% |
| radius | 12 | 7 | 5 | 58.3% |
| motion | 12 | 2 | 10 | 16.7% |
| density | 8 | 0 | 8 | 0.0% |
| material | 4 | 4 | 0 | 100.0% |
| brand | 24 | 0 | 24 | 0.0% |

## 按平台

| 平台 | 条目 | 官方 | custom | 官方占比 |
|---|---|---|---|---|
| windows | 43 | 25 | 18 | 58.1% |
| macos | 43 | 21 | 22 | 48.8% |
| android | 43 | 20 | 23 | 46.5% |
| linux | 43 | 20 | 23 | 46.5% |

## 登记例外（custom）

| 语义键 | 平台 | 作用域 | 理由 |
|---|---|---|---|
| `color.status.success` | android | `status.extended` | M3 colorScheme 没有 success 角色；先按 Material 绿色取一个扩展色，Stage 5 再按亮/暗成对定义 |
| `color.status.warning` | android | `status.extended` | M3 colorScheme 没有 warning 角色；先按 Material 橙色取一个扩展色，Stage 5 再按亮/暗成对定义 |
| `color.status.neutral` | android | `status.extended` | M3 colorScheme 没有 neutral 状态色；先按 Material 灰取一个扩展色，Stage 5 再按亮/暗成对定义 |
| `type.display.size` | windows | `type.display` | 官方文本样式不暴露单独的字号令牌；68 是 WinUI DisplayTextBlockStyle 的字号，见 tokens.official.md |
| `type.display.size` | macos | `type.display` | 官方文本样式不暴露单独的字号令牌；26 是 macOS largeTitle 的字号 |
| `type.display.size` | android | `type.display` | 官方文本样式不暴露单独的字号令牌；57 是 M3 displayLarge 的字号 |
| `type.display.size` | linux | `type.display` | 官方文本样式不暴露单独的字号令牌；24 是 Adwaita large-title 的字号 |
| `type.display.weight` | windows | `type.display` | 官方文本样式不暴露单独的字重令牌；600 是 WinUI DisplayTextBlockStyle 的 Semibold |
| `type.display.weight` | macos | `type.display` | 官方文本样式不暴露单独的字重令牌；400 是 macOS largeTitle 的 Regular |
| `type.display.weight` | android | `type.display` | 官方文本样式不暴露单独的字重令牌；400 是 M3 displayLarge 的 Regular |
| `type.display.weight` | linux | `type.display` | 官方文本样式不暴露单独的字重令牌；800 是 Adwaita large-title 的 ExtraBold |
| `type.display.lineHeight` | windows | `type.display` | 官方文本样式不暴露单独的行高令牌；92 是 WinUI DisplayTextBlockStyle 的行高 |
| `type.display.lineHeight` | macos | `type.display` | 官方文本样式不暴露单独的行高令牌；32 是 macOS largeTitle 的建议行高 |
| `type.display.lineHeight` | android | `type.display` | 官方文本样式不暴露单独的行高令牌；64 是 M3 displayLarge 的行高 |
| `type.display.lineHeight` | linux | `type.display` | 官方文本样式不暴露单独的行高令牌；32 是 Adwaita large-title 的建议行高 |
| `space.1` | windows | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.1` | macos | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.1` | android | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.1` | linux | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.2` | windows | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.2` | macos | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.2` | android | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.2` | linux | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.3` | windows | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.3` | macos | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.3` | android | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.3` | linux | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.4` | windows | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.4` | macos | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.4` | android | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.4` | linux | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.6` | windows | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.6` | macos | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.6` | android | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.6` | linux | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.8` | windows | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.8` | macos | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.8` | android | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `space.8` | linux | `layout.spacing` | 四端都没有命名间距令牌；采用 4px 网格，见 design/windows.md §2 |
| `radius.control` | macos | `control.chrome` | AppKit 标准控件圆角随系统变化，没有命名令牌；取 10 近似标准控件 |
| `radius.control` | linux | `control.chrome` | macOS/GTK 没有命名控件圆角令牌；桌面沿用官方控件的近似值 |
| `radius.card` | macos | `surface.card` | macOS/GTK 没有命名卡片圆角令牌；用 12 与官方卡片观感对齐 |
| `radius.card` | linux | `surface.card` | macOS/GTK 没有命名卡片圆角令牌；用 12 与官方卡片观感对齐 |
| `radius.window` | linux | `window.chrome` | GTK4/Adwaita 窗口圆角由主题决定、没有可读令牌；取 12 与 Adwaita 窗口观感对齐 |
| `motion.fast` | macos | `motion.feedback` | SwiftUI 没有命名时长令牌；按 macOS HIG 取 200ms |
| `motion.fast` | android | `motion.feedback` | M3 规范 short4=200ms，但 Compose 未暴露命名令牌 |
| `motion.fast` | linux | `motion.feedback` | libadwaita 的时长写在样式表里，没有可读令牌；取 150ms |
| `motion.base` | macos | `motion.feedback` | SwiftUI 没有命名时长令牌；按 macOS HIG 取 300ms |
| `motion.base` | android | `motion.feedback` | M3 规范 medium2=300ms，但 Compose 未暴露命名令牌 |
| `motion.base` | linux | `motion.feedback` | libadwaita 的时长写在样式表里，没有可读令牌；取 200ms |
| `motion.slow` | windows | `motion.feedback` | Fluent 慢速动效时长；四端都没有可寻址的命名令牌 |
| `motion.slow` | macos | `motion.feedback` | SwiftUI 没有命名时长令牌；按 macOS HIG 取 350ms |
| `motion.slow` | android | `motion.feedback` | M3 规范 long2=500ms，但 Compose 未暴露命名令牌 |
| `motion.slow` | linux | `motion.feedback` | libadwaita 的时长写在样式表里，没有可读令牌；取 250ms |
| `density.row` | windows | `density.list` | 四端都没有命名行高令牌；桌面沿用 WinUI NavigationView 的 40px 行高，Android 用 48dp 触摸目标 |
| `density.row` | macos | `density.list` | 四端都没有命名行高令牌；macOS HIG 建议 44pt 行高 |
| `density.row` | android | `density.list` | M3 官方最小触摸目标 48dp，但没有命名令牌 |
| `density.row` | linux | `density.list` | 四端都没有命名行高令牌；Adwaita 列表行取 40px |
| `density.gutter` | windows | `layout.spacing` | 四端都没有命名沟槽令牌；采用 16 的统一内容间隔 |
| `density.gutter` | macos | `layout.spacing` | 四端都没有命名沟槽令牌；采用 16 的统一内容间隔 |
| `density.gutter` | android | `layout.spacing` | 四端都没有命名沟槽令牌；采用 16 的统一内容间隔 |
| `density.gutter` | linux | `layout.spacing` | 四端都没有命名沟槽令牌；采用 16 的统一内容间隔 |
| `brand.hero.gradient.start` | windows | `home.hero` | 设计稿品牌渐变；平台无对应语义令牌 |
| `brand.hero.gradient.start` | macos | `home.hero` | 设计稿品牌渐变；平台无对应语义令牌 |
| `brand.hero.gradient.start` | android | `home.hero` | 设计稿品牌渐变；平台无对应语义令牌 |
| `brand.hero.gradient.start` | linux | `home.hero` | 设计稿品牌渐变；平台无对应语义令牌 |
| `brand.hero.gradient.end` | windows | `home.hero` | 设计稿品牌渐变终点色（40% 不透明度）；平台无对应语义令牌 |
| `brand.hero.gradient.end` | macos | `home.hero` | 设计稿品牌渐变终点色（40% 不透明度）；平台无对应语义令牌 |
| `brand.hero.gradient.end` | android | `home.hero` | 设计稿品牌渐变终点色（40% 不透明度）；平台无对应语义令牌 |
| `brand.hero.gradient.end` | linux | `home.hero` | 设计稿品牌渐变终点色（40% 不透明度）；平台无对应语义令牌 |
| `brand.hero.dark.gradient.start` | windows | `home.hero` | 设计稿深色 Hero 渐变；平台无对应语义令牌 |
| `brand.hero.dark.gradient.start` | macos | `home.hero` | 设计稿深色 Hero 渐变；平台无对应语义令牌 |
| `brand.hero.dark.gradient.start` | android | `home.hero` | 设计稿深色 Hero 渐变；平台无对应语义令牌 |
| `brand.hero.dark.gradient.start` | linux | `home.hero` | 设计稿深色 Hero 渐变；平台无对应语义令牌 |
| `brand.hero.dark.gradient.mid` | windows | `home.hero` | 设计稿深色 Hero 渐变中段（75%）；平台无对应语义令牌 |
| `brand.hero.dark.gradient.mid` | macos | `home.hero` | 设计稿深色 Hero 渐变中段（75%）；平台无对应语义令牌 |
| `brand.hero.dark.gradient.mid` | android | `home.hero` | 设计稿深色 Hero 渐变中段（75%）；平台无对应语义令牌 |
| `brand.hero.dark.gradient.mid` | linux | `home.hero` | 设计稿深色 Hero 渐变中段（75%）；平台无对应语义令牌 |
| `brand.hero.dark.gradient.end` | windows | `home.hero` | 设计稿深色 Hero 渐变终点（40%）；平台无对应语义令牌 |
| `brand.hero.dark.gradient.end` | macos | `home.hero` | 设计稿深色 Hero 渐变终点（40%）；平台无对应语义令牌 |
| `brand.hero.dark.gradient.end` | android | `home.hero` | 设计稿深色 Hero 渐变终点（40%）；平台无对应语义令牌 |
| `brand.hero.dark.gradient.end` | linux | `home.hero` | 设计稿深色 Hero 渐变终点（40%）；平台无对应语义令牌 |
| `brand.hero.scrim` | windows | `home.hero` | 设计稿 Hero 文字压暗层（50%）；品牌布局的一部分，平台无对应令牌 |
| `brand.hero.scrim` | macos | `home.hero` | 设计稿 Hero 文字压暗层（50%）；品牌布局的一部分，平台无对应令牌 |
| `brand.hero.scrim` | android | `home.hero` | 设计稿 Hero 文字压暗层（50%）；品牌布局的一部分，平台无对应令牌 |
| `brand.hero.scrim` | linux | `home.hero` | 设计稿 Hero 文字压暗层（50%）；品牌布局的一部分，平台无对应令牌 |
