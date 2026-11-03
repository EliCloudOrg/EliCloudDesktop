#!/usr/bin/env python3
"""EliCloud 契约 mock server：按 contracts/fixtures/ 回放，零第三方依赖。

用法::

    python contracts/mock_server.py                       # 127.0.0.1:4010
    python contracts/mock_server.py --port 4010 --host 127.0.0.1
    python contracts/mock_server.py --strict-auth         # 受保护端点必须带 Bearer
    python contracts/mock_server.py --delay-ms 3000       # 故意变慢，测客户端超时

选择 fixture（优先于路由匹配）::

    curl -s 'http://127.0.0.1:4010/auth/login?__fixture=errors/retry-after'
    curl -s -H 'X-Fixture: auth/login-invalid-grant' http://127.0.0.1:4010/auth/login -X POST ...

辅助端点::

    GET /__healthz      服务自身状态 + fixture 数
    GET /__fixtures     列出全部 fixture（name/method/path/status）
    GET /__openapi      原样返回 contracts/openapi.yaml

行为约定：

* 路由匹配忽略 query；方法 + 路径模板（`{param}` 匹配单段）相同即可。
* 多个 fixture 命中同一路由且未指定 `__fixture` 时，按文件名排序取第一个，
  并在响应头 `X-Mock-Fixture` 标出实际使用的那一个（同时 `X-Mock-Ambiguous: 1`）。
* `?__delay_ms=<n>` 或 `--delay-ms` 会在响应前阻塞，用来验证客户端超时/取消。
* 响应头、状态码、body 全部来自 fixture；`Retry-After`、非 JSON 错误体、
  503 健康快照都原样回放。
"""

from __future__ import annotations

import argparse
import json
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlsplit

ROOT = Path(__file__).resolve().parent
FIXTURES = ROOT / "fixtures"
OPENAPI = ROOT / "openapi.yaml"


def load_fixtures() -> list[dict]:
    fixtures = []
    for path in sorted(FIXTURES.rglob("*.json")):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            raise SystemExit(f"fixture 不是合法 JSON：{path} → {exc}") from exc
        rel = path.relative_to(FIXTURES).as_posix()
        data["_rel"] = rel
        data["_dir"] = path.parent
        data.setdefault("name", rel)
        fixtures.append(data)
    return fixtures


def route_matches(fixture: dict, method: str, path: str) -> bool:
    request = fixture.get("request") or {}
    if str(request.get("method", "")).upper() != method:
        return False
    template = str(request.get("path", ""))
    left = template.strip("/").split("/") if template.strip("/") else []
    right = path.strip("/").split("/") if path.strip("/") else []
    if len(left) != len(right):
        return False
    for expected, actual in zip(left, right):
        if expected.startswith("{") and expected.endswith("}"):
            if not actual:
                return False
            continue
        if expected != actual:
            return False
    return True


def pick_fixture(fixtures: list[dict], method: str, path: str, wanted: str | None):
    if wanted:
        key = wanted.strip()
        key = key[:-5] if key.endswith(".json") else key
        for fixture in fixtures:
            rel = fixture["_rel"]
            if rel == wanted or rel == key + ".json" or fixture["name"] in (wanted, key):
                return fixture, False
        return None, False

    matches = [f for f in fixtures if route_matches(f, method, path)]
    if not matches:
        return None, False
    return matches[0], len(matches) > 1


class Handler(BaseHTTPRequestHandler):
    server_version = "EliCloudMock/1.0"

    fixtures: list[dict] = []
    strict_auth = False
    delay_ms = 0

    # ---------------------------------------------------------------- helpers
    def _send_json(self, status: int, payload) -> None:
        body = json.dumps(payload, ensure_ascii=False, indent=2).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("X-Mock-Server", "EliCloudMock/1.0")
        self.end_headers()
        self.wfile.write(body)

    def _send_bytes(self, status: int, body: bytes, headers: dict[str, str]) -> None:
        self.send_response(status)
        for key, value in headers.items():
            self.send_header(key, value)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("X-Mock-Server", "EliCloudMock/1.0")
        self.end_headers()
        if body:
            self.wfile.write(body)

    def log_message(self, fmt: str, *args) -> None:  # noqa: A003
        sys.stderr.write("mock %s - %s\n" % (self.address_string(), fmt % args))

    # ------------------------------------------------------------------ verbs
    def do_GET(self) -> None:  # noqa: N802
        self._handle("GET")

    def do_POST(self) -> None:  # noqa: N802
        self._handle("POST")

    def do_PUT(self) -> None:  # noqa: N802
        self._handle("PUT")

    def do_DELETE(self) -> None:  # noqa: N802
        self._handle("DELETE")

    def do_PATCH(self) -> None:  # noqa: N802
        self._handle("PATCH")

    # ---------------------------------------------------------------- routing
    def _handle(self, method: str) -> None:
        parts = urlsplit(self.path)
        path = unquote(parts.path)
        query = parse_qs(parts.query, keep_blank_values=True)

        # 读掉请求体，避免连接被客户端半关。
        length = int(self.headers.get("Content-Length") or 0)
        if length:
            self.rfile.read(length)

        if path == "/__healthz":
            self._send_json(
                200,
                {
                    "status": "ok",
                    "fixtures": len(self.fixtures),
                    "strictAuth": self.strict_auth,
                    "delayMs": self.delay_ms,
                },
            )
            return

        if path == "/__fixtures":
            self._send_json(
                200,
                [
                    {
                        "name": f["name"],
                        "operationId": f.get("operationId"),
                        "method": f["request"]["method"],
                        "path": f["request"]["path"],
                        "status": f["response"]["status"],
                        "file": f["_rel"],
                    }
                    for f in self.fixtures
                ],
            )
            return

        if path == "/__openapi":
            self._send_bytes(
                200,
                OPENAPI.read_bytes(),
                {"Content-Type": "application/yaml; charset=utf-8"},
            )
            return

        wanted = (query.get("__fixture") or [None])[0] or self.headers.get("X-Fixture")
        delay_ms = self.delay_ms
        if query.get("__delay_ms"):
            try:
                delay_ms = max(delay_ms, int(query["__delay_ms"][0]))
            except ValueError:
                pass
        if delay_ms > 0:
            time.sleep(delay_ms / 1000)

        fixture, ambiguous = pick_fixture(self.fixtures, method, path, wanted)
        if fixture is None:
            self._send_json(
                404,
                {
                    "error": "mock_fixture_not_found",
                    "error_description": f"没有匹配 {method} {path} 的 fixture"
                    + (f"（指定了 {wanted}）" if wanted else ""),
                },
            )
            return

        operation = fixture.get("operationId")
        if self.strict_auth and operation in self.protected_operations:
            if not (self.headers.get("Authorization") or "").lower().startswith("bearer "):
                self._send_json(
                    401,
                    {"error": "invalid_token", "error_description": "缺少 Bearer 令牌（严格模式）"},
                )
                return

        response = fixture.get("response") or {}
        status = int(response.get("status", 200))
        headers = {str(k): str(v) for k, v in (response.get("headers") or {}).items()}
        headers.setdefault("X-Mock-Fixture", fixture["_rel"])
        if ambiguous:
            headers.setdefault("X-Mock-Ambiguous", "1")

        if "json" in response:
            body = json.dumps(response["json"], ensure_ascii=False).encode("utf-8")
            headers.setdefault("Content-Type", "application/json")
            self._send_bytes(status, body, headers)
            return

        if "raw" in response:
            body = str(response["raw"]).encode("utf-8")
            headers.setdefault("Content-Type", "text/plain; charset=utf-8")
            self._send_bytes(status, body, headers)
            return

        if "rawFile" in response:
            payload = (fixture["_dir"] / response["rawFile"]).read_bytes()
            headers.setdefault("Content-Type", "text/plain; charset=utf-8")
            self._send_bytes(status, payload, headers)
            return

        if "file" in response:
            candidate = fixture["_dir"] / response["file"]
            if not candidate.exists():
                candidate = FIXTURES / response["file"]
            if not candidate.exists():
                self._send_json(500, {"error": "mock_fixture_file_missing", "error_description": str(candidate)})
                return
            headers.setdefault("Content-Type", "application/octet-stream")
            self._send_bytes(status, candidate.read_bytes(), headers)
            return

        # 无 body（例如 204、304）。
        self._send_bytes(status, b"", headers)

    # ------------------------------------------------------------ openapi info
    protected_operations = {
        "getUserInfo",
        "postUserInfo",
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
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="EliCloud contract mock server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=4010)
    parser.add_argument("--strict-auth", action="store_true", help="受保护端点必须带 Authorization: Bearer")
    parser.add_argument("--delay-ms", type=int, default=0, help="所有响应前额外延迟（毫秒）")
    args = parser.parse_args(argv)

    if not FIXTURES.is_dir():
        print(f"找不到 fixtures 目录：{FIXTURES}", file=sys.stderr)
        return 2

    Handler.fixtures = load_fixtures()
    Handler.strict_auth = args.strict_auth
    Handler.delay_ms = args.delay_ms

    server = ThreadingHTTPServer((args.host, args.port), Handler)
    print(f"EliCloud mock server: http://{args.host}:{args.port}  ({len(Handler.fixtures)} fixtures)")
    print("  GET /__healthz   GET /__fixtures   GET /__openapi")
    print("  选择 fixture：?__fixture=<name>  或  X-Fixture: <name>")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nstopped")
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
