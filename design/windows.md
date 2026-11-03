# 窗口 / 布局规格（桌面 + Android）

> 本文只定义**布局与窗口**；内容（字段、动作、文案、校验、错误语义）由 `contracts/` 与
> [`screens/`](screens/) 定义，四端必须一致。
>
> 第一原则仍然是**官方样式优先**：窗口 chrome、材质、导航容器、标题栏、圆角、行高都先用平台官方能力；
> 本文里的数字只在平台没有对应令牌时作为 `design/tokens.json` 的 `custom` 例外出现。

## 1. 桌面窗口（Windows / macOS / Linux）

| 项 | 值 | 说明 |
|---|---|---|
| 初始窗口 | **1252 × 800** 逻辑尺寸 | 设计稿画布；三端一致 |
| 最小窗口 | **1000 × 640** | 再小就退化为“内容滚动”，不压缩标题栏/导航 |
| 可缩放 | 是 | 宽高都可拖；内容区随宽变化，导航宽度不随内容挤压 |
| 内容最大宽度 | 内容列 **≤ 820**，页边距 **56** | 宽屏时内容居中，不把卡片拉成超宽行 |
| 标题栏高度 | **48**（Windows Tall / 等价） | 登录前后同一条，由外壳统一托管 |
| 导航宽度 | Windows **320**；macOS **220–260**；Linux **280** | 用平台官方容器，不手绘导航 |
| 内容滚动 | 正文区滚动，页头（Hero/资料头）吸顶 | 卡片/列表在滚动区内 |

### 1.1 平台 chrome

| 平台 | 窗口 | 标题栏 | 侧栏 | 材质 |
|---|---|---|---|---|
| **Windows / WinUI 3** | `AppWindow` + 单项目 MSIX | 官方 `TitleBar`（Tall 48），应用统一托管 | `NavigationView` 展开 **320** | `MicaBackdrop`；不支持时退回 `DesktopAcrylicBackdrop`，再不行用实底 |
| **macOS / SwiftUI** | 原生 `WindowGroup` / `Settings` 场景 | 原生工具栏 + 红绿灯；**不自绘** | `NavigationSplitView` sidebar **220–260** | `Material` / `NSVisualEffectView`；Liquid Glass 平台用官方玻璃 API |
| **Linux / GTK4** | `Adw.ApplicationWindow` | `Adw.HeaderBar` | `NavigationSplitView` 或 `Adw.OverlaySplitView`，**280** | Adwaita 窗口/headerbar 背景；不手写“假玻璃” |
| **Android / Compose** | `ComponentActivity` + edge-to-edge | 由 `Scaffold`/`TopAppBar` 承担 | 见 §2 断点 | M3 surface；**不伪造模糊材质** |

**不允许**：macOS 用 Windows 风格标题栏；Linux 自绘红绿灯；Android 锁横屏/锁比例；
用半透明渐变冒充 Mica/Glass/Adwaita。

## 2. Android 响应式断点

用 `WindowSizeClass`（`androidx.compose.material3.windowsizeclass`）而不是自己判宽度。

| 断点 | 宽度 | 导航 | 页头 | 内容 |
|---|---|---|---|---|
| **Compact** | < 600dp | 底部 `NavigationBar` | 大标题 + 折叠（滚动时收起） | 单列，页边距 16dp |
| **Medium** | 600–839dp | `NavigationRail` | 大标题 | 单列，页边距 24dp |
| **Expanded** | ≥ 840dp | `NavigationDrawer`（可固定） | 大标题 | 双列/更宽卡片，页边距 32dp |

- 手机竖屏 **360×800dp** 是截图基线；平板 **840×1200dp** 是第二基线。
- 最小触摸目标 **48dp**（`density.row` 在 Android 侧就是 48dp）；不要为了“和桌面一样”压到 32dp。
- 表单/弹窗在 Compact 上用全屏 `BottomSheet` 或 `Dialog`；Medium/Expanded 可用对话框。
- edge-to-edge：状态栏/导航栏区域由 `Scaffold` 的 inset 处理，内容不要被系统栏遮住。

## 3. 间距 / 密度

- 4 的栅格：`space.1=4`、`space.2=8`、`space.3=12`、`space.4=16`、`space.6=24`、`space.8=32`
  （桌面 px / Android dp；这些是 `custom` 令牌，四端没有命名间距资源）。
- 列表/设置行高：桌面 **40**（Windows NavigationView 行高）、macOS **44**、Linux **40**、Android **48dp**。
- 内容沟槽：`density.gutter = 16`。
- 卡片/控件圆角：**必须**优先 `ControlCornerRadius`（WinUI）/ `MaterialTheme.shapes.*`（M3）/
  标准控件（AppKit）/ Adwaita 样式；`radius.*` 只在平台没有令牌时使用。

## 4. 每页的布局差异

> 内容与文案 key 见 [`screens/`](screens/)；这里只说“放在哪”。

| 页面 | Windows | macOS | Linux | Android |
|---|---|---|---|---|
| **登录/注册** | 431 宽卡片，窗口水平居中、垂直居中 | 同等宽卡片，Form 风格，焦点环随系统 | `Adw.StatusPage`/居中 `Adw.Clamp` | Compact 全宽表单，页边距 16；Medium+ 居中 480 宽 |
| **验证邮箱** | 登录卡片同宽，倒计时 + 重发 + 换邮箱 | 同左，系统 sheet 提示 | 同左，`Adw.Toast` 显示重发结果 | 同左，Snackbar 代替 toast |
| **主页** | Hero 932×156 + 分组卡片列表，Nav 320 | Hero 全宽压在工具栏下，卡片用 `Form`/`List` | Hero 全宽，`Adw.PreferencesGroup` 或卡片列表 | Hero 顶部大图（16:9 裁切）+ 分组卡片；Compact 单列 |
| **个人资料** | 页头 64 头像 + banner 5:1 + 编辑弹窗 | 系统 Settings 风格表单；编辑用 sheet | `Adw.PreferencesPage`；编辑用 `Adw.Dialog` | 顶部 banner + 头像；编辑用 BottomSheet |
| **MC 服务** | 状态卡 + 名额卡 + 白名单列表；添加用 `ContentDialog` | 同左，`Form` + `.sheet` | 同左，`Adw.PreferencesPage` + `Adw.Dialog` | 同左，`LazyColumn` + `ModalBottomSheet` |
| **设置** | `SettingsCard` 列表 | 独立 `Settings` 场景 | 独立 Preferences 窗口 | 设置页 + 系统设置入口 |

## 5. 不允许的差异（四端必须一致）

- 字段、默认值、必填/可选、长度限制、校验时机与错误文案 key；
- 动作集合与顺序（主/次按钮、危险操作的确认）；
- 空状态、加载态、错误态的语义（可以换插画/图标，不可以换意思）；
- 入口地址在界面上可见（任何平台都不显示）；
- 密码/令牌落盘（任何平台都不落盘）。

## 6. 无障碍与键盘

- 每个交互控件有可读名称（Windows `AutomationProperties.Name` / macOS `.accessibilityLabel` /
  Android `contentDescription` / GTK `accessible-name`），图标按钮必须有。
- 键盘：桌面端 Tab 顺序 = 视觉顺序；Esc 关闭对话框；Enter 提交主表单；焦点环用官方样式。
- 对比度：正文 ≥ 4.5:1，大字 ≥ 3:1；高对比度模式下不依赖“颜色区分状态”，状态要带文字。
- 动效服从系统“减少动态效果”设置（Windows animation setting / macOS reduce motion / Android
  remove animations / GNOME reduce animations）。
- 主题：默认跟随系统；设置里可显式浅色/深色；四端都要有暗色与高对比度路径。
