# Linux 客户端（Stage 2 hello-world）

Python + GTK4/libadwaita 最小工程，验证 Linux 端能消费 tokens / copy / build-config。
业务页面在 Stage 6 展开。

## 结构

```text
clients/linux/
├── meson.build                  # 安装布局 + 字节码编译 + unittest
├── pyproject.toml
├── src/elicloud/hello.py        # Adw.ApplicationWindow + HeaderBar
├── src/elicloud/design/tokens.py    # 生成物（入库）
├── src/elicloud/copy.py             # 生成物（入库）
├── src/elicloud/config.py           # 构建产物（不入库）
├── locale/zh_CN/LC_MESSAGES/elicloud.po  # 生成物（入库，gettext 用）
└── tests/test_generated.py
```

## 构建 / 测试

需要 Python 3.11+、PyGObject、GTK4、libadwaita、Meson 1.3+。

```bash
python3 tools/gen-tokens/gen_tokens.py
python3 tools/gen-copy/gen_copy.py
python3 tools/gen-build-config/gen_build_config.py --environment local --platform linux

cd clients/linux
meson setup build
meson compile -C build
meson test -C build --print-errorlogs

# 运行（需要显示；无显示用 xvfb-run）
PYTHONPATH=src python3 -m elicloud
```

## 与共享层的关系

- tokens：official 值是 Adwaita 的 CSS 变量 / style class 名（`@theme_fg_color`、`title-1`、
  `dim-label`）；颜色交给 CSS 变量解析，style class 交给 `add_css_class()`，不要混用；
- copy：`copy.py` 是 Python dict + `text(key, **values)`；`.po` 同时生成，给 gettext/Flatpak 用；
- build config：`config.py` 是编译期常量，运行时不读环境变量。

## 已知缺口

- 只 build + unittest；GTK 运行时冒烟依赖 CI 的 `xvfb`（Stage 6 补真实页面截图）；
- Linux 工具包（Python vs Rust）仍需 Stage 0 决策记录确认；
- Flatpak 清单在 Stage 6/8。
