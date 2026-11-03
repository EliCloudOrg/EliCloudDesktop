#!/usr/bin/env python3
"""design/tokens.json → 四端令牌代码 + tokens.official.md / EXCEPTIONS.md / 覆盖率报告。

用法：
    python tools/gen-tokens/gen_tokens.py            # 写入生成物
    python tools/gen-tokens/gen_tokens.py --check    # 只校验生成物是否最新（CI 用）

退出码：0 = 成功；1 = 校验失败或生成物不是最新。
"""

from __future__ import annotations

import argparse
import difflib
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "shared"))

import designlib as dl  # noqa: E402

GEN = "gen-tokens"

EXIT_OK = 0
EXIT_FAIL = 1


def build_outputs(root: Path, doc: dict) -> dict[Path, str]:
    outputs = {
        root / dl.OUTPUT_PATHS["csharp"]: dl.emit_csharp(doc),
        root / dl.OUTPUT_PATHS["swift"]: dl.emit_swift(doc),
        root / dl.OUTPUT_PATHS["kotlin"]: dl.emit_kotlin(doc),
        root / dl.OUTPUT_PATHS["python"]: dl.emit_python(doc),
        root / dl.COVERAGE_DOC: dl.render_coverage(doc),
    }

    official_path = root / dl.OFFICIAL_DOC
    exceptions_path = root / dl.EXCEPTIONS_DOC
    if official_path.exists():
        outputs[official_path] = dl.replace_generated_block(
            official_path.read_text(encoding="utf-8"),
            "official-map",
            dl.render_official_table(doc),
        )
    if exceptions_path.exists():
        outputs[exceptions_path] = dl.replace_generated_block(
            exceptions_path.read_text(encoding="utf-8"),
            "token-exceptions",
            dl.render_exception_table(doc),
        )
    return outputs


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate design tokens for all four platforms.")
    parser.add_argument("--check", action="store_true", help="只校验，不写文件")
    parser.add_argument("--repo-root", type=Path, default=None, help="仓库根目录（默认自动推断）")
    args = parser.parse_args(argv)

    root = (args.repo_root or dl.repo_root()).resolve()
    tokens_path = root / "design" / "tokens.json"
    if not tokens_path.exists():
        print(f"[{GEN}] 找不到 {tokens_path}", file=sys.stderr)
        return EXIT_FAIL

    try:
        doc = dl.load_tokens(tokens_path)
        errors = dl.validate(doc)
    except dl.TokenError as ex:
        print(f"[{GEN}] {ex}", file=sys.stderr)
        return EXIT_FAIL

    if errors:
        print(f"[{GEN}] 令牌校验失败（{len(errors)} 项）：", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return EXIT_FAIL

    outputs = build_outputs(root, doc)
    stale: list[Path] = []
    for path, content in outputs.items():
        current = path.read_text(encoding="utf-8") if path.exists() else None
        if current == content:
            continue
        if args.check:
            stale.append(path)
            old = (current or "").splitlines(keepends=True)
            new = content.splitlines(keepends=True)
            diff = list(difflib.unified_diff(old, new, fromfile=str(path), tofile=str(path) + " (expected)", n=2))
            print(f"[{GEN}] 生成物不是最新：{path.relative_to(root)}", file=sys.stderr)
            print("".join(diff[:40]), file=sys.stderr)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8", newline="\n")
            print(f"[{GEN}] 写入 {path.relative_to(root)}")

    if args.check:
        if stale:
            print(f"[{GEN}] 有 {len(stale)} 个文件需要重新生成；运行 python tools/gen-tokens/gen_tokens.py", file=sys.stderr)
            return EXIT_FAIL
        print(f"[{GEN}] 生成物已是最新。")
        return EXIT_OK

    coverage = dl.render_coverage(doc)
    total_line = next((line for line in coverage.splitlines() if line.startswith("- 官方来源：")), "")
    print(f"[{GEN}] 完成。{total_line}")
    return EXIT_OK


if __name__ == "__main__":
    raise SystemExit(main())
