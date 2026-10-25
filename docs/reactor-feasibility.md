# Reactor 技术选型分析

> 记录这个客户端里「哪里直接用 Reactor、哪里借 WinUI 官方控件、哪里必须走逃生舱」，
> 以及每次选择背后的具体理由和实测证据。

---

## 1. 结论速览

| 需求 | 做法 | 落点 |
|---|---|---|
| 登录后按服务状态动态增删 Nav 条目与入口 | Reactor 原生：菜单每次渲染重算，交给 diff | `Shell/MainShell.cs` · `BuildMenuItems` |
| 主页服务条目（SettingCard） | **借 CommunityToolkit 的 `SettingsCard`**，用 `[GenerateReactorWrapper]` 包成声明式元素 | `Ui/SettingsCardElement.cs` · `Ui/Widgets.cs` · `SettingCard` |
| 主页分组表头「无服务则不加」 | Reactor 原生条件渲染 | `Pages/HomePage.cs` · `Sections` |
| 状态用卡片里的 Badge 呈现 | Reactor 原生 | `Ui/Widgets.cs` · `StatusBadge` |
| 标题栏 | **官方 WinUI `TitleBar` 控件的 Tall 档**（Reactor 的 `TitleBar` 就是它的声明式外壳） | `Shell/MainShell.cs` |
| 个人资料入口塞进 `FooterMenuItems` | 必须走逃生舱 `.Set(native => …)` | `Shell/NavFooterProfileItem.cs` |
| 三页视觉还原 | 设计稿本身是 Windows 11 设计组件库，全部映射到 `Theme.*` | `Ui/DesignTokens.cs` |

一句话：**能声明式就声明式（Reactor 原生）；控件官方已有成熟实现就借过来包一层；
只有声明式表达不了的东西才下到逃生舱。**

---

## 2. 主页服务条目：为什么最后用了 CommunityToolkit 的 SettingsCard

### 2.1 两种做法都试过

**做法 A —— Reactor 原生拼（第一版，已弃用）**
`Border` + 两层 `Grid` + `TextBlock` 手搓出稿子上的 818×62 卡片，约 40 行。

**做法 B —— 包 CommunityToolkit 的 `SettingsCard`（现用）**

```csharp
// Ui/SettingsCardElement.cs
[GenerateReactorWrapper(typeof(SettingsCard), Exclude = new[] { "Command", "CommandParameter" })]
[WrapElementSlot("HeaderIcon")]
[WrapElementSlot("ActionIcon")]
internal sealed partial record SettingsCardElement;
```

```csharp
// 调用点（Ui/Widgets.cs）
SettingsCard(
    header: title,
    description: caption,
    headerIcon: icon,          // 直接塞 Reactor 元素
    content: trailing,         // 右侧内容槽（默认右对齐）
    isClickEnabled: onClick is not null,
    onClick: onClick)
```

整个工程仍然**没有任何 `.xaml` 文件**：生成器在静态构造函数里注册控件程序集
（Toolkit 的 `Themes/Generic.xaml` 因此在首次实现前就位），调用点和 Reactor 自带元素一模一样。

### 2.2 A 为什么不够好

| 维度 | 做法 A | 做法 B |
|---|---|---|
| 视觉状态 | 只有「静态样子」。悬停、按下、焦点矩形、禁用态全要自己写 | 官方模板全带，且与系统设置页一致 |
| 键盘可达性 | 要自己 `IsTabStop` + `OnKeyDown` 处理 Enter/Space | 控件本身是 Button 派生，默认可用 |
| 无障碍对等 | 要自己命名、自己保证朗读顺序 | 自带 AutomationPeer |
| 右侧动作区 | 稿子里那个 40×40 箭头按钮得自己造 | `IsClickEnabled` + `ActionIcon` 就是它 |
| 自适应 | 窄窗时文字会挤在一起或整块消失（见 §2.3） | Toolkit 内置 `SettingsCardWrapThreshold` 换行阈值 |

结论：**手搓只能还原「静止的一帧」**，而设置类条目恰恰是交互状态最多的一类控件。
这不是「写得出来写不出来」的问题，是「写出来要维护多少状态」的问题。

### 2.3 手搓版踩到的坑（留作证据）

第一版用嵌套 Flex：

```csharp
FlexRow( FlexRow(icon, FlexColumn(title, caption).Flex(grow:1, basis:0)).Flex(grow:1),
         FlexRow(trailing).Flex(shrink:0) )
```

实机截图里**只有右侧区域出现，图标与两行文字整体消失**：左侧文字列声明了 `basis:0`，
算出的内在宽度是 0，同一行又有「不收缩」的右侧区，于是分给它的空间就是 0，
`TextTrimming(CharacterEllipsis)` 再把它裁成不可见。
换成 Grid 的 `[Star, Auto]` 分栏后立刻正常 —— 但那时已经说明：**这类控件的布局细节不该由我们承担**。

### 2.4 包的过程中踩的坑

`SettingsCard.HeaderIcon` 的类型是 `IconElement`。生成器不会把 UIElement 属性当普通值属性，
所以要用 `[WrapElementSlot]` 声明成元素槽；但**槽里仍然只能放 `IconElement` 家族的元素**。
第一版往槽里塞了 `Image`，运行期直接抛：

```text
InvalidCastException: Unable to cast object of type 'Microsoft.UI.Xaml.Controls.Image'
                      to type 'Microsoft.UI.Xaml.Controls.IconElement'
```

改成 `Icon(BitmapIcon(uri, showAsMonochrome: false))` 即正常。

### 2.5 什么时候还该手搓

Toolkit 里没有对应控件的自绘区域（Hero 横幅、分组表头、状态圆点）继续用 Reactor 原生元素拼 ——
它们没有交互状态，就是「一段静态布局」，手搓反而是最直的写法。

---

## 3. 动态 Nav 条目（按服务状态增删）

Reactor 是声明式的：`MenuItems` 每次渲染重新算，Reactor 负责把结果 diff 到 `NavigationView` 上，
所以不需要任何手工的 `MenuItems.Add/Remove`：

```csharp
// Shell/MainShell.cs
var available = ServiceCatalog.All.Where(d => state.Services.IsAvailable(d.Id)).ToList();
if (available.Count == 0) return items;          // 一个都没有 → 连「服务列表」表头都不加
items.Add(NavItemHeader("服务列表"));
foreach (var descriptor in available)
{
    items.Add(new NavigationViewItemData(descriptor.DisplayName, Tag: RouteTags.ForService(descriptor.Id))
    {
        IconElement = BitmapIcon(new Uri(descriptor.IconUri), showAsMonochrome: false),
    });
}
```

配套的两条守卫：

- **入口消失后不能把人留在空页上**：当前路由指向的服务**被确认**为不可用时 `Replace` 回主页；
  只在拿到确定结论时动，`Unknown`（还没探测完）不该把用户赶走。
- **路由表与条目同源**：条目 tag 是 `service:<id>`，`RouteTags.Parse` 反解成 `Route.Service(id)`，
  没有第二份「哪个服务有页面」的清单。

---

## 4. FooterMenuItems 逃生舱（个人资料入口）

设计稿的个人资料入口是「64×64 圆角头像 + 两行文字 + 选中指示条」，
而 Reactor 声明的 `NavigationViewItemData` 只有 `Content`（字符串）/`Icon`/`Tag`/`Children` ——
**没有承载自定义内容模板的位置**。要么放弃设计稿里的头像，要么拿到底层控件自己造条目。

```csharp
// Shell/MainShell.cs
.Set(native => NavFooterProfileItem.Apply(
    native, profileSignature, lastProfileSignature,
    state.Session, displayName, navAvatar, AvatarUri))
```

```csharp
// Shell/NavFooterProfileItem.cs（节选）
var item = view.FooterMenuItems.OfType<NavigationViewItem>()
               .FirstOrDefault(c => Equals(c.Tag, RouteTags.Profile));
if (item is null) { item = new NavigationViewItem { Tag = RouteTags.Profile, Content = BuildContent(...) };
                    view.FooterMenuItems.Add(item); return; }
if (lastSignature.Current == signature) return;   // 签名没变 → 不动内容
item.Content = BuildContent(session, displayName, avatarSource, avatarUri);
```

三条保证，缺一不可：

1. **按 `Tag` 去重** —— `.Set` 每次渲染都会跑一遍，不去重就会无限追加条目。
2. **内容按「签名」重建** —— 签名 = 用户名 + 邮箱，存在调用方的 `UseRef` 里（跨渲染稳定）。
3. **选中态交给 Reactor** —— 条目的 `Tag` 就是 `RouteTags.Profile`，Reactor 的 `SelectedTag`
   能正常选中它（实机截图里进入个人资料页时该条目高亮、主页条目取消高亮）。

> 为什么不用声明式的 `PaneFooter`？需求要求放进 `FooterMenuItems`（Nav「设置」项之前那一格），
> 而 `PaneFooter` 是面板最底部的另一块区域，位置不对。

条目内容还有一处对齐设计稿的细节：稿子里这一格是 320×79、头像是 64×64，
所以内容根节点加了上下各 8 的内边距（合计 16）—— 实机量出来 **78px**，与稿子的 79px 一致。

---

## 5. 标题栏

用官方 WinUI `TitleBar` 控件（Reactor 的 `TitleBar(...)` 就是它的声明式外壳），
并按设计稿的 48px 高设 `.Tall()`：实机量到标题栏中心线在 y≈23（48 高的中心是 24），与稿子一致。
另外显式关掉导航栏那层「给标题栏让位」的自动内边距
（`IsTitleBarAutoPaddingEnabled = false`）—— 我们这里是标题栏与导航栏纵向排布的邻居，不是叠在一起。

---

## 6. 状态模型：只做「可用 / 不可用」，其余留扩展

```csharp
enum ServiceAvailability { Unknown, ComingSoon, Available, Maintenance, Unavailable }
```

- **当前能判定的只有两个**：`Available` / `Unavailable`，来源是客户端对服务健康接口的真实探测。
  服务在跑但写通道挂了 → 服务端返回 503 但响应体完整（SDK 照样解析成对象），
  我们据此判为 `Unavailable` 并给出原因。
- **其余三个是预留**：`Unknown` = 尚未探测；`Maintenance` = 将来服务端自报维护窗口时用；
  `ComingSoon` = 服务已在平台清单登记但本版本还没有探测手段。
- UI 侧一行都不用改就能接新状态：卡片按 `ServiceAvailability` 取文案与颜色，分组按状态集合归类。

---

## 7. 状态与数据流

```text
App (根组件)
 ├── UseReducer<AppState, AppAction>(…, threadSafe: true)   ← 单一事实来源
 ├── PlatformConfig.BaseAddress                             ← 构建时注入，运行时只读
 ├── UseEffect → ProbeAllAsync → dispatch(ServicesRefreshed) ← 登录后探活
 ├── UseEffect → AppSettings.Save()                          ← 用户名/主题落盘
 └── .Provide(State / Dispatch / Gateway)
      ├── LoginPage     （未登录）
      └── MainShell     （已登录）
           ├── UseNavigation<Route>(Home) + NavigationHost    ← 导航根在这层
           ├── TitleBar(Tall) + NavigationView(MenuItems = f(服务状态), FooterMenuItems + 逃生舱)
           └── 页面：HomePage / McServicePage / ProfilePage / SettingsPage
```

- **导航句柄放在 `MainShell` 而不是根组件**：`UseNavigation` 的 child 模式要靠 `NavigationHost`
  提供 context，而 `MainShell` 是 `NavigationHost` 的父级、不是子孙。
  第一版就踩了这个坑（`UseNavigation<Route>()` 直接抛
  "found no ancestor NavigationHost providing NavigationContext<Route>"）。
  顺带得到正确语义：退出登录时 `MainShell` 整棵卸载，导航历史自动清空。
- **平台入口不进状态**：它是构建时常量（`Core/PlatformConfig.cs`），没有 action、没有设置项、
  界面上也不显示。
- 页面用 `UseContext` 读状态、`dispatch` 改状态，没有 MVVM 层，也没有 `INotifyPropertyChanged`
  （Reactor 对组件上的 INPC 是无效的，`REACTOR_STATE_001` 会直接报出来）。

---

## 8. 构建与诊断

- 工程是 **packaged**（`Package.appxmanifest` + `EnableMsixTooling`），要拿包标识
  （`ms-appx:///` 资源、WinAppSDK 运行时）。`dotnet run` 会注册调试标识后启动，需要开发者模式。
- 本机没有 `mur`（Reactor CLI），所以构建直接用 `dotnet build -p:Platform=x64`；
  Reactor 的分析器随 `Microsoft.UI.Reactor` 包自动加载
  （`REACTOR_A11Y_*`、`REACTOR_DSL_*` 等在本工程里是真会报的，已全部清零）。
- **两套开发构建方式**（见 README §1）：`Debug-Prod`（远程）/ `Debug-Local`（本机服务端），
  外加保留的 `Debug`（等同 `Debug-Prod`）与 `Release`。
  入口地址是构建期生成的**编译期常量**（csproj 的 `GeneratePlatformConfig` 目标 → `PlatformConfig.g.cs`），
  不是运行时反射读程序集元数据 —— Release 曾开 `PublishTrimmed`，常量对裁剪免疫。
- **Release 关掉了裁剪**（`PublishTrimmed=false`，有意偏离模板默认）：
  `EliCloud.Sdk` 内部用反射式 `System.Text.Json` 反序列化自己的 DTO，
  裁剪器无法证明那些类型会被用到，报出 IL2104；把反射式序列化当裁剪安全对待，
  风险不是多几个警告，而是线上某个响应解析不出来。
- 四个配置 + 不带 `-c` 的默认构建都是 **0 error / 0 warning**（含 `-p:TreatWarningsAsErrors=true`）；
  `docs/screenshots/` 是与设计稿逐屏比对用的实机截图。

---

## 9. 已知的 Reactor 缺口与绕法

### 9.1 `TextBoxElement.Description(...)` 不渲染

WinUI 的 `TextBox` 有 `Description`（输入框下方那行辅助说明）。Reactor 也给了同名的类型化修饰符：

```csharp
TextBox(value, setValue).Description("只能用字母、数字和下划线，3 到 16 位")
```

但实机上**什么都不显示**。UI 自动化的证据（本地服务端 + 真机）：

```text
ControlType.Edit '用户名'   HelpText=''      ← 描述没落到原生属性上
（整棵树里也找不到那行文字）
```

绕法：自己画。`DesignTokens.Hint(...)` 输出一行 12/16 的 Secondary 文字，
`DesignTokens.Field(input, hint)` 把它和输入框按 4px 绑在一起（否则会被父容器的 16px 间距推得像属于下一个字段）。
自己画还顺带保证读屏能读到这句约束 —— 靠 `Description` 的话连名字都没有。

### 9.2 `SettingsCard.HeaderIcon` 必须是 `IconElement`

见 §2.4：元素槽里塞 `Image` 会在挂载时抛 `InvalidCastException`，要用 `Icon(BitmapIcon(uri))`。

### 9.3 排查这类问题的通用手法

界面上「看不出来」的问题，先看 **UI 自动化树**再改代码：
`uia-click.ps1`（`tools/` 旁边那份临时脚本）在找不到目标时会 dump 整棵带名字的元素树，
上一条 `Description` 的结论就是这么来的 —— 比反复读代码猜快得多。
