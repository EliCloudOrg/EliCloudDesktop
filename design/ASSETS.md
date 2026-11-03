# 资产来源与许可

> 清单是 [`assets.json`](assets.json)，导出脚本是 `tools/export-assets.py`。
> 规则：**源文件保留高分辨率**；每端生成自己的尺寸/格式；所有资产有来源/许可记录。

## 1. 资产表

| 资产 | 源 | 许可 | 状态 | 各端输出 |
|---|---|---|---|---|
| 品牌 Logo（云朵） | `design/icons/logo.svg` | proprietary (EliCloud) | **草案**，待 design-owner 确认 | Windows ICO + PNG；macOS PNG；Android adaptive icon 前景；Linux SVG |
| 主页 Hero 横幅 | `src/EliCloudDesktop/Assets/Images/home-hero-banner-christmas-pink.jpg`（3840×2160） | proprietary (EliCloud) | 已确认 | Windows JPG；macOS PNG；Android WebP；Linux PNG |
| Minecraft 服务图标 | `Assets/Images/service-icon-minecraft-block.png`（300×300） | proprietary (EliCloud) | 已确认 | PNG 16/20/24/32/48/64（按端裁） |
| PDF 服务图标 | `Assets/Images/service-icon-pdf-lock.png` | proprietary (EliCloud) | 已确认 | PNG 16/20/24/32/48/64 |
| 默认头像 | `Assets/Images/user-avatar-default.png`（1080×1076） | proprietary (EliCloud) | 已确认 | PNG 64/128 |

## 2. 规则

1. **不跨平台复用 SF Symbols / Segoe Fluent Icons 字形**：服务图标在各端用原生图标集
   （Windows Fluent、macOS SF Symbols、Linux Adwaita symbolic、Android Material Symbols），
   只有品牌 Logo 用同一形状、按平台规范重新导出。
2. 不打包 SF Pro / Segoe UI：各端用系统字体；Linux 可以打包 Inter（OFL），但必须先登记许可。
3. 图片源只保留高分辨率母版；生成物按端输出（桌面 PNG/JPG、Android WebP、macOS asset catalog）。
4. Android 的 adaptive icon 用 108dp 安全区：前景层只占中间 66dp，避免被圆形/方形遮罩裁掉。
5. 新增资产必须同时更新 `assets.json` 与本文；缺来源/许可的资产不允许进仓库。

## 3. 导出

```bash
# 只校验清单与源文件（CI 用；不需要写文件）
python tools/export-assets.py --check

# 导出到 artifacts/assets（默认，不入库）
python tools/export-assets.py --export artifacts/assets

# 单独导出某端
python tools/export-assets.py --export artifacts/assets --platform windows
```

- 依赖 Pillow（本机已有）；SVG 光栅化需要 `cairosvg`，缺失时自动回退到 `rasterFallback` 并在输出里注明。
- `--check` 校验：id 唯一、source 存在、输出规格合法、尺寸为正整数、每个平台至少一种输出。
- 已知缺口：macOS 的 PDF 矢量导出需要设计侧从 SVG 出图；Android adaptive icon 的 vector drawable
  需要人工从 SVG 转（脚本暂不生成 XML）；PDF 服务图标的源只有 64×64，Android 端目前只导出
  24/48px，Stage 5 用 Material Symbols 重画或由设计侧补 96/192px 源。

## 4. 待确认

- `design/icons/logo.svg` 是 Stage 2 按现有产品图标视觉语言重画的**草案**；
  正式品牌稿确认后，替换 SVG 并重新导出，`assets.json` 的 `status` 改为 `approved`。
