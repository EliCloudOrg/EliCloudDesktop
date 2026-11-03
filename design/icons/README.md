# 品牌图标源

| 文件 | 用途 | 状态 |
|---|---|---|
| `logo.svg` | 品牌云朵的矢量源；Linux 直接使用，其它平台按各自规范导出 | **草案**：Stage 2 按现有产品图标的视觉语言重画，待 design-owner 确认 |
| `logo-maskable.svg` | Android adaptive icon 的安全区版本（前景层） | 随 `logo.svg` 一起确认 |

规则：

- **不跨平台复用 SF Symbols / Segoe Fluent Icons 字形**；品牌 Logo 是自家资产，可以跨端复用形状，
  但各端输出格式/尺寸按平台规范（Windows ICO/PNG、macOS PNG/PDF、Android vector drawable、Linux SVG）。
- 服务图标用平台原生图标集（Windows Fluent、macOS SF Symbols、Linux Adwaita symbolic、Android Material Symbols），
  不从这里导出。
- 导出脚本：`python tools/export-assets.py --check` / `--export artifacts/assets`，清单见
  [`../assets.json`](../assets.json)，来源与许可见 [`../ASSETS.md`](../ASSETS.md)。
