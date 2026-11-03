"""文案（contracts/copy/zh-CN.json）的加载、校验与四端资源生成。

只依赖标准库。占位符统一是 `{name}`，由各端按 `placeholders.<name>.type` 替换；
生成器不把占位符改写成任何平台专有格式（`%1$s` / `%@`），否则四端文案会漂移。
"""

from __future__ import annotations

import json
import re
from pathlib import Path

import designlib as dl

COPY_INPUT = "contracts/copy/zh-CN.json"

OUTPUT_PATHS = {
    # 主应用直接消费的 .resw（MRT/ResourceLoader 读 resources.pri）与 key 常量表。
    # 两者都是生成物：文案本体只存在于 .resw，C# 侧只有 key，不落第二份文案。
    # 中性（不带语言后缀）的那份是 resources.pri 的默认回退，缺了它 PRI 会报
    # PRI263「No default or neutral resource given」，非中文系统下取不到任何文案。
    "windows_app_resw_neutral": "src/EliCloudDesktop/Strings/Resources.resw",
    "windows_app_resw": "src/EliCloudDesktop/Strings/zh-CN/Resources.resw",
    "windows_copy_keys": "src/EliCloudDesktop/Generated/CopyKeys.g.cs",
    # Stage 2 hello-world 的消费证明（纯 C# 工程，不引 WinUI）。
    "windows_resw": "clients/windows/hello/Strings/zh-CN/Resources.resw",
    "macos_strings": "clients/macos/Resources/zh-Hans.lproj/Localizable.strings",
    "android_strings": "clients/android/app/src/main/res/values/strings.xml",
    "linux_po": "clients/linux/locale/zh_CN/LC_MESSAGES/elicloud.po",
    "linux_python": "clients/linux/src/elicloud/copy.py",
    "android_keymap": "design/generated/copy-keys.json",
}

# 生成 C# key 常量时用的命名空间/文件名；主应用与测试都按这个约定引用。
CSHARP_KEYS_NAMESPACE = "EliCloudDesktop.Generated"

KEY_RE = re.compile(r"^[a-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)+$")
PLACEHOLDER_RE = re.compile(r"\{([a-z][A-Za-z0-9]*)\}")

GROUPS = ("app", "nav", "common", "status", "login", "verify", "home", "service",
          "profile", "mc", "settings", "error")

BANNED_TOKENS = ("RCON", "rcon", "/v1/", "/auth/", "http://", "https://", "scope", "token")
ERROR_CODE_TOKENS = ("invalid_", "rate_limited", "too_many_requests", "not_found")

EXIT_OK = 0
EXIT_FAIL = 1


class CopyError(Exception):
    pass


def android_name(key: str) -> str:
    """Android 资源名只允许小写字母/数字/下划线。"""
    words: list[str] = []
    for segment in key.split("."):
        words.extend(_split_words(segment))
    return "_".join(w.lower() for w in words)


def _split_words(segment: str) -> list[str]:
    parts = re.split(r"[-_]", segment)
    out: list[str] = []
    for part in parts:
        out.extend(re.findall(r"[A-Z]+(?=[A-Z][a-z])|[A-Z]?[a-z0-9]+|[A-Z]+|[0-9]+", part))
    return [w for w in out if w]


def dotnet_name(key: str) -> str:
    """语义 key → C# 常量名（PascalCase）。与 gen-tokens 的命名规则一致。"""
    return dl.pascal(tuple(key.split(".")))


def strict_json_load(text: str, where: str) -> dict:
    def hook(pairs: list[tuple[str, object]]) -> dict:
        seen: dict[str, object] = {}
        for key, value in pairs:
            if key in seen:
                raise CopyError(f"{where}: 重复的 JSON 键 {key!r}")
            seen[key] = value
        return seen

    try:
        doc = json.loads(text, object_pairs_hook=hook)
    except json.JSONDecodeError as ex:
        raise CopyError(f"{where}: JSON 语法错误：{ex}") from ex
    if not isinstance(doc, dict):
        raise CopyError(f"{where}: 顶层必须是对象")
    return doc


def load_copy(path: Path) -> dict:
    return strict_json_load(path.read_text(encoding="utf-8"), str(path))


def validate_copy(doc: dict, errors_copy: dict | None = None) -> list[str]:
    errors: list[str] = []
    if doc.get("version") != 2:
        errors.append(f"copy version 必须是 2，实际是 {doc.get('version')!r}")
    if doc.get("locale") != "zh-CN":
        errors.append("copy locale 必须是 zh-CN")
    strings = doc.get("strings")
    if not isinstance(strings, dict) or not strings:
        return errors + ["copy 没有任何 strings"]

    placeholders = doc.get("placeholders") or {}
    declared = set(placeholders)
    used: set[str] = set()
    android_names: dict[str, str] = {}
    dotnet_names: dict[str, str] = {}

    for key, value in strings.items():
        where = f"strings[{key}]"
        if not isinstance(value, str) or not value:
            errors.append(f"{where}: 必须是非空字符串")
            continue
        if not KEY_RE.match(key):
            errors.append(f"{where}: key 必须是小写开头的点分语义键")
            continue
        group = key.split(".", 1)[0]
        if group not in GROUPS:
            errors.append(f"{where}: 未知分组 {group!r}（允许：{', '.join(GROUPS)}）")
        if key.startswith("error."):
            for token in ERROR_CODE_TOKENS:
                if token in value:
                    errors.append(f"{where}: 错误文案里不允许出现错误码片段 {token!r}")
            if len(value) > 60:
                errors.append(f"{where}: 错误文案过长（{len(value)} 字符，>60）")
        else:
            if len(value) > 120:
                errors.append(f"{where}: 界面文案过长（{len(value)} 字符，>120）")
        for token in BANNED_TOKENS:
            if token in value:
                errors.append(f"{where}: 文案里不允许出现基础设施术语 {token!r}")

        names = PLACEHOLDER_RE.findall(value)
        used.update(names)
        for name in names:
            if name not in declared:
                errors.append(f"{where}: 占位符 {{{name}}} 未在 placeholders 里声明")

        name = android_name(key)
        if name in android_names:
            errors.append(f"{where}: Android 资源名与 {android_names[name]} 冲突（{name}）")
        android_names[name] = key

        csharp_name = dotnet_name(key)
        if csharp_name in dotnet_names:
            errors.append(f"{where}: C# 常量名与 {dotnet_names[csharp_name]} 冲突（{csharp_name}）")
        dotnet_names[csharp_name] = key

    for name in sorted(declared - used):
        errors.append(f"placeholders.{name}: 已声明但没有任何文案使用")
    for name, meta in placeholders.items():
        if not isinstance(meta, dict) or meta.get("type") not in ("string", "integer"):
            errors.append(f"placeholders.{name}: type 必须是 string 或 integer")

    if errors_copy is not None:
        codes = errors_copy.get("codes") or {}
        for code, meta in codes.items():
            copy_key = meta.get("copyKey")
            if copy_key and copy_key not in strings:
                errors.append(f"errors.json[{code}] 的 copyKey 不存在：{copy_key}")
        if not codes:
            errors.append("errors.json 没有错误码")

    return errors


def _xml_escape(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def _android_escape(text: str) -> str:
    return _xml_escape(text).replace("\\", "\\\\").replace("'", "\\'").replace('"', "\\\"")


def _strings_escape(text: str) -> str:
    return text.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n")


def _py_string(text: str) -> str:
    return json.dumps(text, ensure_ascii=False)


def render_resw(doc: dict) -> str:
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        "<!-- <auto-generated> from contracts/copy/zh-CN.json — 不要手改；见 tools/gen-copy 。 -->",
        "<root>",
        '  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>',
        '  <resheader name="version"><value>2.0</value></resheader>',
        '  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>',
        '  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>',
    ]
    section = None
    for key, value in doc["strings"].items():
        group = key.split(".", 1)[0]
        if group != section:
            section = group
            lines.append(f"  <!-- {group} -->")
        lines.append(f'  <data name="{key}" xml:space="preserve">')
        lines.append(f"    <value>{_xml_escape(value)}</value>")
        lines.append("  </data>")
    lines.append("</root>")
    return "\n".join(lines) + "\n"


def render_strings(doc: dict) -> str:
    lines = [
        "/* <auto-generated> from contracts/copy/zh-CN.json — 不要手改；见 tools/gen-copy 。 */",
        "",
    ]
    section = None
    for key, value in doc["strings"].items():
        group = key.split(".", 1)[0]
        if group != section:
            section = group
            lines.append(f"/* {group} */")
        lines.append(f'"{_strings_escape(key)}" = "{_strings_escape(value)}";')
    return "\n".join(lines) + "\n"


def render_android_strings(doc: dict) -> str:
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        "<!-- <auto-generated> from contracts/copy/zh-CN.json — 不要手改；见 tools/gen-copy 。",
        "     语义 key → Android 资源名：点号与 camelCase 转小写下划线，完整映射见 design/generated/copy-keys.json。 -->",
        '<resources xmlns:tools="http://schemas.android.com/tools">',
    ]
    section = None
    for key, value in doc["strings"].items():
        group = key.split(".", 1)[0]
        if group != section:
            section = group
            lines.append(f"    <!-- {group} -->")
        lines.append(f'    <string name="{android_name(key)}">{_android_escape(value)}</string>')
    lines.append("</resources>")
    return "\n".join(lines) + "\n"


def render_po(doc: dict) -> str:
    lines = [
        "# <auto-generated> from contracts/copy/zh-CN.json — 不要手改；见 tools/gen-copy 。",
        'msgid ""',
        'msgstr ""',
        '"Project-Id-Version: EliCloud 1.0.0\\n"',
        '"Language: zh_CN\\n"',
        '"MIME-Version: 1.0\\n"',
        '"Content-Type: text/plain; charset=UTF-8\\n"',
        '"Content-Transfer-Encoding: 8bit\\n"',
        "",
    ]
    for key, value in doc["strings"].items():
        lines.append(f"#. {key}")
        lines.append(f'msgid "{_strings_escape(key)}"')
        lines.append(f'msgstr "{_strings_escape(value)}"')
        lines.append("")
    return "\n".join(lines)


def render_python(doc: dict) -> str:
    lines = [
        "# <auto-generated> from contracts/copy/zh-CN.json",
        "# 由 tools/gen-copy/gen_copy.py 生成；不要手改。",
        '"""四端共用的文案（Python dict）。key → 文案；占位符用 {name}。"""',
        "",
        "STRINGS: dict[str, str] = {",
    ]
    for key, value in doc["strings"].items():
        lines.append(f"    {_py_string(key)}: {_py_string(value)},")
    lines.append("}")
    lines.append("")
    lines.append("PLACEHOLDERS: dict[str, str] = {")
    for name, meta in (doc.get("placeholders") or {}).items():
        lines.append(f"    {_py_string(name)}: {_py_string(str(meta.get('type', 'string'))) },")
    lines.append("}")
    lines.append("")
    lines.append("")
    lines.append("def text(key: str, **values: object) -> str:")
    lines.append('    """取一条文案并替换占位符；缺 key 直接 KeyError（不静默回退）。"""')
    lines.append("    return STRINGS[key].format(**values)")
    return "\n".join(lines) + "\n"


def render_android_keymap(doc: dict) -> str:
    mapping = {
        "version": 1,
        "source": COPY_INPUT,
        "note": "语义 key → 各端资源名；Android 资源名由生成器确定性转换，其它端直接用语义 key。",
        "keys": {
            key: {
                "resw": key,
                "strings": key,
                "po": key,
                "android": android_name(key),
            }
            for key in doc["strings"]
        },
    }
    return json.dumps(mapping, ensure_ascii=False, indent=2) + "\n"


def render_csharp_keys(doc: dict, errors_doc: dict | None = None) -> str:
    """生成主应用消费的 C# key 常量表（不含文案本体）与错误码 → copyKey 映射。

    文案本体只在 .resw 里；这里只放 key 字符串，避免出现第二份用户可见文案。
    """
    strings = doc["strings"]
    codes = (errors_doc or {}).get("codes") or {}
    lines = [
        "// <auto-generated> from contracts/copy/zh-CN.json + contracts/errors.json",
        "// 由 tools/gen-copy/gen_copy.py 生成；不要手改。",
        "// 这里只有 key 与错误码映射；用户可见文案在 Strings/zh-CN/Resources.resw。",
        "#nullable enable",
        "",
        f"namespace {CSHARP_KEYS_NAMESPACE};",
        "",
        "/// <summary>文案 key（与 contracts/copy/zh-CN.json 一一对应）。</summary>",
        "internal static class CopyKeys",
        "{",
    ]
    for key in strings:
        lines.append(f'    public const string {dotnet_name(key)} = "{key}";')

    lines.extend([
        "}",
        "",
        "/// <summary>错误码 → 文案 key（来自 contracts/errors.json，不维护第二份映射）。</summary>",
        "internal static class ErrorCopyKeys",
        "{",
        "    /// <summary>取错误码对应的文案 key；未知错误码返回 null，由调用方回落到 unknown。</summary>",
        "    public static string? ForErrorCode(string? code) => code switch",
        "    {",
    ])
    for code in sorted(codes):
        meta = codes[code]
        copy_key = meta.get("copyKey") if isinstance(meta, dict) else None
        if not copy_key or copy_key not in strings:
            continue
        lines.append(f'        "{code}" => CopyKeys.{dotnet_name(copy_key)},')
    lines.extend([
        "        _ => null,",
        "    };",
        "}",
        "",
    ])
    return "\n".join(lines)


def verify_csharp_keys(text: str, keys: set[str], errors_doc: dict | None = None) -> list[str]:
    """回读 C# 生成物：key 覆盖 100%，且错误码映射与 errors.json 完全一致。"""
    problems: list[str] = []
    expected = {dotnet_name(key): key for key in keys}
    actual = dict(re.findall(r'public const string (\w+) = "([^"]+)";', text))

    missing = sorted(set(expected) - set(actual))
    extra = sorted(set(actual) - set(expected))
    if missing:
        problems.append(f"csharp keys: 缺少 {len(missing)} 个 key（例如 {missing[:3]}）")
    if extra:
        problems.append(f"csharp keys: 多出 {len(extra)} 个 key（例如 {extra[:3]}）")
    for name, key in expected.items():
        if actual.get(name) not in (None, key):
            problems.append(f"csharp keys: {name} 的 key 不一致（{actual.get(name)!r} != {key!r}）")

    codes = (errors_doc or {}).get("codes") or {}
    expected_arms = {
        code: dotnet_name(str(meta["copyKey"]))
        for code, meta in codes.items()
        if isinstance(meta, dict) and meta.get("copyKey") in keys
    }
    actual_arms = dict(re.findall(r'"([a-z0-9_]+)" => CopyKeys\.(\w+),', text))
    for code, name in expected_arms.items():
        if actual_arms.get(code) != name:
            problems.append(f"csharp errors: {code} 的映射缺失或不一致（期望 {name}，实际 {actual_arms.get(code)}）")
    for code in sorted(set(actual_arms) - set(expected_arms)):
        problems.append(f"csharp errors: 多出 errors.json 没有的错误码映射 {code}")
    return problems


# ── 回读校验：生成物必须覆盖全部 key ────────────────────────────


def verify_resw(text: str, keys: set[str]) -> list[str]:
    import xml.etree.ElementTree as ET

    root = ET.fromstring(text)
    names = {node.get("name") for node in root.findall("data")}
    return _coverage("resw", names, keys)


def verify_android(text: str, keys: set[str]) -> list[str]:
    import xml.etree.ElementTree as ET

    root = ET.fromstring(text)
    names = {node.get("name") for node in root.findall("string")}
    expected = {android_name(key) for key in keys}
    return _coverage("android strings.xml", names, expected)


def verify_strings(text: str, keys: set[str]) -> list[str]:
    names = set(re.findall(r'^"([^"]+)"\s*=\s*"', text, re.MULTILINE))
    return _coverage(".strings", names, keys)


def verify_po(text: str, keys: set[str]) -> list[str]:
    names = set(re.findall(r'^msgid "([^"]+)"$', text, re.MULTILINE)) - {""}
    return _coverage("po", names, keys)


def verify_python(text: str, keys: set[str]) -> list[str]:
    namespace: dict[str, object] = {}
    exec(compile(text, "<copy.py>", "exec"), namespace)  # noqa: S102 - 生成物，非外部输入
    strings = namespace.get("STRINGS")
    if not isinstance(strings, dict):
        return ["copy.py: 没有 STRINGS 字典"]
    return _coverage("copy.py", set(strings), keys)


def _coverage(where: str, actual: set[str], expected: set[str]) -> list[str]:
    missing = sorted(expected - actual)
    extra = sorted(actual - expected)
    problems: list[str] = []
    if missing:
        problems.append(f"{where}: 缺少 {len(missing)} 个 key（例如 {missing[:3]}）")
    if extra:
        problems.append(f"{where}: 多出 {len(extra)} 个 key（例如 {extra[:3]}）")
    return problems


def coverage_report(doc: dict) -> str:
    strings = doc["strings"]
    by_group: dict[str, int] = {}
    for key in strings:
        group = key.split(".", 1)[0]
        by_group[group] = by_group.get(group, 0) + 1
    parts = ", ".join(f"{group} {count}" for group, count in by_group.items())
    return f"{len(strings)} 条文案（{parts}）；输出 5 端资源 + 1 份 key 映射"
