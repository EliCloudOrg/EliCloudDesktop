"""gen-build-config 的单元测试：合法/非法入口地址、UA、构建失败。

运行：
    python -m unittest discover -s tools/gen-build-config/tests -v
"""

from __future__ import annotations

import importlib.util
import os
import sys
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "shared"))

import designlib as dl  # noqa: E402

MODULE_PATH = ROOT / "tools" / "gen-build-config" / "gen_build_config.py"
spec = importlib.util.spec_from_file_location("gen_build_config", MODULE_PATH)
gbc = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(gbc)


def load_config() -> dict:
    return gbc.load_config(ROOT / "design" / "build-config.json")


class ConfigTests(unittest.TestCase):
    def test_repository_config_is_valid(self) -> None:
        doc = load_config()
        self.assertEqual(doc["appVersion"], "0.1.0")

    def test_user_agent(self) -> None:
        doc = load_config()
        self.assertEqual(gbc.user_agent(doc, "windows"), "EliCloud/0.1.0 (windows)")
        self.assertEqual(gbc.user_agent(doc, "android"), "EliCloud/0.1.0 (android)")

    def test_local_uses_loopback_default(self) -> None:
        doc = load_config()
        with mock.patch.dict(os.environ, {}, clear=True):
            self.assertEqual(gbc.resolve_base_url(doc, "local", None), "http://127.0.0.1:8080")

    def test_prod_requires_value(self) -> None:
        doc = load_config()
        with mock.patch.dict(os.environ, {}, clear=True):
            with self.assertRaises(gbc.BuildConfigError):
                gbc.resolve_base_url(doc, "prod", None)

    def test_prod_requires_https(self) -> None:
        doc = load_config()
        with mock.patch.dict(os.environ, {}, clear=True):
            with self.assertRaises(gbc.BuildConfigError):
                gbc.resolve_base_url(doc, "prod", "http://api.example.com")
            self.assertEqual(
                gbc.resolve_base_url(doc, "prod", "https://api.example.com"),
                "https://api.example.com",
            )

    def test_explicit_wins_over_env(self) -> None:
        doc = load_config()
        with mock.patch.dict(os.environ, {"ELICLOUD_BASE_URL_LOCAL": "http://127.0.0.1:9999"}):
            self.assertEqual(
                gbc.resolve_base_url(doc, "local", "http://127.0.0.1:8080"),
                "http://127.0.0.1:8080",
            )
            self.assertEqual(gbc.resolve_base_url(doc, "local", None), "http://127.0.0.1:9999")


class BaseUrlValidationTests(unittest.TestCase):
    def test_accepts(self) -> None:
        self.assertEqual(gbc.validate_base_url("https://api.example.com", True), "https://api.example.com")
        self.assertEqual(gbc.validate_base_url("https://api.example.com/", True), "https://api.example.com")
        self.assertEqual(gbc.validate_base_url("http://127.0.0.1:8080", False), "http://127.0.0.1:8080")

    def test_rejects(self) -> None:
        bad = [
            "ftp://api.example.com",
            "api.example.com",
            "https://api.example.com:8443/path",
            "https://api.example.com/path",
            "https://api.example.com?x=1",
            "https://api.example.com#frag",
            "https://user:secret@api.example.com",
            " https://api.example.com",
            "https://api.example.com ",
            "",
        ]
        for value in bad:
            with self.assertRaises(gbc.BuildConfigError, msg=value):
                gbc.validate_base_url(value, False)


class EmitTests(unittest.TestCase):
    def test_each_platform_carries_values_and_header(self) -> None:
        doc = load_config()
        base = "https://api.example.com"
        for platform in gbc.PLATFORMS:
            ua = gbc.user_agent(doc, platform)
            text = gbc.EMITTERS[platform]("prod", doc["appVersion"], base, ua)
            self.assertIn(base, text)
            self.assertIn(ua, text)
            self.assertIn("auto-generated", text)

    def test_windows_is_compile_time_constants(self) -> None:
        text = gbc.emit_windows("local", "0.1.0", "http://127.0.0.1:8080", "EliCloud/0.1.0 (windows)")
        self.assertIn("public const string BaseAddress", text)
        self.assertNotIn("Environment.GetEnvironmentVariable", text)

    def test_android_is_properties_for_gradle(self) -> None:
        text = gbc.emit_android("local", "0.1.0", "http://127.0.0.1:8080", "EliCloud/0.1.0 (android)")
        self.assertIn("baseUrl=http://127.0.0.1:8080", text)


if __name__ == "__main__":
    unittest.main()
