#!/usr/bin/env python3
"""构建时配置注入（入口地址 / 版本 / UA）——四端共用。

设计原则与 Windows 现有 `GeneratePlatformConfig` 一致：
  - 入口地址**构建时注入、运行时只读**：不读环境变量、不读配置文件、界面不显示；
  - debug/local 与 release/prod 两套值；prod 必须是 HTTPS；
  - 值非法时**构建失败**，而不是运行时崩溃；
  - 生成物是构建产物，不入库（CI 用 secrets 提供 prod 地址）。

用法：
    # 本地开发
    python tools/gen-build-config/gen_build_config.py --environment local --platform windows

    # CI / 发布（地址来自 secrets）
    ELICLOUD_BASE_URL_PROD=https://api.example.com \
      python tools/gen-build-config/gen_build_config.py --environment prod --platform android

    # 一次生成四端（本地冒烟）
    python tools/gen-build-config/gen_build_config.py --environment local --platform all

    # 只校验配置与值，不写文件
    python tools/gen-build-config/gen_build_config.py --environment prod --platform windows --check
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path
from urllib.parse import urlsplit

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "shared"))

import designlib as dl  # noqa: E402

GEN = "gen-build-config"
CONFIG_PATH = "design/build-config.json"
PLATFORMS = ("windows", "macos", "android", "linux")
SEMVER_RE = re.compile(r"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$")
PLATFORM_NAME_RE = re.compile(r"^[a-z][a-z0-9]*$")


class BuildConfigError(Exception):
    pass


def load_config(path: Path) -> dict:
    doc = dl.strict_json_load(path.read_text(encoding="utf-8"), str(path))
    if not isinstance(doc, dict):
        raise BuildConfigError(f"{path}: 顶层必须是对象")
    if doc.get("version") != 1:
        raise BuildConfigError("design/build-config.json: version 必须是 1")
    if not SEMVER_RE.match(str(doc.get("appVersion", ""))):
        raise BuildConfigError(f"design/build-config.json: appVersion 不是 semver：{doc.get('appVersion')!r}")
    environments = doc.get("environments")
    if not isinstance(environments, dict) or set(environments) != {"local", "prod"}:
        raise BuildConfigError("design/build-config.json: environments 必须恰好是 local 与 prod")
    for name, meta in environments.items():
        if not isinstance(meta, dict) or not isinstance(meta.get("baseUrl"), dict):
            raise BuildConfigError(f"environments.{name}: 缺少 baseUrl")
    platforms = doc.get("platforms")
    if not isinstance(platforms, dict) or set(platforms) != set(PLATFORMS):
        raise BuildConfigError(f"design/build-config.json: platforms 必须恰好是 {PLATFORMS}")
    for name, meta in platforms.items():
        if not isinstance(meta, dict) or not PLATFORM_NAME_RE.match(str(meta.get("name", ""))):
            raise BuildConfigError(f"platforms.{name}: name 必须是平台标识")
        if not str(meta.get("output", "")).strip():
            raise BuildConfigError(f"platforms.{name}: 缺少 output")
    user_agent = doc.get("userAgent")
    if not isinstance(user_agent, str) or "{version}" not in user_agent or "{platform}" not in user_agent:
        raise BuildConfigError("userAgent 必须包含 {version} 与 {platform}")
    return doc


def resolve_base_url(doc: dict, environment: str, explicit: str | None) -> str:
    if environment not in doc["environments"]:
        raise BuildConfigError(f"未知环境 {environment!r}（可选：local / prod）")
    meta = doc["environments"][environment]["baseUrl"]
    value = explicit or os.environ.get(str(meta.get("env", "")), "") or meta.get("default")
    if not value:
        env_name = meta.get("env")
        raise BuildConfigError(
            f"环境 {environment} 没有入口地址：请传 --base-url，或设置环境变量 {env_name}"
        )
    return validate_base_url(str(value), require_https=bool(doc["environments"][environment].get("requireHttps")))


def validate_base_url(value: str, require_https: bool) -> str:
    text = value.strip()
    if not text or text != value:
        raise BuildConfigError("入口地址不能为空，也不能有首尾空白")
    if any(ch.isspace() for ch in text):
        raise BuildConfigError(f"入口地址不能包含空白：{text!r}")
    parts = urlsplit(text)
    if parts.scheme not in ("http", "https"):
        raise BuildConfigError(f"入口地址只接受 http/https：{text!r}")
    if require_https and parts.scheme != "https":
        raise BuildConfigError(f"prod 环境必须使用 HTTPS：{text!r}")
    if not parts.hostname:
        raise BuildConfigError(f"入口地址必须是绝对地址（含主机名）：{text!r}")
    if parts.username or parts.password:
        raise BuildConfigError("入口地址不能包含用户名/密码")
    if parts.query or parts.fragment:
        raise BuildConfigError("入口地址不能包含查询串或片段")
    if parts.path not in ("", "/"):
        raise BuildConfigError(f"入口地址不能带路径（当前：{parts.path}）")
    return text.rstrip("/")


def user_agent(doc: dict, platform: str) -> str:
    platform_name = str(doc["platforms"][platform]["name"])
    value = str(doc["userAgent"]).format(version=doc["appVersion"], platform=platform_name)
    if not re.fullmatch(r"EliCloud/\S+ \([a-z][a-z0-9]*\)", value):
        raise BuildConfigError(f"生成出来的 UA 不合法：{value!r}")
    if "://" in value:
        raise BuildConfigError("UA 不允许包含入口地址")
    return value


def emit_windows(environment: str, app_version: str, base_url: str, ua: str) -> str:
    return "\n".join([
        "// <auto-generated> from design/build-config.json",
        "// 构建时注入；不要手改，也不要在运行时读取环境变量或配置文件。",
        "// 这是构建产物，不入库；prod 地址由 CI secrets 提供。",
        "#nullable enable",
        "",
        "namespace EliCloud.Generated;",
        "",
        "/// <summary>构建时注入的平台入口配置（编译期常量）。</summary>",
        "public static class BuildConfig",
        "{",
        f'    public const string Environment = "{environment}";',
        f'    public const string AppVersion = "{app_version}";',
        f'    public const string BaseAddress = "{base_url}";',
        f'    public const string UserAgent = "{ua}";',
        "}",
        "",
    ])


def emit_swift(environment: str, app_version: str, base_url: str, ua: str) -> str:
    return "\n".join([
        "// <auto-generated> from design/build-config.json",
        "// 构建时注入；不要手改，也不要在运行时读取环境变量或配置文件。",
        "// 这是构建产物，不入库；prod 地址由 CI secrets 提供。",
        "",
        "import Foundation",
        "",
        "/// 构建时注入的平台入口配置。",
        "public enum BuildConfig {",
        f'    public static let environment = "{environment}"',
        f'    public static let appVersion = "{app_version}"',
        f'    public static let baseAddress = "{base_url}"',
        f'    public static let userAgent = "{ua}"',
        "}",
        "",
    ])


def emit_android(environment: str, app_version: str, base_url: str, ua: str) -> str:
    return "\n".join([
        "# <auto-generated> from design/build-config.json — 构建时注入，不入库。",
        f"environment={environment}",
        f"appVersion={app_version}",
        f"baseUrl={base_url}",
        f"userAgent={ua}",
        "",
    ])


def emit_linux(environment: str, app_version: str, base_url: str, ua: str) -> str:
    return "\n".join([
        "# <auto-generated> from design/build-config.json",
        "# 构建时注入；不要手改，也不要在运行时读取环境变量或配置文件。",
        "# 这是构建产物，不入库；prod 地址由 CI secrets 提供。",
        "",
        f'ENVIRONMENT = "{environment}"',
        f'APP_VERSION = "{app_version}"',
        f'BASE_URL = "{base_url}"',
        f'USER_AGENT = "{ua}"',
        "",
    ])


EMITTERS = {
    "windows": emit_windows,
    "macos": emit_swift,
    "android": emit_android,
    "linux": emit_linux,
}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Inject build-time platform config.")
    parser.add_argument("--environment", choices=("local", "prod"), required=True)
    parser.add_argument("--platform", choices=(*PLATFORMS, "all"), required=True)
    parser.add_argument("--base-url", default=None, help="显式入口地址（优先于环境变量）")
    parser.add_argument("--repo-root", type=Path, default=None)
    parser.add_argument("--out", type=Path, default=None, help="覆盖输出路径（单平台时可用）")
    parser.add_argument("--check", action="store_true", help="只校验，不写文件")
    args = parser.parse_args(argv)

    root = (args.repo_root or dl.repo_root()).resolve()
    try:
        doc = load_config(root / CONFIG_PATH)
        base_url = resolve_base_url(doc, args.environment, args.base_url)
        platforms = PLATFORMS if args.platform == "all" else (args.platform,)
        outputs: list[tuple[Path, str]] = []
        for platform in platforms:
            ua = user_agent(doc, platform)
            content = EMITTERS[platform](args.environment, str(doc["appVersion"]), base_url, ua)
            out = args.out if args.out is not None else root / str(doc["platforms"][platform]["output"])
            if args.platform == "all" and args.out is not None:
                raise BuildConfigError("--out 只能和单个 --platform 一起用")
            outputs.append((out, content))
    except BuildConfigError as ex:
        print(f"[{GEN}] {ex}", file=sys.stderr)
        return 1

    if args.check:
        for out, _ in outputs:
            print(f"[{GEN}] 校验通过；将写入 {out}")
        return 0

    for out, content in outputs:
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(content, encoding="utf-8", newline="\n")
        print(f"[{GEN}] 写入 {out}")

    for platform in platforms:
        print(f"[{GEN}] environment={args.environment} platform={platform} UA={user_agent(doc, platform)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
