# AGENT.md — Linux

## 命令

```bash
python3 tools/gen-tokens/gen_tokens.py
python3 tools/gen-copy/gen_copy.py
python3 tools/gen-build-config/gen_build_config.py --environment local --platform linux
cd clients/linux && meson setup build && meson compile -C build && meson test -C build --print-errorlogs
PYTHONPATH=src python3 -m elicloud
python3 tools/audit-official-styles/audit.py --platform linux
```

## 结构 / 约定

- `src/elicloud/design/tokens.py`、`src/elicloud/copy.py`、`locale/**/*.po` 是生成物（勿手改）；
- `src/elicloud/config.py` 是构建产物（不入库）；
- 新增 UI 用 GTK4/libadwaita 官方控件；不引入第二个 UI 框架。

## 官方样式优先（硬约束）

- 颜色用 Adwaita 命名颜色（`@theme_fg_color`、`@window_bg_color`、`@accent_bg_color`、
  `@card_bg_color`、`@borders`…），**不要**写 `Gdk.RGBA(...)` 或 hex；
- 排版用 Adwaita style class（`title-1`…`title-4`、`large-title`、`body`、`caption`、`dim-label`、
  `monospace`），**不要** `set_font_size()` 或 CSS `font-size: Npx`；
- 控件用 `Adw.HeaderBar` / `Adw.PreferencesPage` / `Adw.StatusPage` / `Adw.ToastOverlay`；
  Adwaita 已有样式时不要自己写 CSS；
- 圆角/间距没有官方令牌时，从 `design/tokens.json` 的登记例外取。

## 不要做

- 不要在 Stage 2 写业务页面（Stage 6 才做）；
- 不要手改生成物；
- 不要把入口地址/令牌写进日志或界面；
- 不要伪造 Mica/Glass 材质（Linux 用 Adwaita 的窗口/headerbar 背景）。

## 已知缺口

- 只 build + unittest；运行时截图/无障碍（AT-SPI）在 Stage 6/8；
- `pyproject.toml` 的 meson-python 打包尚未在 CI 验证（CI 只跑 meson setup/compile/test）。
