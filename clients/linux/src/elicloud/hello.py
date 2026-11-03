"""GTK4 + libadwaita hello-world。

Stage 2 只证明 Linux 端能消费 tokens / copy / build-config；业务页面属于 Stage 6。

官方样式优先：
  - 文本用生成物里的 Adwaita style class（`title-1` / `body` / `caption` / `dim-label`），
    不在这里写字号或字体；
  - 窗口/标题栏用 `Adw.ApplicationWindow` + `Adw.HeaderBar`，不手绘 chrome；
  - 颜色一律让 libadwaita 的命名颜色负责。
"""

from __future__ import annotations

import sys

import gi

gi.require_version("Gtk", "4.0")
gi.require_version("Adw", "1")

from gi.repository import Adw, Gtk  # noqa: E402

from . import config  # noqa: E402
from .copy import text  # noqa: E402
from .design import tokens  # noqa: E402


class HelloWindow(Adw.ApplicationWindow):
    def __init__(self, app: Adw.Application) -> None:
        super().__init__(application=app, title=text("app.title"))
        self.set_default_size(640, 400)

        header = Adw.HeaderBar()
        toolbar_view = Adw.ToolbarView()
        toolbar_view.add_top_bar(header)

        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=tokens.SPACE_4)
        box.set_margin_top(tokens.SPACE_6)
        box.set_margin_bottom(tokens.SPACE_6)
        box.set_margin_start(tokens.SPACE_6)
        box.set_margin_end(tokens.SPACE_6)
        box.set_valign(Gtk.Align.START)

        box.append(self._label(text("app.title"), tokens.TYPE_TITLE))
        box.append(self._label(config.USER_AGENT, tokens.TYPE_BODY, secondary=True))
        box.append(self._label(text("home.hero.description"), tokens.TYPE_BODY))
        box.append(
            self._label(
                text("settings.about.version", version=config.APP_VERSION),
                tokens.TYPE_CAPTION,
                secondary=True,
            )
        )

        toolbar_view.set_content(box)
        self.set_content(toolbar_view)

    def _label(self, value: str, style_class: str, *, secondary: bool = False) -> Gtk.Label:
        label = Gtk.Label(label=value, xalign=0)
        label.set_wrap(True)
        # 生成物里 official 的 ref 就是 Adwaita 的 style class 名，直接交给 GTK。
        label.add_css_class(style_class)
        if secondary:
            label.add_css_class(tokens.COLOR_TEXT_SECONDARY)
        return label


class HelloApp(Adw.Application):
    def __init__(self) -> None:
        super().__init__(application_id="cloud.elipese.hello")

    def do_activate(self) -> None:  # noqa: N802 - GTK vfunc 命名
        window = self.props.active_window or HelloWindow(application=self)
        window.present()


def main(argv: list[str] | None = None) -> int:
    return HelloApp().run(argv if argv is not None else sys.argv)


if __name__ == "__main__":
    raise SystemExit(main())
