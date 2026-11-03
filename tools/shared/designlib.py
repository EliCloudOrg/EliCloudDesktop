"""Stage 2 共享工具的公共实现：设计令牌的加载、校验与四端代码生成。

只依赖标准库：CI 与本地都不需要额外安装包。

令牌模型（design/tokens.json）：
  - 顶层 section：color / type / space / radius / motion / density / material / brand。
  - 叶子是一个“按平台解析”的对象：
        叶子上的字段是四端共享的默认值；
        `platforms.<platform>` 里的字段覆盖默认值。
    解析后每个平台都必须得到 source=official|custom。
  - official：必须有非空 ref；emit=comment 表示官方只有机制、没有可寻址令牌。
  - custom：必须有 value + reason + approvedBy + scope；颜色用 `#RRGGBB` + 可选 alpha。

本模块同时负责：
  - 生成四端令牌文件（C# / Swift / Kotlin / Python）；
  - 生成 `design/tokens.official.md` 的映射表；
  - 生成 `design/EXCEPTIONS.md` 的令牌例外登记表；
  - 生成 `design/generated/official-coverage.md` 覆盖率报告。
"""

from __future__ import annotations

import json
import re
from pathlib import Path

PLATFORMS = ("windows", "macos", "android", "linux")

SECTIONS = ("color", "type", "space", "radius", "motion", "density", "material", "brand")

PAYLOAD_KEYS = {
    "source",
    "ref",
    "value",
    "alpha",
    "reason",
    "approvedBy",
    "scope",
    "emit",
    "note",
    "platforms",
}

TOP_LEVEL_KEYS = {"version", "meta", *SECTIONS}

OFFICIAL_DOC = "design/tokens.official.md"
EXCEPTIONS_DOC = "design/EXCEPTIONS.md"
COVERAGE_DOC = "design/generated/official-coverage.md"
GENERATED_MARK = "<!-- BEGIN GENERATED: {name} -->"
GENERATED_END = "<!-- END GENERATED: {name} -->"

OUTPUT_PATHS = {
    "csharp": "clients/windows/hello/Generated/DesignTokens.g.cs",
    "swift": "clients/macos/Generated/DesignTokens.swift",
    "kotlin": "clients/android/app/src/main/java/cloud/elipese/hello/Tokens.kt",
    "python": "clients/linux/src/elicloud/design/tokens.py",
}

GENERATED_HEADER = "from design/tokens.json"

COLOR_RE = re.compile(r"^#[0-9A-Fa-f]{6}$")

# 官方引用的“形态”白名单：生成器无法知道平台资源库里有没有这个键（那需要各端的资源表），
# 但可以拦住“把裸值/拼错的形态写进 ref”。最终可解析性由各端编译/运行验证。
_SWIFT_PREFIXES = (
    "Color(", "Color.", "Font.", "HierarchicalShapeStyle.", "BackgroundStyle.",
    "Material.", "ShapeStyle.",
)
_KOTLIN_PREFIXES = ("MaterialTheme.", "Color(", "FontFamily.", "FontWeight.")
_LINUX_STYLE_CLASSES = {
    "title-1", "title-2", "title-3", "title-4", "large-title", "heading", "body",
    "caption", "caption-heading", "dim-label", "monospace", "card", "boxed-list",
}
_LINUX_FONT_FAMILIES = {"Cantarell", "Inter"}
_WINDOWS_REF_RE = re.compile(r"^[A-Za-z][A-Za-z0-9 ._-]*$")


def validate_ref_shape(platform: str, ref: str, emit: str) -> str | None:
    if emit == "comment":
        return None
    if platform == "windows":
        if not _WINDOWS_REF_RE.match(ref):
            return f"ref {ref!r} 不是合法的 WinUI 资源键/样式键/字体族"
        return None
    if platform == "macos":
        if not ref.startswith(_SWIFT_PREFIXES):
            return f"ref {ref!r} 不是 SwiftUI 官方样式表达式（应以 {', '.join(_SWIFT_PREFIXES[:4])}… 开头）"
        return None
    if platform == "android":
        if not ref.startswith(_KOTLIN_PREFIXES):
            return f"ref {ref!r} 不是 Compose/M3 官方引用（应以 {', '.join(_KOTLIN_PREFIXES)} 开头）"
        return None
    if platform == "linux":
        if ref.startswith("@"):
            return None
        if ref in _LINUX_STYLE_CLASSES or ref in _LINUX_FONT_FAMILIES:
            return None
        return f"ref {ref!r} 既不是 GTK CSS 命名颜色（@…），也不是已登记的 Adwaita style class"
    return None


class TokenError(Exception):
    """令牌文件本身不合法（JSON 语法、重复键、结构错误）。"""


def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def strict_json_load(text: str, where: str) -> object:
    """解析 JSON，并拒绝重复键（Python 默认会静默保留最后一个）。"""

    def hook(pairs: list[tuple[str, object]]) -> dict[str, object]:
        seen: dict[str, object] = {}
        for key, value in pairs:
            if key in seen:
                raise TokenError(f"{where}: 重复的 JSON 键 {key!r}")
            seen[key] = value
        return seen

    try:
        return json.loads(text, object_pairs_hook=hook)
    except json.JSONDecodeError as ex:
        raise TokenError(f"{where}: JSON 语法错误：{ex}") from ex


def load_tokens(path: Path) -> dict:
    doc = strict_json_load(path.read_text(encoding="utf-8"), str(path))
    if not isinstance(doc, dict):
        raise TokenError(f"{path}: 顶层必须是对象")
    return doc


def iter_leaves(doc: dict) -> list[tuple[tuple[str, ...], dict]]:
    """展开成 (路径, 原始叶子) 列表；结构错误直接抛 TokenError。"""
    unknown = set(doc) - TOP_LEVEL_KEYS
    if unknown:
        raise TokenError(f"design/tokens.json: 未知顶层键 {sorted(unknown)}")

    leaves: list[tuple[tuple[str, ...], dict]] = []
    for section in SECTIONS:
        if section not in doc:
            raise TokenError(f"design/tokens.json: 缺少 section {section!r}")
        _walk(doc[section], (section,), leaves)
    return leaves


def _walk(node: object, path: tuple[str, ...], out: list[tuple[tuple[str, ...], dict]]) -> None:
    where = ".".join(path)
    if not isinstance(node, dict):
        raise TokenError(f"{where}: 必须是对象")
    if not node:
        raise TokenError(f"{where}: 不能是空对象")

    child = {k: v for k, v in node.items() if k not in PAYLOAD_KEYS}
    payload = {k: v for k, v in node.items() if k in PAYLOAD_KEYS}
    if child and payload:
        raise TokenError(f"{where}: 不能同时是叶子和分组（既有令牌字段又有子键 {sorted(child)}）")
    if child:
        for key, value in child.items():
            if not re.fullmatch(r"[a-z][A-Za-z0-9]*|[0-9]+", key):
                raise TokenError(f"{where}.{key}: 键名必须是 lowerCamelCase 或数字")
            _walk(value, (*path, key), out)
        return
    out.append((path, node))


def resolve_platform(entry: dict, platform: str) -> dict:
    """把叶子按平台合并；source 变化时丢掉另一类形态的字段。"""
    base = {k: v for k, v in entry.items() if k != "platforms"}
    platforms = entry.get("platforms") or {}
    if not isinstance(platforms, dict):
        raise TokenError("platforms 必须是对象")
    unknown = set(platforms) - set(PLATFORMS)
    if unknown:
        raise TokenError(f"platforms 里有未知平台：{sorted(unknown)}")
    override = platforms.get(platform) or {}
    if not isinstance(override, dict):
        raise TokenError(f"platforms.{platform} 必须是对象")

    merged = {**base, **override}
    if merged.get("source") == "official":
        for key in ("value", "alpha", "reason", "approvedBy", "scope"):
            merged.pop(key, None)
    elif merged.get("source") == "custom":
        merged.pop("ref", None)
        merged.pop("emit", None)
    return merged


def kind_of(path: tuple[str, ...]) -> str:
    head = path[0]
    if head in ("color", "brand"):
        return "color"
    if head == "type":
        return "type"
    if head == "space":
        return "space"
    if head == "radius":
        return "radius"
    if head == "motion":
        return "motion"
    if head == "density":
        return "density"
    if head == "material":
        return "material"
    raise TokenError(f"未知 section：{head}")


def type_slot(path: tuple[str, ...]) -> str:
    if path[0] != "type":
        return "style"
    if path[-1] in ("family", "size", "weight", "lineHeight"):
        return path[-1]
    return "style"


def token_id(path: tuple[str, ...]) -> str:
    return ".".join(path)


def _is_number(value: object) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def validate(doc: dict) -> list[str]:
    """返回所有校验错误；空列表表示通过。"""
    errors: list[str] = []
    version = doc.get("version")
    if version != 2:
        errors.append(f"version 必须是 2，实际是 {version!r}")

    try:
        leaves = iter_leaves(doc)
    except TokenError as ex:
        return [str(ex)]

    seen: set[tuple[str, ...]] = set()
    for path, entry in leaves:
        where = token_id(path)
        if path in seen:
            errors.append(f"{where}: 重复的语义键")
        seen.add(path)
        kind = kind_of(path)
        slot = type_slot(path)

        for platform in PLATFORMS:
            try:
                resolved = resolve_platform(entry, platform)
            except TokenError as ex:
                errors.append(f"{where}[{platform}]: {ex}")
                continue
            source = resolved.get("source")
            tag = f"{where}[{platform}]"
            if source is None:
                errors.append(f"{tag}: 缺少 source")
                continue
            if source == "official":
                ref = resolved.get("ref")
                if not isinstance(ref, str) or not ref.strip():
                    errors.append(f"{tag}: official 缺少可解析的 ref")
                emit = resolved.get("emit", "code")
                if emit not in ("code", "comment"):
                    errors.append(f"{tag}: emit 只能是 code 或 comment，实际是 {emit!r}")
                elif isinstance(ref, str) and ref.strip():
                    shape_error = validate_ref_shape(platform, ref, emit)
                    if shape_error:
                        errors.append(f"{tag}: {shape_error}")
                if "value" in resolved or "alpha" in resolved:
                    errors.append(f"{tag}: official 不允许带 value/alpha")
            elif source == "custom":
                if "value" not in resolved:
                    errors.append(f"{tag}: custom 缺少 value")
                for field in ("reason", "approvedBy", "scope"):
                    value = resolved.get(field)
                    if not isinstance(value, str) or not value.strip():
                        errors.append(f"{tag}: custom 缺少 {field}")
                value = resolved.get("value")
                if kind == "color":
                    if not isinstance(value, str) or not COLOR_RE.match(value):
                        errors.append(f"{tag}: 颜色 value 必须是 #RRGGBB，实际是 {value!r}")
                    alpha = resolved.get("alpha", 1)
                    if not _is_number(alpha) or not (0 <= float(alpha) <= 1):
                        errors.append(f"{tag}: alpha 必须是 0..1 的数字")
                elif slot == "family":
                    if not isinstance(value, str) or not value.strip():
                        errors.append(f"{tag}: 字族 value 必须是非空字符串")
                elif slot == "weight":
                    if not _is_number(value) or int(value) not in range(100, 1000, 100):
                        errors.append(f"{tag}: 字重 value 必须是 100..900 且为 100 的倍数")
                elif kind in ("space", "radius", "density") or slot in ("size", "lineHeight"):
                    if not _is_number(value) or float(value) < 0:
                        errors.append(f"{tag}: {kind}/{slot} value 必须是非负数字")
                elif kind == "motion":
                    if not _is_number(value) or float(value) <= 0:
                        errors.append(f"{tag}: 动效 value 必须是正数（毫秒）")
                else:
                    if not isinstance(value, str) or not value.strip():
                        errors.append(f"{tag}: value 必须是非空字符串")
            else:
                errors.append(f"{tag}: source 非法：{source!r}")
    return errors


# ── 命名 ─────────────────────────────────────────────────────────────


def _words(segment: str) -> list[str]:
    parts = re.split(r"[-_]", segment)
    out: list[str] = []
    for part in parts:
        out.extend(re.findall(r"[A-Z]+(?=[A-Z][a-z])|[A-Z]?[a-z0-9]+|[A-Z]+|[0-9]+", part))
    return [w for w in out if w]


def pascal(path: tuple[str, ...]) -> str:
    return "".join(w[:1].upper() + w[1:] for w in _words("_".join(path)))


def camel(path: tuple[str, ...]) -> str:
    parts = _words("_".join(path))
    if not parts:
        return ""
    return parts[0].lower() + "".join(w[:1].upper() + w[1:] for w in parts[1:])


def upper_snake(path: tuple[str, ...]) -> str:
    return "_".join(w.upper() for w in _words("_".join(path)))


# ── 颜色 ─────────────────────────────────────────────────────────────


def argb_hex(entry: dict) -> str:
    raw = str(entry["value"]).lstrip("#").upper()
    if len(raw) == 8:
        return f"#{raw}"
    alpha = round(float(entry.get("alpha", 1)) * 255)
    return f"#{alpha:02X}{raw}"


def rgba(entry: dict) -> tuple[int, int, int, float]:
    raw = str(entry["value"]).lstrip("#")
    return (
        int(raw[0:2], 16),
        int(raw[2:4], 16),
        int(raw[4:6], 16),
        float(entry.get("alpha", 1)),
    )


def fmt_num(value: object, digits: int = 6) -> str:
    number = float(value)
    if number == int(number):
        return str(int(number))
    text = f"{number:.{digits}f}".rstrip("0").rstrip(".")
    return text or "0"


# ── 生成：C# ─────────────────────────────────────────────────────────

_WEIGHTS = {100: "Thin", 200: "ExtraLight", 300: "Light", 400: "Normal", 500: "Medium",
            600: "SemiBold", 700: "Bold", 800: "ExtraBold", 900: "Black"}
_SWIFT_WEIGHTS = {100: ".thin", 200: ".ultraLight", 300: ".light", 400: ".regular", 500: ".medium",
                  600: ".semibold", 700: ".bold", 800: ".heavy", 900: ".black"}


def _xml_doc(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")


def _csharp_str(text: str) -> str:
    return text.replace("\\", "\\\\").replace('"', '\\"')


def emit_csharp(doc: dict) -> str:
    entries = _platform_entries(doc, "windows")
    lines = [
        f"// <auto-generated> {GENERATED_HEADER}",
        "// 由 tools/gen-tokens/gen_tokens.py 生成；不要手改。",
        "// official 令牌输出平台资源键 / 样式键；custom 令牌输出登记过的字面值（颜色为 #AARRGGBB）。",
        "// ReSharper disable All",
        "#nullable enable",
        "",
        "namespace EliCloud.Generated;",
        "",
        "/// <summary>四端共享的设计令牌（C# / WinUI 3）。</summary>",
        "public static partial class DesignTokens",
        "{",
        f"    /// <summary>design/tokens.json 的 version。</summary>",
        f"    public const int TokensVersion = {doc['version']};",
    ]
    section = None
    for path, entry in entries:
        if path[0] != section:
            section = path[0]
            lines.extend(["", f"    // ── {section} ──"])
        kind = kind_of(path)
        slot = type_slot(path)
        name = pascal(path)
        source = entry["source"]
        if source == "official":
            ref = entry["ref"]
            if entry.get("emit") == "comment":
                lines.append(f"    // {token_id(path)}: official — {ref}（官方没有可寻址令牌，不要手绘）")
                continue
            suffix = "Key"
            lines.extend([
                f"    /// <summary>official: {_xml_doc(token_id(path))} → {_xml_doc(ref)}</summary>",
                f'    public const string {name}{suffix} = "{_csharp_str(ref)}";',
            ])
            continue

        reason = _xml_doc(str(entry["reason"]))
        if kind == "color":
            lines.extend([
                f"    /// <summary>custom: {_xml_doc(token_id(path))} — {reason}</summary>",
                f'    public const string {name}Hex = "{argb_hex(entry)}";',
            ])
        elif slot == "family":
            lines.extend([
                f"    /// <summary>custom: {_xml_doc(token_id(path))} — {reason}</summary>",
                f'    public const string {name} = "{_csharp_str(str(entry["value"]))}";',
            ])
        elif slot == "weight" or kind == "motion":
            suffix = "Ms" if kind == "motion" else ""
            lines.extend([
                f"    /// <summary>custom: {_xml_doc(token_id(path))} — {reason}</summary>",
                f"    public const int {name}{suffix} = {int(entry['value'])};",
            ])
        elif _is_number(entry.get("value")):
            lines.extend([
                f"    /// <summary>custom: {_xml_doc(token_id(path))} — {reason}</summary>",
                f"    public const double {name} = {fmt_num(entry['value'])};",
            ])
        else:
            lines.extend([
                f"    /// <summary>custom: {_xml_doc(token_id(path))} — {reason}</summary>",
                f'    public const string {name} = "{_csharp_str(str(entry["value"]))}";',
            ])
    lines.append("}")
    return "\n".join(lines) + "\n"


# ── 生成：Swift ──────────────────────────────────────────────────────


def emit_swift(doc: dict) -> str:
    entries = _platform_entries(doc, "macos")
    lines = [
        f"// <auto-generated> {GENERATED_HEADER}",
        "// 由 tools/gen-tokens/gen_tokens.py 生成；不要手改。",
        "// official 令牌输出 SwiftUI 官方样式表达式；custom 令牌输出登记过的字面值。",
        "",
        "import SwiftUI",
        "",
        "/// 四端共享的设计令牌（SwiftUI）。",
        "public enum DesignTokens {",
        f"    public static let tokensVersion: Int = {doc['version']}",
    ]
    section = None
    for path, entry in entries:
        if path[0] != section:
            section = path[0]
            lines.extend(["", f"    // MARK: - {section}"])
        kind = kind_of(path)
        slot = type_slot(path)
        name = camel(path)
        source = entry["source"]
        doc_id = token_id(path)

        if source == "official":
            ref = entry["ref"]
            if entry.get("emit") == "comment":
                lines.append(f"    // {doc_id}: official — {ref}（官方没有可寻址令牌）")
                continue
            if kind == "color" or kind == "material":
                lines.extend([
                    f"    /// official: {doc_id} → {ref}",
                    f"    public static let {name}: AnyShapeStyle = AnyShapeStyle({ref})",
                ])
            elif kind == "type" and slot != "family":
                lines.extend([
                    f"    /// official: {doc_id} → {ref}",
                    f"    public static let {name}: Font = {ref}",
                ])
            else:
                lines.extend([
                    f"    /// official: {doc_id} → {ref}",
                    f'    public static let {name}: String = "{_csharp_str(ref)}"',
                ])
            continue

        reason = str(entry["reason"])
        if kind == "color":
            r, g, b, a = rgba(entry)
            lines.extend([
                f"    /// custom: {doc_id} — {reason}",
                "    public static let " + f"{name}: AnyShapeStyle = AnyShapeStyle(Color("
                f"red: {fmt_num(r / 255)}, green: {fmt_num(g / 255)}, blue: {fmt_num(b / 255)}, "
                f"opacity: {fmt_num(a)}))",
            ])
        elif slot == "weight":
            weight = _SWIFT_WEIGHTS.get(int(entry["value"]))
            if weight is None:
                raise TokenError(f"{doc_id}: macOS 不支持的字体粗细 {entry['value']}")
            lines.extend([
                f"    /// custom: {doc_id} — {reason}",
                f"    public static let {name}: Font.Weight = {weight}",
            ])
        elif kind == "motion":
            lines.extend([
                f"    /// custom: {doc_id} — {reason}（源：毫秒）",
                f"    public static let {name}: TimeInterval = {fmt_num(float(entry['value']) / 1000)}",
            ])
        elif slot == "family":
            lines.extend([
                f"    /// custom: {doc_id} — {reason}",
                f'    public static let {name}: String = "{_csharp_str(str(entry["value"]))}"',
            ])
        else:
            lines.extend([
                f"    /// custom: {doc_id} — {reason}",
                f"    public static let {name}: CGFloat = {fmt_num(entry['value'])}",
            ])
    lines.append("}")
    return "\n".join(lines) + "\n"


# ── 生成：Kotlin ─────────────────────────────────────────────────────


def emit_kotlin(doc: dict) -> str:
    entries = _platform_entries(doc, "android")
    lines = [
        "// <auto-generated> from design/tokens.json",
        "// 由 tools/gen-tokens/gen_tokens.py 生成；不要手改。",
        "// official 令牌输出 MaterialTheme / 官方样式引用；custom 令牌输出登记过的字面值。",
        "",
        "@file:Suppress(\"unused\")",
        "",
        "package cloud.elipese.hello",
        "",
        "import androidx.compose.material3.MaterialTheme",
        "import androidx.compose.runtime.Composable",
        "import androidx.compose.ui.graphics.Color",
        "import androidx.compose.ui.graphics.Shape",
        "import androidx.compose.ui.text.TextStyle",
        "import androidx.compose.ui.text.font.FontFamily",
        "import androidx.compose.ui.text.font.FontWeight",
        "import androidx.compose.ui.unit.Dp",
        "import androidx.compose.ui.unit.TextUnit",
        "import androidx.compose.ui.unit.dp",
        "import androidx.compose.ui.unit.sp",
        "",
        "/// 四端共享的设计令牌（Jetpack Compose Material 3）。",
        "object Tokens {",
        f"    const val tokensVersion: Int = {doc['version']}",
    ]
    section = None
    for path, entry in entries:
        if path[0] != section:
            section = path[0]
            lines.extend(["", f"    // ── {section} ──"])
        kind = kind_of(path)
        slot = type_slot(path)
        name = camel(path)
        source = entry["source"]
        doc_id = token_id(path)

        if source == "official":
            ref = entry["ref"]
            if entry.get("emit") == "comment":
                lines.append(f"    // {doc_id}: official — {ref}（官方没有可寻址令牌）")
                continue
            if slot == "family":
                lines.extend([
                    f"    /** official: {doc_id} → {ref} */",
                    f"    val {name}: FontFamily = {ref}",
                ])
                continue
            property_type = {
                "color": "Color",
                "material": "Color",
                "type": "TextStyle",
                "radius": "Shape",
            }.get(kind)
            if property_type is None:
                lines.extend([
                    f"    /** official: {doc_id} → {ref} */",
                    f'    val {name}: String = "{_csharp_str(ref)}"',
                ])
            else:
                lines.extend([
                    f"    /** official: {doc_id} → {ref} */",
                    f"    val {name}: {property_type}",
                    f"        @Composable get() = {ref}",
                ])
            continue

        reason = str(entry["reason"])
        if kind == "color":
            lines.extend([
                f"    /** custom: {doc_id} — {reason} */",
                f"    val {name}: Color = Color(0x{argb_hex(entry)[1:]})",
            ])
        elif slot == "weight":
            weight = _WEIGHTS.get(int(entry["value"]))
            if weight is None:
                raise TokenError(f"{doc_id}: 不支持的字体粗细 {entry['value']}")
            lines.extend([
                f"    /** custom: {doc_id} — {reason} */",
                f"    val {name}: FontWeight = FontWeight.{weight}",
            ])
        elif kind == "motion":
            lines.extend([
                f"    /** custom: {doc_id} — {reason}（源：毫秒） */",
                f"    const val {name}Ms: Int = {int(entry['value'])}",
            ])
        elif kind == "radius":
            lines.extend([
                f"    /** custom: {doc_id} — {reason} */",
                f"    val {name}: Dp = {fmt_num(entry['value'])}.dp",
            ])
        elif kind == "space" or kind == "density":
            lines.extend([
                f"    /** custom: {doc_id} — {reason} */",
                f"    val {name}: Dp = {fmt_num(entry['value'])}.dp",
            ])
        elif slot in ("size", "lineHeight"):
            lines.extend([
                f"    /** custom: {doc_id} — {reason} */",
                f"    val {name}: TextUnit = {fmt_num(entry['value'])}.sp",
            ])
        elif slot == "family":
            lines.extend([
                f"    /** custom: {doc_id} — {reason} */",
                f'    const val {name}: String = "{_csharp_str(str(entry["value"]))}"',
            ])
        else:
            lines.extend([
                f"    /** custom: {doc_id} — {reason} */",
                f"    const val {name}: String = \"{_csharp_str(str(entry['value']))}\"",
            ])
    lines.append("}")
    return "\n".join(lines) + "\n"


# ── 生成：Python (GTK / libadwaita) ─────────────────────────────────


def emit_python(doc: dict) -> str:
    entries = _platform_entries(doc, "linux")
    lines = [
        f"# <auto-generated> {GENERATED_HEADER}",
        "# 由 tools/gen-tokens/gen_tokens.py 生成；不要手改。",
        "# official 令牌输出 GTK4/libadwaita 的 CSS 变量或 style class 名（不复制 hex）；",
        "# custom 令牌输出登记过的字面值（颜色为 #AARRGGBB）。",
        "",
        '"""四端共享的设计令牌（GTK4 / libadwaita）。"""',
        "",
        f"TOKENS_VERSION = {doc['version']}",
    ]
    section = None
    for path, entry in entries:
        if path[0] != section:
            section = path[0]
            lines.extend(["", f"# ── {section} ──"])
        kind = kind_of(path)
        slot = type_slot(path)
        name = upper_snake(path)
        source = entry["source"]
        doc_id = token_id(path)

        if source == "official":
            ref = entry["ref"]
            if entry.get("emit") == "comment":
                lines.append(f"# {doc_id}: official — {ref}（官方没有可寻址令牌）")
                continue
            lines.extend([
                f"# official: {doc_id} → {ref}",
                f'{name} = "{_csharp_str(ref)}"',
            ])
            continue

        reason = str(entry["reason"])
        if kind == "color":
            lines.extend([
                f"# custom: {doc_id} — {reason}",
                f'{name} = "{argb_hex(entry)}"',
            ])
        elif slot == "weight":
            lines.extend([
                f"# custom: {doc_id} — {reason}",
                f"{name} = {int(entry['value'])}",
            ])
        elif kind == "motion":
            lines.extend([
                f"# custom: {doc_id} — {reason}（毫秒）",
                f"{name}_MS = {int(entry['value'])}",
            ])
        elif kind in ("space", "radius", "density") or slot in ("size", "lineHeight"):
            lines.extend([
                f"# custom: {doc_id} — {reason}",
                f"{name} = {fmt_num(entry['value'])}",
            ])
        else:
            lines.extend([
                f"# custom: {doc_id} — {reason}",
                f'{name} = "{_csharp_str(str(entry["value"]))}"',
            ])
    return "\n".join(lines) + "\n"


def _require_valid(doc: dict) -> None:
    errors = validate(doc)
    if errors:
        raise TokenError("令牌校验失败：" + "；".join(errors[:5]))


def _platform_entries(doc: dict, platform: str) -> list[tuple[tuple[str, ...], dict]]:
    _require_valid(doc)
    return [(path, resolve_platform(entry, platform)) for path, entry in iter_leaves(doc)]


# ── 文档生成 ────────────────────────────────────────────────────────


def render_official_table(doc: dict) -> str:
    _require_valid(doc)
    header = "| 语义键 | Windows / WinUI 3 | macOS / SwiftUI | Android / Compose M3 | Linux / GTK + Adwaita |"
    sep = "|---|---|---|---|---|"
    rows = [header, sep]
    for path, entry in iter_leaves(doc):
        cells = []
        for platform in PLATFORMS:
            resolved = resolve_platform(entry, platform)
            if resolved["source"] == "official":
                ref = resolved["ref"]
                note = "（仅标注）" if resolved.get("emit") == "comment" else ""
                cells.append(f"`{ref}`{note}")
            else:
                cells.append(f"custom `{argb_hex(resolved) if kind_of(path) == 'color' else resolved['value']}`")
        rows.append("| `" + token_id(path) + "` | " + " | ".join(cells) + " |")
    return "\n".join(rows)


def render_exception_table(doc: dict) -> str:
    _require_valid(doc)
    rows = [
        "| 语义键 | 平台 | 值 | 理由 | 批准人 | 作用域 |",
        "|---|---|---|---|---|---|",
    ]
    for path, entry in iter_leaves(doc):
        for platform in PLATFORMS:
            resolved = resolve_platform(entry, platform)
            if resolved["source"] != "custom":
                continue
            value = argb_hex(resolved) if kind_of(path) == "color" else str(resolved["value"])
            rows.append(
                "| `" + token_id(path) + f"` | {platform} | `{value}` | "
                + str(resolved["reason"]).replace("|", "\\|")
                + f" | {resolved['approvedBy']} | `{resolved['scope']}` |"
            )
    return "\n".join(rows)


def render_coverage(doc: dict) -> str:
    _require_valid(doc)
    leaves = iter_leaves(doc)
    total = official = custom = comments = 0
    by_section: dict[str, list[int]] = {}
    by_platform: dict[str, list[int]] = {p: [0, 0, 0] for p in PLATFORMS}
    custom_rows: list[tuple[str, str, str, str]] = []

    for path, entry in leaves:
        section = path[0]
        bucket = by_section.setdefault(section, [0, 0, 0])
        for platform in PLATFORMS:
            resolved = resolve_platform(entry, platform)
            total += 1
            bucket[0] += 1
            by_platform[platform][0] += 1
            if resolved["source"] == "official":
                official += 1
                bucket[1] += 1
                by_platform[platform][1] += 1
                if resolved.get("emit") == "comment":
                    comments += 1
            else:
                custom += 1
                bucket[2] += 1
                by_platform[platform][2] += 1
                custom_rows.append(
                    (token_id(path), platform, str(resolved["scope"]), str(resolved["reason"]))
                )

    def pct(part: int, whole: int) -> str:
        return f"{part * 100 / whole:.1f}%" if whole else "0.0%"

    lines = [
        f"# 官方来源覆盖率（{GENERATED_HEADER}）",
        "",
        "> 由 `python tools/gen-tokens/gen_tokens.py` 生成，不要手改。",
        "",
        "## 汇总",
        "",
        f"- 语义键：{len(leaves)} 个；平台取值条目：{total} 个",
        f"- 官方来源：{official} 个（{pct(official, total)}），其中 {comments} 个是“官方只有机制/无可寻址令牌”的标注",
        f"- 登记例外（custom）：{custom} 个（{pct(custom, total)}）",
        "",
        "## 按类别",
        "",
        "| 类别 | 条目 | 官方 | custom | 官方占比 |",
        "|---|---|---|---|---|",
    ]
    for section in SECTIONS:
        if section not in by_section:
            continue
        whole, off, cus = by_section[section]
        lines.append(f"| {section} | {whole} | {off} | {cus} | {pct(off, whole)} |")

    lines.extend([
        "",
        "## 按平台",
        "",
        "| 平台 | 条目 | 官方 | custom | 官方占比 |",
        "|---|---|---|---|---|",
    ])
    for platform in PLATFORMS:
        whole, off, cus = by_platform[platform]
        lines.append(f"| {platform} | {whole} | {off} | {cus} | {pct(off, whole)} |")

    lines.extend([
        "",
        "## 登记例外（custom）",
        "",
        "| 语义键 | 平台 | 作用域 | 理由 |",
        "|---|---|---|---|",
    ])
    for token, platform, scope, reason in custom_rows:
        lines.append(f"| `{token}` | {platform} | `{scope}` | {reason.replace('|', '\\|')} |")
    lines.append("")
    return "\n".join(lines)


def replace_generated_block(text: str, name: str, body: str) -> str:
    begin = GENERATED_MARK.format(name=name)
    end = GENERATED_END.format(name=name)
    if begin not in text or end not in text:
        raise TokenError(f"文档缺少生成块标记：{name}")
    head, rest = text.split(begin, 1)
    _, tail = rest.split(end, 1)
    return f"{head}{begin}\n{body}\n{end}{tail}"
