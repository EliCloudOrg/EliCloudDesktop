#!/usr/bin/env python3
"""contracts/copy/zh-CN.json → .resw / .strings / strings.xml / .po / copy.py。

用法：
    python tools/gen-copy/gen_copy.py            # 写入生成物
    python tools/gen-copy/gen_copy.py --check    # 只校验（CI 用）

校验内容：
  - key 命名与分组、占位符声明/使用双向一致、错误文案不含错误码、界面文案不含基础设施术语；
  - errors.json 的每个 copyKey 都存在；
  - Android 资源名无冲突；
  - 每个生成物回读后 key 覆盖率 100%（缺 key 直接失败）。
"""

from __future__ import annotations

import argparse
import difflib
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "shared"))

import copylib as cl  # noqa: E402
import designlib as dl  # noqa: E402

GEN = "gen-copy"


def build_outputs(root: Path, doc: dict, errors_doc: dict | None) -> dict[Path, str]:
    resw = cl.render_resw(doc)
    return {
        root / cl.OUTPUT_PATHS["windows_app_resw_neutral"]: resw,
        root / cl.OUTPUT_PATHS["windows_app_resw"]: resw,
        root / cl.OUTPUT_PATHS["windows_copy_keys"]: cl.render_csharp_keys(doc, errors_doc),
        root / cl.OUTPUT_PATHS["windows_resw"]: resw,
        root / cl.OUTPUT_PATHS["macos_strings"]: cl.render_strings(doc),
        root / cl.OUTPUT_PATHS["android_strings"]: cl.render_android_strings(doc),
        root / cl.OUTPUT_PATHS["linux_po"]: cl.render_po(doc),
        root / cl.OUTPUT_PATHS["linux_python"]: cl.render_python(doc),
        root / cl.OUTPUT_PATHS["android_keymap"]: cl.render_android_keymap(doc),
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate platform copy resources.")
    parser.add_argument("--check", action="store_true", help="只校验，不写文件")
    parser.add_argument("--repo-root", type=Path, default=None, help="仓库根目录（默认自动推断）")
    args = parser.parse_args(argv)

    root = (args.repo_root or dl.repo_root()).resolve()
    copy_path = root / cl.COPY_INPUT
    errors_path = root / "contracts" / "errors.json"

    try:
        doc = cl.load_copy(copy_path)
        errors_doc = cl.load_copy(errors_path) if errors_path.exists() else None
    except cl.CopyError as ex:
        print(f"[{GEN}] {ex}", file=sys.stderr)
        return cl.EXIT_FAIL

    problems = cl.validate_copy(doc, errors_doc)
    if problems:
        print(f"[{GEN}] 文案校验失败（{len(problems)} 项）：", file=sys.stderr)
        for problem in problems:
            print(f"  - {problem}", file=sys.stderr)
        return cl.EXIT_FAIL

    keys = set(doc["strings"])
    outputs = build_outputs(root, doc, errors_doc)
    verify = {
        cl.OUTPUT_PATHS["windows_app_resw_neutral"]: cl.verify_resw,
        cl.OUTPUT_PATHS["windows_app_resw"]: cl.verify_resw,
        cl.OUTPUT_PATHS["windows_copy_keys"]: lambda text, expected: cl.verify_csharp_keys(text, expected, errors_doc),
        cl.OUTPUT_PATHS["windows_resw"]: cl.verify_resw,
        cl.OUTPUT_PATHS["macos_strings"]: cl.verify_strings,
        cl.OUTPUT_PATHS["android_strings"]: cl.verify_android,
        cl.OUTPUT_PATHS["linux_po"]: cl.verify_po,
        cl.OUTPUT_PATHS["linux_python"]: cl.verify_python,
    }
    for relative, check in verify.items():
        path = root / relative
        text = outputs[path]
        problems.extend(check(text, keys))
    if problems:
        print(f"[{GEN}] 生成物覆盖率校验失败：", file=sys.stderr)
        for problem in problems:
            print(f"  - {problem}", file=sys.stderr)
        return cl.EXIT_FAIL

    stale: list[Path] = []
    for path, content in outputs.items():
        current = path.read_text(encoding="utf-8") if path.exists() else None
        if current == content:
            continue
        if args.check:
            stale.append(path)
            diff = difflib.unified_diff(
                (current or "").splitlines(keepends=True),
                content.splitlines(keepends=True),
                fromfile=str(path),
                tofile=str(path) + " (expected)",
                n=2,
            )
            print(f"[{GEN}] 生成物不是最新：{path.relative_to(root)}", file=sys.stderr)
            print("".join(list(diff)[:40]), file=sys.stderr)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8", newline="\n")
            print(f"[{GEN}] 写入 {path.relative_to(root)}")

    if args.check:
        if stale:
            print(f"[{GEN}] 有 {len(stale)} 个文件需要重新生成；运行 python tools/gen-copy/gen_copy.py", file=sys.stderr)
            return cl.EXIT_FAIL
        print(f"[{GEN}] 生成物已是最新。")
        return cl.EXIT_OK

    print(f"[{GEN}] 完成。{cl.coverage_report(doc)}")
    return cl.EXIT_OK


if __name__ == "__main__":
    raise SystemExit(main())
