#!/usr/bin/env python3
"""EliCloud 契约自检（离线，不依赖 npx / 网络）。

覆盖 Stage 1 验收标准里可以自动检查的部分：

1. `openapi.yaml` 结构：3.1.0、版本与 `VERSION` 一致、operationId 唯一、每个 operation 有
   summary/tags/responses/至少一个 example、所有 `$ref` 可解析。
2. 端点覆盖：27 个客户端端点一个不少（清单来自 `docs/implementation/01-contract-openapi.md` §2）。
3. 不泄露：没有 admin/内部端点；没有真实入口地址（只允许 `*.example.com`）。
4. `errors.json` ↔ `copy/zh-CN.json`：每个 copyKey 都存在，文案里不出现错误码/路径/RCON。
5. `fixtures/`：每个 fixture 的 operationId/method/path/status 都能在契约里找到，
   request/response body 按 JSON Schema 子集校验，`expected` 里的错误码/Retry-After/文案 key 对得上。
6. 每个 operation 至少有一个 fixture。
7. `sdk-mapping.md` 第一节覆盖的 operationId 集合与契约完全一致。

退出码：0 = 全部通过；1 = 有失败。

用法::

    python contracts/tools/lint_contract.py
    python contracts/tools/lint_contract.py --verbose
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from urllib.parse import urlsplit

try:
    import yaml
except ImportError:  # pragma: no cover
    print("需要 PyYAML：python -m pip install pyyaml", file=sys.stderr)
    raise SystemExit(2)

ROOT = Path(__file__).resolve().parents[1]
OPENAPI = ROOT / "openapi.yaml"
ERRORS = ROOT / "errors.json"
COPY = ROOT / "copy" / "zh-CN.json"
FIXTURES = ROOT / "fixtures"
VERSION_FILE = ROOT / "VERSION"
MAPPING = ROOT / "sdk-mapping.md"

# Stage 1 §2 的端点目录：方法 + 路径 + operationId。少一个都算失败。
EXPECTED_OPERATIONS = {
    ("POST", "/auth/register", "registerStart"),
    ("POST", "/auth/register/status", "getRegistrationStatus"),
    ("POST", "/auth/register/resend", "resendRegistrationEmail"),
    ("POST", "/auth/register/cancel", "cancelRegistration"),
    ("POST", "/auth/login", "login"),
    ("POST", "/auth/refresh", "refreshToken"),
    ("GET", "/auth/userinfo", "getUserInfo"),
    ("POST", "/auth/userinfo", "postUserInfo"),
    ("GET", "/auth/.well-known/openid-configuration", "getOidcDiscovery"),
    ("GET", "/auth/.well-known/jwks.json", "getJwks"),
    ("GET", "/auth/authorize", "authorize"),
    ("POST", "/auth/token", "token"),
    ("POST", "/auth/device_authorization", "deviceAuthorization"),
    ("GET", "/auth/logout", "logout"),
    ("GET", "/userinfo/v1/profile", "getMyProfile"),
    ("PUT", "/userinfo/v1/profile", "updateMyProfile"),
    ("GET", "/userinfo/v1/profiles/{sub}", "getProfile"),
    ("GET", "/userinfo/v1/profile/{kind}", "getMyProfileImage"),
    ("PUT", "/userinfo/v1/profile/{kind}", "uploadMyProfileImage"),
    ("DELETE", "/userinfo/v1/profile/{kind}", "deleteMyProfileImage"),
    ("GET", "/userinfo/v1/profiles/{sub}/{kind}", "getProfileImage"),
    ("GET", "/mc/healthz", "mcHealth"),
    ("GET", "/mc/v1/names", "listMyMcNames"),
    ("POST", "/mc/v1/names", "addMcName"),
    ("DELETE", "/mc/v1/names/{id}", "removeMcName"),
    ("GET", "/mc/v1/me", "mcIdentity"),
    ("GET", "/core/v1/services", "listCoreServices"),
}

# 允许只有跳转/HTML 语义、没有 4xx/5xx JSON 的端点。
NO_ERROR_RESPONSE_ALLOWED = {"logout", "getJwks"}

OPERATION_VERBS = ("get", "put", "post", "delete", "patch", "head", "options", "trace")
DATE_TIME_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$")
BANNED_COPY_TOKENS = ("RCON", "rcon", "/v1/", "/auth/", "http://", "https://", "scope", "token")


class Report:
    def __init__(self) -> None:
        self.failures: list[str] = []
        self.checks = 0
        self.notes: list[str] = []

    def check(self, condition: bool, message: str) -> bool:
        self.checks += 1
        if not condition:
            self.failures.append(message)
        return condition

    def fail(self, message: str) -> None:
        self.failures.append(message)

    def note(self, message: str) -> None:
        self.notes.append(message)


# ------------------------------------------------------------------ JSON Schema
def resolve_ref(doc: dict, ref: str):
    if not ref.startswith("#/"):
        raise ValueError(f"只支持本地 $ref：{ref}")
    node = doc
    for part in ref[2:].split("/"):
        part = part.replace("~1", "/").replace("~0", "~")
        if not isinstance(node, dict) or part not in node:
            raise KeyError(ref)
        node = node[part]
    return node


def type_names(value) -> str:
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, int):
        return "integer"
    if isinstance(value, float):
        return "number"
    if isinstance(value, str):
        return "string"
    if isinstance(value, list):
        return "array"
    if isinstance(value, dict):
        return "object"
    return type(value).__name__


def schema_types(schema: dict) -> list[str]:
    t = schema.get("type")
    if t is None:
        return []
    return t if isinstance(t, list) else [t]


def validate(value, schema, doc: dict, where: str, errors: list[str]) -> None:
    if schema is True or schema is None:
        return
    if not isinstance(schema, dict):
        return

    if "$ref" in schema:
        try:
            target = resolve_ref(doc, schema["$ref"])
        except (KeyError, ValueError) as exc:
            errors.append(f"{where}: 无法解析 $ref {schema['$ref']} ({exc})")
            return
        validate(value, target, doc, where, errors)
        return

    if "oneOf" in schema:
        branches = []
        for index, branch in enumerate(schema["oneOf"]):
            branch_errors: list[str] = []
            validate(value, branch, doc, where, branch_errors)
            if not branch_errors:
                return
            branches.append(f"oneOf[{index}]: {branch_errors[0]}")
        errors.append(f"{where}: 不匹配任何 oneOf 分支 → {'; '.join(branches)}")
        return

    allowed = schema_types(schema)
    if allowed:
        actual = type_names(value)
        ok = actual in allowed or (actual == "integer" and "number" in allowed)
        if not ok:
            errors.append(f"{where}: 期望 {allowed}，实际 {actual} ({value!r})")
            return

    if "const" in schema and value != schema["const"]:
        errors.append(f"{where}: 期望常量 {schema['const']!r}，实际 {value!r}")

    if "enum" in schema and value not in schema["enum"]:
        errors.append(f"{where}: {value!r} 不在枚举 {schema['enum']} 里")

    if isinstance(value, str):
        if "minLength" in schema and len(value) < schema["minLength"]:
            errors.append(f"{where}: 长度 {len(value)} < minLength {schema['minLength']}")
        if "maxLength" in schema and len(value) > schema["maxLength"]:
            errors.append(f"{where}: 长度 {len(value)} > maxLength {schema['maxLength']}")
        if "pattern" in schema and not re.search(schema["pattern"], value):
            errors.append(f"{where}: 不匹配 pattern {schema['pattern']} ({value!r})")
        fmt = schema.get("format")
        if fmt == "date-time" and not DATE_TIME_RE.match(value):
            errors.append(f"{where}: 不是 ISO8601 date-time：{value!r}")
        if fmt == "email" and "@" not in value:
            errors.append(f"{where}: 不是邮箱：{value!r}")
        if fmt == "uri" and not re.match(r"^https?://", value):
            errors.append(f"{where}: 不是 http(s) URL：{value!r}")

    if isinstance(value, int) and not isinstance(value, bool):
        if "minimum" in schema and value < schema["minimum"]:
            errors.append(f"{where}: {value} < minimum {schema['minimum']}")
        if "maximum" in schema and value > schema["maximum"]:
            errors.append(f"{where}: {value} > maximum {schema['maximum']}")

    if isinstance(value, dict):
        for field in schema.get("required", []):
            if field not in value:
                errors.append(f"{where}: 缺少必填字段 {field}")
        properties = schema.get("properties") or {}
        for key, item in value.items():
            if key in properties:
                validate(item, properties[key], doc, f"{where}.{key}", errors)
            elif schema.get("additionalProperties") is False:
                errors.append(f"{where}: 不允许的字段 {key}")
            elif isinstance(schema.get("additionalProperties"), dict):
                validate(item, schema["additionalProperties"], doc, f"{where}.{key}", errors)

    if isinstance(value, list) and isinstance(schema.get("items"), dict):
        for index, item in enumerate(value):
            validate(item, schema["items"], doc, f"{where}[{index}]", errors)


# ------------------------------------------------------------------ 工具
def normalize_path(path: str) -> list[str]:
    return path.strip("/").split("/") if path.strip("/") else []


def path_matches(template: str, concrete: str) -> bool:
    left, right = normalize_path(template), normalize_path(concrete)
    if len(left) != len(right):
        return False
    for expected, actual in zip(left, right):
        if expected.startswith("{") and expected.endswith("}"):
            continue
        if expected != actual:
            return False
    return True


def has_example(node) -> bool:
    if isinstance(node, dict):
        if "example" in node or "examples" in node:
            return True
        return any(has_example(v) for v in node.values())
    if isinstance(node, list):
        return any(has_example(v) for v in node)
    return False


def collect_refs(node, found: list[str]) -> None:
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "$ref" and isinstance(value, str):
                found.append(value)
            else:
                collect_refs(value, found)
    elif isinstance(node, list):
        for item in node:
            collect_refs(item, found)


# ------------------------------------------------------------------ 各项检查
def check_openapi(report: Report, doc: dict) -> dict[str, tuple[str, str]]:
    version = VERSION_FILE.read_text(encoding="utf-8").strip()
    report.check(doc.get("openapi") == "3.1.0", f"openapi 必须是 3.1.0，实际 {doc.get('openapi')}")
    report.check(
        doc.get("info", {}).get("version") == version,
        f"info.version ({doc.get('info', {}).get('version')}) 必须等于 VERSION ({version})",
    )
    report.check(bool(doc.get("info", {}).get("title")), "info.title 缺失")
    report.check(len(doc.get("servers") or []) > 0, "servers 缺失")
    for server in doc.get("servers") or []:
        host = urlsplit(server.get("url", "")).hostname or ""
        report.check(
            host.endswith("example.com"),
            f"servers.url 只能用示意地址（*.example.com），实际 {server.get('url')}",
        )

    paths = doc.get("paths") or {}
    report.check(bool(paths), "paths 为空")

    operations: dict[str, tuple[str, str]] = {}
    for path, item in paths.items():
        report.check(path.startswith("/"), f"path 必须以 / 开头：{path}")
        for verb in OPERATION_VERBS:
            if verb not in item:
                continue
            op = item[verb]
            where = f"{verb.upper()} {path}"
            operation_id = op.get("operationId")
            report.check(bool(operation_id), f"{where} 缺少 operationId")
            if not operation_id:
                continue
            report.check(operation_id not in operations, f"operationId 重复：{operation_id}")
            operations[operation_id] = (verb.upper(), path)
            report.check(bool(op.get("summary")), f"{where} 缺少 summary")
            report.check(bool(op.get("tags")), f"{where} 缺少 tags")
            responses = op.get("responses") or {}
            report.check(bool(responses), f"{where} 缺少 responses")
            report.check(has_example(op), f"{where} 没有任何 example/examples")

            # 至少一个 4xx/5xx（或显式允许）。
            statuses = [str(code) for code in responses]
            has_error = any(code[0] in "45" for code in statuses if code.isdigit())
            if operation_id not in NO_ERROR_RESPONSE_ALLOWED:
                report.check(has_error, f"{where} 没有声明 4xx/5xx 错误响应")

            # 路径模板里的参数必须声明在 parameters（path or operation 级）。
            params: set[str] = set()
            for param in (item.get("parameters") or []) + (op.get("parameters") or []):
                if not isinstance(param, dict):
                    continue
                if "$ref" in param:
                    try:
                        param = resolve_ref(doc, param["$ref"])
                    except (KeyError, ValueError):
                        continue
                if isinstance(param, dict) and param.get("name"):
                    params.add(param["name"])
            for segment in normalize_path(path):
                if segment.startswith("{") and segment.endswith("}"):
                    name = segment[1:-1]
                    report.check(name in params, f"{where} 缺少 path 参数 {name} 的声明")

            # 请求体必须声明 content。
            request_body = op.get("requestBody")
            if request_body:
                report.check(
                    bool(request_body.get("content")),
                    f"{where} requestBody 缺少 content",
                )

            # 受保护端点必须声明 security。
            if operation_id in (
                "getMyProfile",
                "updateMyProfile",
                "getProfile",
                "getMyProfileImage",
                "uploadMyProfileImage",
                "deleteMyProfileImage",
                "getProfileImage",
                "listMyMcNames",
                "addMcName",
                "removeMcName",
                "mcIdentity",
                "getUserInfo",
                "postUserInfo",
            ):
                report.check(bool(op.get("security")), f"{where} 应声明 bearerAuth security")

    # $ref 全部可解析。
    refs: list[str] = []
    collect_refs(doc, refs)
    for ref in sorted(set(refs)):
        try:
            resolve_ref(doc, ref)
        except (KeyError, ValueError) as exc:
            report.fail(f"$ref 无法解析：{ref} ({exc})")

    # schema 命名与字段名必须 snake_case（契约里不许顺手改 camelCase）。
    schemas = (doc.get("components") or {}).get("schemas") or {}
    report.check(bool(schemas), "components.schemas 为空")
    for name, schema in schemas.items():
        if not isinstance(schema, dict):
            continue
        for field in (schema.get("properties") or {}).keys():
            report.check(
                re.fullmatch(r"[a-z0-9]+(_[a-z0-9]+)*", field) is not None,
                f"schema {name} 的字段 {field} 不是 snake_case",
            )

    # 不允许 admin / 内部端点混进客户端契约。
    for path in paths:
        lowered = path.lower()
        report.check(
            "/admin" not in lowered
            and "/v1/clients" not in lowered
            and "/internal" not in lowered
            and "rotate-secret" not in lowered,
            f"客户端契约里不允许出现管理/内部端点：{path}",
        )

    return operations


def check_coverage(report: Report, operations: dict[str, tuple[str, str]]) -> None:
    actual = {(method, path, op) for op, (method, path) in operations.items()}
    for method, path, op in sorted(EXPECTED_OPERATIONS):
        report.check(
            (method, path, op) in actual,
            f"缺少 §2 要求的端点：{method} {path} → {op}",
        )
    for method, path, op in sorted(actual):
        report.check(
            (method, path, op) in EXPECTED_OPERATIONS,
            f"契约里出现了 §2 之外的端点：{method} {path} → {op}（先改文档再改契约）",
        )
    report.note(f"端点覆盖：{len(actual)} / {len(EXPECTED_OPERATIONS)}")


def check_errors(report: Report, doc: dict) -> dict:
    errors_doc = json.loads(ERRORS.read_text(encoding="utf-8"))
    copy_doc = json.loads(COPY.read_text(encoding="utf-8"))
    strings = copy_doc.get("strings") or {}
    codes = errors_doc.get("codes") or {}

    report.check(errors_doc.get("version") == 1, "errors.json version 必须是 1")
    report.check(bool(codes), "errors.json 没有任何错误码")

    for code, meta in codes.items():
        where = f"errors.json[{code}]"
        for field in ("http", "category", "copyKey", "retryable"):
            report.check(field in meta, f"{where} 缺少 {field}")
        report.check(
            meta.get("category") in (errors_doc.get("categories") or {}),
            f"{where} 的 category 未在 categories 里定义",
        )
        copy_key = meta.get("copyKey")
        if copy_key:
            report.check(copy_key in strings, f"{where} 的 copyKey 在 copy/zh-CN.json 里不存在：{copy_key}")
            template = strings.get(copy_key, "")
            for placeholder in meta.get("placeholders") or []:
                report.check(
                    "{" + placeholder + "}" in template,
                    f"{where} 声明了占位符 {placeholder}，但文案里没有 {{{placeholder}}}",
                )

    for key, text in strings.items():
        if key.startswith("error."):
            for token in ("invalid_", "rate_limited", "too_many_requests", "not_found"):
                report.check(
                    token not in text,
                    f"文案里不允许出现错误码（{token}）：{key} = {text}",
                )
            report.check(len(text) <= 60, f"错误文案过长（{len(text)} 字符，>60）：{key}")
        else:
            # Stage 2 起 copy/zh-CN.json 同时承载界面文案：按页面分组，长度上限放宽。
            report.check(
                re.fullmatch(r"[a-z][A-Za-z0-9]*(\.[A-Za-z0-9]+)+", key) is not None,
                f"界面文案 key 必须是小写开头的点分语义键：{key}",
            )
            report.check(len(text) <= 120, f"界面文案过长（{len(text)} 字符，>120）：{key}")
        for token in BANNED_COPY_TOKENS:
            report.check(token not in text, f"文案里不允许出现基础设施术语（{token}）：{key} = {text}")

    error_strings = {key: text for key, text in strings.items() if key.startswith("error.")}
    report.note(f"错误码：{len(codes)} 个，错误文案：{len(error_strings)} 条，界面文案：{len(strings) - len(error_strings)} 条")

    # 契约里出现的每个 error 示例码都必须在 errors.json 里。
    known = set(codes)
    for code in find_error_examples(doc):
        report.check(code in known, f"openapi 示例里出现了未登记的错误码：{code}")

    return errors_doc


def find_error_examples(node, found: set[str] | None = None) -> set[str]:
    """只把「错误信封」里的 error 当错误码：必须有同级的 error_description。"""
    found = set() if found is None else found
    if isinstance(node, dict):
        if isinstance(node.get("error"), str) and "error_description" in node:
            found.add(node["error"])
        for value in node.values():
            find_error_examples(value, found)
    elif isinstance(node, list):
        for item in node:
            find_error_examples(item, found)
    return found


def response_for(doc: dict, operation: dict, status: int):
    responses = operation.get("responses") or {}
    node = responses.get(str(status))
    if node is None:
        return None
    if "$ref" in node:
        node = resolve_ref(doc, node["$ref"])
    return node


def media_schema(doc: dict, node: dict, media_type: str):
    content = node.get("content") or {}
    chosen = None
    for key in content:
        if key.split(";")[0].strip().lower() == media_type:
            chosen = content[key]
            break
    if chosen is None:
        return None
    schema = chosen.get("schema")
    return schema


def check_fixtures(report: Report, doc: dict, operations: dict[str, tuple[str, str]], errors_doc: dict) -> None:
    if not FIXTURES.is_dir():
        report.fail(f"fixtures 目录不存在：{FIXTURES}")
        return

    covered: set[str] = set()
    codes = errors_doc.get("codes") or {}
    files = sorted(FIXTURES.rglob("*.json"))
    report.check(bool(files), "fixtures 为空")

    for path in files:
        rel = path.relative_to(FIXTURES).as_posix()
        try:
            fixture = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            report.fail(f"{rel}: 不是合法 JSON ({exc})")
            continue

        where = f"fixtures/{rel}"
        report.check(fixture.get("contractVersion") == VERSION_FILE.read_text(encoding="utf-8").strip(),
                     f"{where}: contractVersion 与 VERSION 不一致")
        request = fixture.get("request") or {}
        response = fixture.get("response") or {}
        method = str(request.get("method", "")).upper()
        concrete_path = request.get("path")
        operation_id = fixture.get("operationId")

        if operation_id not in operations:
            report.fail(f"{where}: operationId 不在契约里：{operation_id}")
            continue
        covered.add(operation_id)

        expected_method, template_path = operations[operation_id]
        report.check(method == expected_method, f"{where}: 方法 {method} ≠ 契约 {expected_method}")
        report.check(
            isinstance(concrete_path, str) and path_matches(template_path, concrete_path),
            f"{where}: 路径 {concrete_path} 不匹配模板 {template_path}",
        )

        # 找到契约里的 operation 节点。
        op_node = doc["paths"][template_path][expected_method.lower()]

        # ---------------- 请求体
        request_body = op_node.get("requestBody")
        if request_body:
            if "json" in request:
                schema = media_schema(doc, request_body, "application/json")
                if schema is not None:
                    body_errors: list[str] = []
                    validate(request["json"], schema, doc, f"{where}.request.json", body_errors)
                    for message in body_errors:
                        report.fail(message)
            elif "form" in request:
                schema = media_schema(doc, request_body, "application/x-www-form-urlencoded")
                if schema is not None:
                    body_errors = []
                    validate(request["form"], schema, doc, f"{where}.request.form", body_errors)
                    for message in body_errors:
                        report.fail(message)
            elif "multipart" in request:
                report.check(
                    media_schema(doc, request_body, "multipart/form-data") is not None,
                    f"{where}: 契约未声明 multipart/form-data",
                )
            else:
                report.fail(f"{where}: 契约要求 requestBody，但 fixture 没给 json/form/multipart")
        elif any(key in request for key in ("json", "form")):
            report.fail(f"{where}: fixture 带了请求体，但契约没有 requestBody")

        # ---------------- 响应
        status = response.get("status")
        report.check(isinstance(status, int), f"{where}: response.status 必须是整数")
        node = response_for(doc, op_node, int(status)) if isinstance(status, int) else None
        if node is None:
            report.fail(f"{where}: 契约没有声明状态码 {status} 的响应")
            continue

        expected = fixture.get("expected") or {}
        if "json" in response:
            schema = media_schema(doc, node, "application/json")
            if schema is None:
                report.fail(f"{where}: 契约没有为该状态码声明 application/json 响应")
            else:
                body_errors = []
                validate(response["json"], schema, doc, f"{where}.response.json", body_errors)
                for message in body_errors:
                    report.fail(message)

            error_code = (response.get("json") or {}).get("error") if isinstance(response.get("json"), dict) else None
            if "errorCode" in expected:
                report.check(
                    error_code == expected["errorCode"],
                    f"{where}: expected.errorCode={expected['errorCode']!r}，实际 body.error={error_code!r}",
                )
            if error_code is not None:
                report.check(error_code in codes, f"{where}: 错误码 {error_code} 未登记在 errors.json")
                if error_code in codes:
                    expected_key = codes[error_code]["copyKey"]
                    if expected.get("copyKey"):
                        report.check(
                            expected.get("copyKey") == expected_key,
                            f"{where}: expected.copyKey 与 errors.json 不一致（{expected.get('copyKey')} ≠ {expected_key}）",
                        )
        elif "raw" in response or "rawFile" in response:
            if expected.get("malformed"):
                raw = response.get("raw") or (path.parent / str(response.get("rawFile"))).read_text(encoding="utf-8")
                try:
                    json.loads(raw)
                except json.JSONDecodeError:
                    pass
                else:
                    report.fail(f"{where}: 标记了 malformed，但 body 其实是合法 JSON")
            if expected.get("nonJson"):
                content_type = str((response.get("headers") or {}).get("Content-Type", ""))
                report.check(
                    "json" not in content_type.lower(),
                    f"{where}: 标记了 nonJson，但 Content-Type 是 {content_type}",
                )
                target = response.get("rawFile")
                if target:
                    payload = (path.parent / str(target))
                    report.check(payload.exists(), f"{where}: rawFile 不存在：{target}")
            if expected.get("copyKey"):
                known_keys = {meta["copyKey"] for meta in codes.values()}
                report.check(
                    expected["copyKey"] in known_keys,
                    f"{where}: expected.copyKey {expected['copyKey']} 不在 errors.json",
                )
        elif "file" in response:
            content_type = str((response.get("headers") or {}).get("Content-Type", "")).lower()
            report.check(content_type.startswith("image/"), f"{where}: 二进制 fixture 的 Content-Type 不是 image/*")
            payload = path.parent / str(response["file"])
            report.check(payload.exists(), f"{where}: 引用的图片不存在：{response['file']}")
            if expected.get("binary"):
                report.check(
                    expected["binary"] == response["file"],
                    f"{where}: expected.binary 与 response.file 不一致",
                )
        else:
            # 204/304 之类无 body 的响应。
            content = node.get("content") or {}
            report.check(
                not content,
                f"{where}: 契约声明了响应体，但 fixture 没有 body（若确实为空，请加 expected.emptyBody）",
            )

        # ---------------- Retry-After
        headers = {str(k).lower(): str(v) for k, v in (response.get("headers") or {}).items()}
        if "retryAfterSeconds" in expected:
            wanted = expected["retryAfterSeconds"]
            report.check("retry-after" in headers, f"{where}: expected 声明了 Retry-After，但响应头里没有")
            if "retry-after" in headers:
                report.check(
                    headers["retry-after"].strip() == str(wanted),
                    f"{where}: Retry-After 头为 {headers['retry-after']!r}，expected 为 {wanted}",
                )
            report.check(
                errors_doc["retryAfter"]["header"].lower() in ("retry-after",),
                "errors.json retryAfter.header 必须写成 Retry-After",
            )

    for operation_id in sorted(set(operations) - covered):
        report.fail(f"fixtures 没有覆盖 operationId={operation_id}")

    report.note(f"fixtures：{len(files)} 个，覆盖 {len(covered)} / {len(operations)} 个 operation")


def check_examples(report: Report, doc: dict) -> None:
    """openapi.yaml 里内联的 example/examples 必须通过自己的 schema。"""
    checked = 0

    def check_media(media: dict, where: str) -> None:
        nonlocal checked
        if not isinstance(media, dict):
            return
        schema = media.get("schema")
        if not isinstance(schema, dict):
            return
        resolved = resolve_ref(doc, schema["$ref"]) if "$ref" in schema else schema
        if resolved.get("format") == "binary":
            return
        samples = []
        if "example" in media:
            samples.append(("example", media["example"]))
        for name, entry in (media.get("examples") or {}).items():
            if isinstance(entry, dict) and "value" in entry:
                samples.append((f"examples.{name}", entry["value"]))
        for label, value in samples:
            errors: list[str] = []
            validate(value, schema, doc, f"{where}.{label}", errors)
            checked += 1
            report.checks += 1
            for message in errors:
                report.fail(message)

    for path, item in (doc.get("paths") or {}).items():
        for verb in OPERATION_VERBS:
            if verb not in item:
                continue
            op = item[verb]
            where = f"{verb.upper()} {path}"
            request_body = op.get("requestBody")
            if isinstance(request_body, dict):
                for media_type, media in (request_body.get("content") or {}).items():
                    check_media(media, f"{where} request[{media_type}]")
            for status, node in (op.get("responses") or {}).items():
                if "$ref" in node:
                    node = resolve_ref(doc, node["$ref"])
                for media_type, media in (node.get("content") or {}).items():
                    check_media(media, f"{where} {status}[{media_type}]")

    for name, node in ((doc.get("components") or {}).get("responses") or {}).items():
        for media_type, media in (node.get("content") or {}).items():
            check_media(media, f"components.responses.{name}[{media_type}]")

    report.note(f"examples：{checked} 个通过 schema 校验")


def check_mapping(report: Report, operations: dict[str, tuple[str, str]]) -> None:
    text = MAPPING.read_text(encoding="utf-8")
    section_one = text.split("## 2.")[0]
    mapped = set()
    for line in section_one.splitlines():
        stripped = line.strip()
        if not stripped.startswith("|"):
            continue
        cells = [cell.strip() for cell in stripped.strip("|").split("|")]
        if not cells:
            continue
        match = re.fullmatch(r"`([A-Za-z][A-Za-z0-9]*)`", cells[0])
        if match:
            mapped.add(match.group(1))
    report.check(bool(mapped), "sdk-mapping.md 第一节没有解析到 operationId 表格")
    for operation_id in sorted(set(operations) - mapped):
        report.fail(f"sdk-mapping.md 缺少 operationId={operation_id}")
    for operation_id in sorted(mapped - set(operations)):
        report.fail(f"sdk-mapping.md 里的 {operation_id} 不在契约里")
    report.note(f"sdk 映射：{len(mapped)} 个 operationId")


def check_no_hardcoded_hosts(report: Report) -> None:
    """fixtures / openapi 里只允许 *.example.com 作为示意主机。"""
    host_re = re.compile(r"https?://([A-Za-z0-9._-]+)")
    for path in sorted(FIXTURES.rglob("*")):
        if not path.is_file():
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        for host in host_re.findall(text):
            report.check(
                host.endswith("example.com") or host.endswith("example.org"),
                f"{path.relative_to(ROOT)} 里出现了非示意主机：{host}",
            )
    text = OPENAPI.read_text(encoding="utf-8")
    for host in host_re.findall(text):
        report.check(
            host.endswith("example.com") or host == "json-schema.org",
            f"openapi.yaml 里出现了非示意主机：{host}",
        )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="EliCloud contract linter")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args(argv)

    report = Report()
    try:
        doc = yaml.safe_load(OPENAPI.read_text(encoding="utf-8"))
    except yaml.YAMLError as exc:
        print(f"openapi.yaml 解析失败：{exc}", file=sys.stderr)
        return 1

    if not isinstance(doc, dict):
        print("openapi.yaml 顶层必须是对象", file=sys.stderr)
        return 1

    operations = check_openapi(report, doc)
    check_coverage(report, operations)
    errors_doc = check_errors(report, doc)
    check_examples(report, doc)
    check_fixtures(report, doc, operations, errors_doc)
    check_mapping(report, operations)
    check_no_hardcoded_hosts(report)

    for note in report.notes:
        print(f"  · {note}")

    if report.failures:
        print(f"\n失败 {len(report.failures)} 项（共检查 {report.checks} 项）：", file=sys.stderr)
        for message in report.failures:
            print(f"  ✗ {message}", file=sys.stderr)
        return 1

    print(f"\n通过：{report.checks} 项检查，contracts {VERSION_FILE.read_text(encoding='utf-8').strip()}")
    if args.verbose:
        for operation_id, (method, path) in sorted(operations.items()):
            print(f"  {method:6s} {path:45s} {operation_id}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
