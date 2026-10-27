# EliCloud 桌面客户端 · 设计规格（从 Figma 设计稿反解）

> 设计源文件：`E:\Download\EliCloudDesktopUI.fig`
> 本文件是**实现依据**：客户端里的每一个字号、间距、文案都能在这里找到出处。

---

## 0. 这份规格是怎么来的

`.fig` 是 Figma 的导出容器，不是图片包。它本身是一个 zip，里面最关键的是 `canvas.fig` ——
一份 **fig-kiwi** 二进制（kiwi 是 Figma 自研的 schema 驱动二进制格式）：

```text
canvas.fig
├── "fig-kiwi"            magic（8 字节）
├── uint32 LE version     = 101
├── uint32 LE size + data chunk 0   # kiwi schema，raw deflate 压缩
├── uint32 LE size + data chunk 1   # kiwi message，zstd 压缩
└── ...
```

解出来的 message 里有 8914 个节点（`nodeChanges`）与 765 个二进制 blob。
本规格就是遍历这棵树、按 `parentIndex` 还原层次后逐节点读出来的
（字体、字号、行高、颜色、圆角、内边距、autolayout 全部来自节点原始属性，不是目测）。

解出来的三个页面本身就是设计组件库的实例 —— 稿子建立在 **Windows 11 官方设计组件库**之上
（节点名形如 `Mode=Light, Layout=Icon Before, …State=Rest`、`Surface / App Surface`、
`Blink / Mica`），所以下面的令牌表能一一对到 WinUI 的主题资源键。

---

## 1. 画布

| 项 | 值 |
|---|---|
| 应用窗口 | **1252 × 800** |
| 自定义标题栏 | 高 **48**，左右内边距 **8** |
| 窗口底色 | Mica（稿：`Surface / App Surface` + `Blink / Mica`，`#F3F3F3` 系） |
| 内容区 | 1252 × 752（标题栏以下） |

三个页面是同一组件 `App window` 的三个实例，在画布上纵向排列：

| # | 页面 | 实例内主体 |
|---|---|---|
| 1 | 登录页 | `Frame 1` 431 × 324 |
| 2 | 主页 | `Nav + Content` 1252 × 752 |
| 3 | 个人资料页 | `Nav + Content` 1252 × 752 |

---

## 2. 页面规格

### 2.1 登录页

```
App window 1252×800
├─ Title Bar              1252×48   padH=8   「EliCloud Desktop」
└─ Frame 1                431×324   @x=410  （水平居中；垂直居内容区）
   ├─ TEXT "EliCloud"     255×92    Segoe UI Variable / Semibold Display
   │                                fs=68  lh=92  ls=0  TextFillColorPrimary
   └─ Frame 2             431×232   VERTICAL  gap=16  居中对齐
      ├─ Text Box / Text Box      431×60   Header「用户名」  Placeholder「输入用户名或邮箱」
      ├─ Password Box / Password  431×60   Header「密码」    Placeholder「填入密码」
      ├─ Button / Button          431×32   Style=Accent，《登录》
      └─ Button / Button          431×32   Style=Standard，《注册》
```

- 强调按钮底色 `#005FB8`，描边为 `#FFFFFF@8% → #000000@40%` 的线性渐变（WinUI 的
  `AccentControlElevationBorderBrush`），圆角 4。
- 稿子没有「注册页」，也没有服务器地址输入框；实现里注册模式多出一个邮箱字段，
  地址放在设置页并持久化。

### 2.2 主页

```
App window 1252×800
├─ Title Bar                       1252×48   「EliCloud Desktop」+ 副标题「Internal」
└─ Nav + Content                   1252×752   HORIZONTAL
   ├─ Nav Pane                     320×752
   │  ├─ Top Items                 320×663
   │  │  └─ Nav Items              320×659   VERTICAL
   │  │     ├─ 列表项「主页」       320×40    圆角 4  padH=1  icon=Home  Selected=True
   │  │     ├─ 分组头「服务列表」   320×40    Type=Menu Section
   │  │     └─ 列表项「Minecraft 玩家服务器」320×40
   │  │        ├─ Icon  16×16       （Minecraft 草方块，图片填充）
   │  │        ├─ Text 「Minecraft 玩家服务器」fs=14 lh=20  TextFillColorPrimary
   │  │        ├─ Selector 3×10     圆角 999   AccentFillColorDefault
   │  │        └─ Badge 16×16       右对齐
   │  └─ Footer                    320×123
   │     ├─ 列表项（个人资料）       320×79
   │     │  ├─ 头像 64×64 圆角 134  （≈圆形）
   │     │  ├─ 「秋风Elipese」      fs=14 Semibold lh=20
   │     │  ├─ 「1111111111@xx.xxx」fs=12 lh=16  TextFillColorSecondary
   │     │  └─ Selector 3×10
   │     └─ 列表项「设置」          320×40    Selected=False
   └─ Content                      932×752   VERTICAL
      ├─ Sticky Header             932×183   padH=56 padV=36
      │  ├─ 横幅底图+粉色渐变       932×156   @1,1
      │  │    图片 + GRADIENT_LINEAR(#F9F9F9@0 → #FFA2E0@40%@1)
      │  │    + FOREGROUND_BLUR r=4 off(0,4) #000000@25%
      │  └─ Heading                860×147   @36,36   Type=Page title, Size=Large
      │        Title「EliCloud」    fs=28 lh=36 Semibold Display
      │        Description「欢迎来到EliCloud！ 您可以在此访问我们的各项服务，Feel free to use」fs=14 lh=20
      │        Divider             1px 描边
      └─ Scrolling Content         932×569   padH=56 gap=12
         └─ Frame 4                820×569   VERTICAL gap=16
            ├─ Heading「新服务」     820×66    Type=Content Section（标题 + 说明 + 分隔线）
            │     说明「期待最新的服务！」
            ├─ 服务卡片             820×64    （见 §3）
            ├─ Heading「正在工作」
            │     说明「一览当前可用服务，点击即用」
            ├─ 服务卡片             820×64    右侧多一个 40×40 的箭头按钮
            ├─ Heading「停止服务」
            │     说明「一刻也没有为这些停止工作的服务而哀悼，接下来赶到的是——用户的审视！」
            ├─ 服务卡片             820×64
            └─ 服务卡片             820×64
```

> 稿子在「停止服务」组里放了两张卡片，分别标 `维护中` 与 `不可用` —— 说明
> 「维护中 / 不可用」共用同一个分组表头。`即将上线` 属于「新服务」。

### 2.3 个人资料页

```
App window 1252×800
├─ Title Bar                       1252×48   副标题「Internal」
└─ Nav + Content                   1252×752
   ├─ Nav Pane                     320×752   ← 与主页同构，区别只在 Footer
   │  └─ Footer
   │     └─ 列表项（个人资料）       320×79    **Selected=True**
   │        ├─ Base 310×73          fill=#000000@4%  stroke=#000000@4%
   │        ├─ Selector 3×16        圆角 999  AccentFillColorDefault（可见）
   │        └─ 头像 / 名字 / 邮箱
   └─ Content                      932×752
      ├─ Sticky Header             932×163   padH=56 padV=36
      │  └─ Heading                860×127   Type=Page title, Size=Large
      │     ├─ Frame 6             860×64    头像 64×64 + 文字块
      │     │  ├─ Title「秋风Elipese」fs=28 lh=36 Semibold Display
      │     │  ├─ Actions「编辑个人信息」132×32  Style=Standard, Layout=Icon Before
      │     │  └─ Description「1111111111@xx.xxx」fs=14 lh=20 TextFillColorSecondary
      │     └─ Divider Wrapper     860×20   1px 描边
      └─ Scrolling Content         932×589   padH=56
         └─ TEXT「前面的路以后再来探索吧」fs=14 lh=20 居中
```

> 稿子上的个人资料页是**占位页**。实现把它扩成可编辑的资料页：
> 页头增加 banner 头栏图（无图时退回普通页头；粉色渐变只属于主页 Hero）、自定义头像、昵称与简介；
> 正文保留一组只读的账号事实（用户名/邮箱/邮箱状态/登录状态）。
> 「编辑个人信息」弹窗支持头像更换、头图更换与移除、昵称（1–32）与简介（最多 500）。
> 新账号的昵称默认为用户名；昵称不合法（为空、超过 32 字、含换行或控制字符）时
> 「保存」直接不可用，不再在提交后弹「昵称不能为空」。
> 头像/头图更换都走 Windows Community Toolkit 的 `ImageCropper`（标准 ContentDialog 按钮）：
> 头像圆形选区 1:1、结果缩到 **512×512 以内的 PNG**；头图按页头横幅的 **5:1** 比例裁、
> 结果缩到 **1600px 宽以内的 PNG**。原图直裁的 3000×3000 PNG 会超过 5 MB 上传上限。
> 页头是 `Grid`（头像 Auto 列 + 文字 Auto 列）：头像固定 64×64 不被挤压，昵称过长时按可用宽度省略；
> 压在 banner 上的承托面按内容收缩（短昵称/短简介时就是文字那么宽），最长到 56px 的页边距处，
> 长简介在承托面内换行；文字上限由页头实测宽度减去 padding、头像与间距算出，不写死像素值。
> banner 高度随窗口宽度按 **5:1** 等比缩放（内容更高时以内容为准）：窗口拉宽时底图不再被
> `UniformToFill` 横向裁掉，裁切弹窗里看到的取景就是页头上看到的取景。
> 图片字节由 userinfo 代理 file，客户端不直连 file；头像 5 MB、头图 10 MB 上限。

---

## 3. 服务卡片（稿里的 `Expander / Expander Example`，即需求里说的 SettingCard）

```
Expander / Expander Example   820×64
└─ Parent Content             820×64   pad=1
   └─ Base (Stroke)           818×62   1px  #000000@6%（CardStrokeColorDefault）
      └─ Base (Fill)          818×62   #FFFFFF@70%（LayerFillColorDefault 一系）
         ├─ Left Content      可伸缩    HORIZONTAL gap=16  padH=15 padV=13
         │  ├─ Icon           16×16     图片填充
         │  └─ Text           可伸缩     VERTICAL
         │     ├─ Heading     fs=14 lh=20 TextFillColorPrimary
         │     └─ Caption     fs=12 lh=16 TextFillColorSecondary
         └─ Right Content     98 / 104 / 84 宽  HORIZONTAL gap=10  右对齐
            ├─ 状态文案「即将上线 / 可用 / 维护中 / 不可用」fs=14 lh=20
            ├─ Badge         16×16
            └─ Button        40×40（只有「可用」那张有，圆角 4，箭头上箭头）
```

四张卡片的文案完全一样，只有状态不同 —— 稿子是在用同一张卡演示四种状态：

| 卡片 | 状态文案 | 右侧附加 |
|---|---|---|
| 1 | 即将上线 | — |
| 2 | 可用 | 40×40 箭头按钮 |
| 3 | 维护中 | — |
| 4 | 不可用 | — |

卡片正文：`Minecraft 玩家服务器` / `绑定用户名以获取准入权限`。

---

## 4. 设计令牌 → WinUI 主题令牌

稿件全部取自 Windows 11 设计组件库的 **Light** 变体，逐个对应：

| 设计稿里的名字 | 稿子取值 | 客户端用法 |
|---|---|---|
| `Light/Fill Color/Text/Primary` | `#000000` 90% | `Theme.PrimaryText` |
| `Light/Fill Color/Text/Secondary` | `#000000` 61% | `Theme.SecondaryText` |
| `Light/Stroke Color/Card Stroke/Default` | `#000000` 6% | `Theme.CardStroke` |
| `Light/Stroke Color/Divider Stroke/Default` | `#000000` 8% | `Theme.DividerStroke` |
| `Light/Background/Fill Color/Layer/Default` | `#FFFFFF` 70% | `Theme.LayerFill` |
| `Light/Fill Color/Accent/Default` | `#005FB8` | `Theme.Accent` |
| `Light/Background/Fill Color/Solid Background/Base` | `#F3F3F3` | `Theme.SolidBackground` |
| `Light/Background/Fill Color/Solid Background/Tertiary` | `#F9F9F9` | 横幅渐变起点 |
| `Light/Background/Fill Color/Mica Background/Base` | `#F3F3F3` | Mica 背板 |
| `Light/Shell Shadows/*` | 双段投影 | WinUI 窗口阴影 |

**结论：这套稿子就是一套标准 WinUI 3 界面**，所以实现里颜色一律走 `Theme.*`，
亮/暗/高对比度三套主题自动成立，没有一个硬编码色值。

排版阶梯（稿子的字号/行高正好落在 WinUI 文本样式上）：

| 用途 | 稿子 | WinUI 文本样式 | 客户端 |
|---|---|---|---|
| 登录页产品名 | 68 / 92 Semibold Display | `DisplayTextBlockStyle` | `DesignTokens.Display` |
| 页面主标题 | 28 / 36 Semibold Display | `TitleTextBlockStyle` | `DesignTokens.PageTitle` |
| 分组标题 | 14 / 20 Semibold | `BodyStrongTextBlockStyle` | `DesignTokens.SectionTitle` |
| 卡片标题 | 14 / 20 Regular | `BodyTextBlockStyle` | `DesignTokens.CardTitle` |
| 卡片说明 | 12 / 16 Regular | `CaptionTextBlockStyle` | `DesignTokens.CardCaption` |

---

## 5. 文案总表

| 位置 | 设计稿原文 | 客户端实现 |
|---|---|---|
| 窗口标题 | `EliCloud Desktop` | 同稿 |
| 标题栏副标题 | `Internal`（仅登录后） | 同稿 |
| 登录页 | `EliCloud` / `用户名` / `输入用户名或邮箱` / `密码` / `填入密码` / `登录` / `注册` | 标题、字段标签 `用户名或邮箱`、占位符 `输入用户名或邮箱`、`登录` 同稿（服务端确认两种标识都能定位账号）；密码占位符 `填入密码` → `请输入密码`；次级按钮 `注册` → `创建账号`；**注册模式**下这一格收窄为 `用户名` + 一行「只能用用户名，不能填邮箱」 |
| Nav 分组头 | `服务列表` | 同稿 |
| Nav 条目 | `主页`、`Minecraft 玩家服务器`、`设置` | 同稿 |
| 主页 Hero | `EliCloud` / `欢迎来到EliCloud！ 您可以在此访问我们的各项服务，Feel free to use` | 同稿 |
| 分组 | `新服务` / `期待最新的服务！` | 同稿 |
| 分组 | `正在工作` / `一览当前可用服务，点击即用` | 同稿 |
| 分组 | `停止服务` / `一刻也没有为这些停止工作的服务而哀悼，接下来赶到的是——用户的审视！` | 同稿 |
| 服务卡片 | `Minecraft 玩家服务器` / `绑定用户名以获取准入权限` | 同稿 |
| 状态 | `即将上线` / `可用` / `维护中` / `不可用` | 同稿 |
| 个人资料页 | `秋风Elipese` / `1111111111@xx.xxx` / `编辑个人信息` / `前面的路以后再来探索吧` | 标题/邮箱/按钮沿用稿；运行时换成昵称/账号/简介，并显示头像与 banner |

> 设计稿没写到的地方（状态说明、错误提示、MC 服务页、设置页）由客户端补写，
> 风格规范见 [copy-style.md](copy-style.md)：面向 ToC，不出现术语与实现细节。
>
> 稿子登录页的 `Header「用户名」` 在实现里是 `用户名或邮箱` —— 因为 `/login` 的
> `username` 字段收的本来就是「用户名或邮箱」，照抄 `用户名` 会让人以为邮箱登不了。

---

## 6. 设计稿里的图片资源

`.fig` 的 `images/` 目录里是 7 张按 sha1 命名的图片，其中只有 3 张真正被页面引用：

| 稿内引用处 | 尺寸 | 导出的语义化文件名 |
|---|---|---|
| 主页 Sticky Header 的横幅底图 | 3840×2160 JPEG | `home-hero-banner-christmas-pink.jpg` |
| Nav 页脚头像 / 资料页头像 | 1080×1076 PNG | `user-avatar-default.png` |
| Nav 条目图标 / 卡片图标 | 300×300 PNG | `service-icon-minecraft-block.png` |

其余 4 张（设计组件库的亚克力噪点贴图、PersonPicture 占位图、同一张横幅的缩略版等）
只服务于组件库本身，客户端不需要。
另有两张是**稿子里没有**、按同一视觉语言生成的图标（见 `Assets/Images/README.md`）：
产品图标 `elicloud-app-icon-cloud.png`、PDF 服务图标 `service-icon-pdf-lock.png`。

---

## 7. 与设计稿的有意差异

| 差异 | 原因 |
|---|---|
| 服务卡片用 **CommunityToolkit 的 `SettingsCard`**，不是手搓的等价物 | 手搓只能还原静止的一帧；悬停/按下/焦点视觉状态、键盘可达性、无障碍对等、窄窗换行阈值都由官方模板负责。见 `docs/reactor-feasibility.md` §2 |
| 卡片右侧的动作区用控件的 `ActionIcon` + `IsClickEnabled`，不是稿子里那个 40×40 按钮 | 同一个东西的官方实现：`IsClickEnabled=false` 时 Toolkit 会自动收起箭头，与稿子里「只有可用状态那张卡有箭头」一致 |
| 标题栏用官方 `TitleBar` 的 **Tall** 档（48px），由窗口外壳（`App`）统一托管，并关掉导航栏的自动让位内边距 | 稿子的 48px 就是 Tall；稿子第 1 屏（登录页）同样画了这条标题栏，所以未登录时也由应用自己托管、不退回系统标题栏；标题栏与导航栏是纵向邻居而非叠加。实机量到中心线 y≈23（48 高的中心是 24） |
| Nav 页脚个人资料条目按稿子补回上下各 8（合计 16）内边距 | 稿子该格是 320×79、头像 64×64；补完实机量到 **78px**，与稿子的 79px 一致 |
| 横幅底部那层 `FOREGROUND_BLUR` 用同位置的压暗渐变近似 | WinUI 没有「只对某张位图的局部做前景模糊」的廉价做法；差异仅在横幅下缘 |
| 状态徽标用 8px 实心圆点（外框 16×16） | 稿子里的 `Badge / Badge` 在三个实例上都是隐藏的、没有可见样式，只能按 WinUI InfoBadge 的语义取「状态色圆点」 |
| 登录页字段标签是 `用户名或邮箱`，不是稿子的 `用户名` | `/login` 的 `username` 字段收的本来就是「用户名或邮箱」；照抄 `用户名` 会让人以为邮箱登不了 |
| 注册模式下这一格收窄为 `用户名`，并加一行「只能用用户名，不能填邮箱」 | 注册时 `username` 只认用户名（`validate_username` 的字符集不含 `@`），与登录标识的宽窄不同，必须说清 |
| 登录页多出「注册」模式的邮箱字段 | 平台注册必须先证明邮箱归属；注册不再直接建号（`SsoClient.StartRegistrationAsync`） |
| 登录/注册卡片之后多一屏「验证邮箱」 | 稿子只有登录页；注册改为先发确认邮件，点击后客户端轮询到 verified 才自动登录。沿用 431px 卡片、`DesignTokens` 与 `Theme.*`，不新造视觉样式 |
| 界面上不出现平台入口地址 | 入口是构建时注入的我方基础设施，不是用户设置；登录页原有的一行地址已移除，见 `README.md` §4 |
| 个人资料页增加昵称/简介/头像/banner 编辑，正文保留用户名/邮箱/邮箱状态/登录状态 | 稿子该页是占位页；新增字段来自 userinfo，邮箱状态让用户知道 `email_verified` 的真实值，术语型字段（sub / scope / 令牌）不进界面 |
| 头像/头图不读取本地文件路径，只存 file 的元数据 | 图片由 file 模块管理，所有资料图固定 public + 永久；客户端只经 userinfo 代理上传/读取 |
| 默认主题跟随系统 | 不指定元素级主题，浅色/深色随 Windows 走；设置页可固定为浅色或深色 |
