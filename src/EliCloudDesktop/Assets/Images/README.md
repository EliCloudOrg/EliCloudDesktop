# 图片资源命名约定

命名规则：**`<用途/归属>[-<变体>].<扩展名>`**，全小写、连字符分词、不含哈希与编号。
目标是一眼能从文件名读出「这张图是什么、用在哪」，而不是「这是第几张」。

| 文件 | 尺寸 | 来源 | 用在哪 |
|---|---|---|---|
| `home-hero-banner-christmas-pink.jpg` | 3840×2160 JPEG | `.fig` 导出（sha1 `7279e4d7…`） | 主页 Hero 横幅底图（`Widgets.HeroBanner`） |
| `user-avatar-default.png` | 1080×1076 PNG | `.fig` 导出（sha1 `d46dfd2c…`） | Nav 页脚个人资料条目、个人资料页页头头像 |
| `service-icon-minecraft-block.png` | 300×300 PNG | `.fig` 导出（sha1 `7c09e909…`） | MC 服务的 Nav 条目图标、服务卡片图标 |
| `service-icon-pdf-lock.png` | 64×64 PNG | `tools/generate-icons.py` 生成 | PDF 在线解密服务的卡片图标（设计稿未画该服务） |
| `elicloud-app-icon-cloud.png` | 256×256 PNG | `tools/generate-icons.py` 生成 | 窗口标题栏左侧产品图标（设计稿未画 Logo） |

## 为什么有两张是「生成」的

设计稿里没有产品 Logo，也没有 PDF 服务的图标 —— 但客户端需要它们（标题栏图标、服务清单里的第二项）。
按同一套视觉语言（Windows 11 设计组件库的强调色 `#005FB8`）用 Segoe Fluent Icons 字形生成，
脚本是 `tools/generate-icons.py`，随时可重跑：

```powershell
python tools/generate-icons.py
```

## 打包相关资源

`Assets/` 根目录下的 `AppIcon.ico`、`Square44x44Logo.*`、`SplashScreen.*` 等是
MSIX 清单 `Package.appxmanifest` **按固定名字**引用的打包资源（模板自带），
不受上面的命名约定约束，也不要重命名。
