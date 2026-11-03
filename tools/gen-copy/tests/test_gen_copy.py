"""gen-copy 的单元测试：快照 + 校验失败用例 + 生成物回读覆盖率。

运行：
    python -m unittest discover -s tools/gen-copy/tests -v
"""

from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "shared"))

import copylib as cl  # noqa: E402

FIXTURE = ROOT / "tools" / "gen-copy" / "tests" / "fixtures" / "valid"
ERRORS_STUB = {"codes": {"rate_limited": {"copyKey": "error.transport.rate_limited"}}}


def load_fixture() -> dict:
    return cl.load_copy(FIXTURE / "zh-CN.json")


class SnapshotTests(unittest.TestCase):
    def assert_snapshot(self, name: str, actual: str) -> None:
        expected = (FIXTURE / "expected" / name).read_text(encoding="utf-8")
        self.assertEqual(expected, actual, f"{name} 与快照不一致")

    def test_resw_snapshot(self) -> None:
        self.assert_snapshot("Resources.resw", cl.render_resw(load_fixture()))

    def test_strings_snapshot(self) -> None:
        self.assert_snapshot("Localizable.strings", cl.render_strings(load_fixture()))

    def test_android_snapshot(self) -> None:
        self.assert_snapshot("strings.xml", cl.render_android_strings(load_fixture()))

    def test_po_snapshot(self) -> None:
        self.assert_snapshot("elicloud.po", cl.render_po(load_fixture()))

    def test_python_snapshot(self) -> None:
        self.assert_snapshot("copy.py", cl.render_python(load_fixture()))

    def test_keymap_snapshot(self) -> None:
        self.assert_snapshot("copy-keys.json", cl.render_android_keymap(load_fixture()))

    def test_csharp_keys_snapshot(self) -> None:
        self.assert_snapshot("CopyKeys.g.cs", cl.render_csharp_keys(load_fixture(), ERRORS_STUB))


class CoverageTests(unittest.TestCase):
    def setUp(self) -> None:
        self.doc = load_fixture()
        self.keys = set(self.doc["strings"])

    def test_valid_fixture(self) -> None:
        self.assertEqual(cl.validate_copy(self.doc, ERRORS_STUB), [])

    def test_all_outputs_cover_all_keys(self) -> None:
        self.assertEqual(cl.verify_resw(cl.render_resw(self.doc), self.keys), [])
        self.assertEqual(cl.verify_strings(cl.render_strings(self.doc), self.keys), [])
        self.assertEqual(cl.verify_android(cl.render_android_strings(self.doc), self.keys), [])
        self.assertEqual(cl.verify_po(cl.render_po(self.doc), self.keys), [])
        self.assertEqual(cl.verify_python(cl.render_python(self.doc), self.keys), [])
        self.assertEqual(
            cl.verify_csharp_keys(cl.render_csharp_keys(self.doc, ERRORS_STUB), self.keys, ERRORS_STUB),
            [])

    def test_csharp_error_mapping_follows_errors_json(self) -> None:
        text = cl.render_csharp_keys(self.doc, ERRORS_STUB)
        broken = {"codes": {"rate_limited": {"copyKey": "app.title"}}}
        problems = cl.verify_csharp_keys(text, self.keys, broken)
        self.assertTrue(any("rate_limited" in problem for problem in problems), problems)

    def test_missing_key_detected(self) -> None:
        keys = set(self.keys) | {"login.missing.key"}
        problems = cl.verify_resw(cl.render_resw(self.doc), keys)
        self.assertTrue(any("缺少" in problem for problem in problems), problems)

    def test_android_name_mapping_and_collision(self) -> None:
        self.assertEqual(cl.android_name("login.usernameOrEmail.label"), "login_username_or_email_label")
        doc = copy.deepcopy(self.doc)
        doc["strings"]["login.username_or_email.label"] = "冲突"
        problems = cl.validate_copy(doc, ERRORS_STUB)
        self.assertTrue(any("Android 资源名" in problem for problem in problems), problems)


class ValidationFailureTests(unittest.TestCase):
    def setUp(self) -> None:
        self.doc = load_fixture()

    def test_undeclared_placeholder(self) -> None:
        self.doc["strings"]["verify.sent"] = "已向 {address} 发送邮件。"
        problems = cl.validate_copy(self.doc, ERRORS_STUB)
        self.assertTrue(any("{address}" in problem for problem in problems), problems)

    def test_unused_placeholder(self) -> None:
        self.doc["placeholders"]["unused"] = {"type": "string"}
        problems = cl.validate_copy(self.doc, ERRORS_STUB)
        self.assertTrue(any("已声明但没有任何文案使用" in problem for problem in problems), problems)

    def test_banned_terminology(self) -> None:
        self.doc["strings"]["app.title"] = "EliCloud RCON"
        problems = cl.validate_copy(self.doc, ERRORS_STUB)
        self.assertTrue(any("RCON" in problem for problem in problems), problems)

    def test_error_key_needs_errors_json(self) -> None:
        problems = cl.validate_copy(self.doc, {"codes": {}})
        self.assertTrue(any("errors.json 没有错误码" in problem for problem in problems), problems)
        problems = cl.validate_copy(self.doc, {"codes": {"x": {"copyKey": "error.does.not.exist"}}})
        self.assertTrue(any("copyKey 不存在" in problem for problem in problems), problems)

    def test_unknown_group_rejected(self) -> None:
        self.doc["strings"]["bogus.key"] = "x"
        problems = cl.validate_copy(self.doc, ERRORS_STUB)
        self.assertTrue(any("未知分组" in problem for problem in problems), problems)

    def test_duplicate_json_key_rejected(self) -> None:
        with self.assertRaises(cl.CopyError):
            cl.strict_json_load('{"strings": {}, "strings": {}}', "<test>")

    def test_python_text_helper(self) -> None:
        namespace: dict[str, object] = {}
        exec(compile(cl.render_python(self.doc), "<copy.py>", "exec"), namespace)
        text = namespace["text"]
        self.assertEqual(text("mc.whitelist.count", count=3), "共 3 人")
        self.assertEqual(text("verify.sent", email="a@b.c"), "我们已向 a@b.c 发送了一封验证邮件。")


if __name__ == "__main__":
    unittest.main()
