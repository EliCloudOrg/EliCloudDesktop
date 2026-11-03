"""Stage 2 Linux hello-world 的生成物自检（不依赖 GTK，可在无显示环境跑）。

运行：
    PYTHONPATH=clients/linux/src python -m unittest discover -s clients/linux/tests -v
"""

from __future__ import annotations

import json
import re
import sys
import unittest
from pathlib import Path

LINUX_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = LINUX_ROOT.parents[1]
sys.path.insert(0, str(LINUX_ROOT / "src"))

from elicloud import config  # noqa: E402
from elicloud.copy import STRINGS, text  # noqa: E402
from elicloud.design import tokens  # noqa: E402


class TokenTests(unittest.TestCase):
    def test_official_references_are_kept(self) -> None:
        self.assertEqual(tokens.TOKENS_VERSION, 2)
        self.assertEqual(tokens.COLOR_TEXT_PRIMARY, "@theme_fg_color")
        self.assertEqual(tokens.COLOR_TEXT_SECONDARY, "dim-label")
        self.assertEqual(tokens.TYPE_TITLE, "title-1")
        self.assertEqual(tokens.TYPE_BODY, "body")
        self.assertEqual(tokens.TYPE_CAPTION, "caption")
        self.assertEqual(tokens.MATERIAL_CHROME, "@headerbar_bg_color")

    def test_custom_values_are_literals(self) -> None:
        self.assertEqual(tokens.SPACE_4, 16)
        self.assertEqual(tokens.DENSITY_ROW, 40)
        self.assertEqual(tokens.MOTION_FAST_MS, 150)
        self.assertEqual(tokens.BRAND_HERO_SCRIM, "#80000000")
        self.assertEqual(tokens.RADIUS_CONTROL, 6)


class CopyTests(unittest.TestCase):
    def test_text_substitutes_placeholders(self) -> None:
        self.assertEqual(text("mc.whitelist.count", count=3), "共 3 人")
        self.assertEqual(text("settings.about.version", version="0.1.0"), "EliCloud 桌面版 0.1.0")

    def test_all_keys_present(self) -> None:
        keymap = REPO_ROOT / "design" / "generated" / "copy-keys.json"
        expected = set(json.loads(keymap.read_text(encoding="utf-8"))["keys"])
        self.assertEqual(expected - set(STRINGS), set())
        self.assertEqual(set(STRINGS) - expected, set())

    def test_po_covers_all_keys(self) -> None:
        po = LINUX_ROOT / "locale" / "zh_CN" / "LC_MESSAGES" / "elicloud.po"
        msgids = set(re.findall(r'^msgid "([^"]+)"$', po.read_text(encoding="utf-8"), re.MULTILINE)) - {""}
        self.assertEqual(set(STRINGS) - msgids, set(), "elicloud.po 缺少 key")
        self.assertEqual(msgids - set(STRINGS), set(), "elicloud.po 多出 key")


class BuildConfigTests(unittest.TestCase):
    def test_injected_config(self) -> None:
        self.assertIn(config.ENVIRONMENT, {"local", "prod"})
        self.assertTrue(config.BASE_URL.startswith(("http://", "https://")))
        self.assertFalse(config.BASE_URL.endswith("/"))
        self.assertEqual(config.USER_AGENT, f"EliCloud/{config.APP_VERSION} (linux)")
        self.assertNotIn("://", config.USER_AGENT)
        if config.ENVIRONMENT == "prod":
            self.assertTrue(config.BASE_URL.startswith("https://"))


if __name__ == "__main__":
    unittest.main()
