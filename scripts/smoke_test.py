#!/usr/bin/env python3
"""
MP Auth/Catalog API smoke (happy + bad path).
Windows:  python scripts\\smoke_test.py
Requires: docker compose up (gateway http://localhost:11080)
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import json
import os
import sys
import time
import urllib.error
import urllib.request
import uuid

BASE = os.environ.get("BASE", "http://localhost:11080").rstrip("/")
JWT_SECRET = os.environ.get("JWT_SECRET", "")
PASS = 0
FAIL = 0
RESULTS: list[dict] = []


def req(method: str, path: str, body: dict | None = None, headers: dict | None = None):
    url = path if path.startswith("http") else f"{BASE}{path}"
    data = None
    hdr = dict(headers or {})
    if body is not None:
        data = json.dumps(body).encode()
        hdr.setdefault("Content-Type", "application/json")
    r = urllib.request.Request(url, data=data, headers=hdr, method=method)
    try:
        with urllib.request.urlopen(r, timeout=20) as resp:
            return resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()
    except Exception as e:
        return 0, str(e)


def expect(name: str, method: str, path: str, expected: int, body=None, headers=None, allow_non_json=False):
    global PASS, FAIL
    code, raw = req(method, path, body, headers)
    ok = code == expected
    note = ""
    if ok and expected not in (204,) and raw and not allow_non_json:
        try:
            json.loads(raw)
        except json.JSONDecodeError:
            if not (path.endswith("/health") and raw.strip().lower() == "ok"):
                ok = False
                note = f" non-JSON body={raw[:120]}"
    if ok:
        print(f"  PASS  {name}  ({code})")
        PASS += 1
        RESULTS.append({"name": name, "ok": True, "code": code})
    else:
        print(f"  FAIL  {name}  expected={expected} got={code}{note} body={raw[:250]}")
        FAIL += 1
        RESULTS.append({"name": name, "ok": False, "code": code, "body": raw[:500]})
    return code, raw


def parse_login(raw: str):
    j = json.loads(raw)
    return j.get("access_token") or "", j.get("refresh_token") or "", j.get("mp_account_id") or ""


def main():
    global PASS, FAIL
    print(f"=== MP smoke against {BASE} ===")
    suffix = uuid.uuid4().hex[:8]
    username = f"smoke_{suffix}"
    password = "test1234"
    device_id = f"smoke-device-{suffix}"

    print("--- health ---")
    expect("GET gateway /health", "GET", "/health", 200, allow_non_json=True)

    print("--- auth bad path ---")
    expect(
        "login missing provider -> 400",
        "POST",
        "/api/v1/auth/login",
        400,
        {"provider": "", "device_id": device_id, "auth_payload": {"username": "a", "password": "b"}},
    )
    expect(
        "login missing device_id -> 400",
        "POST",
        "/api/v1/auth/login",
        400,
        {"provider": "official", "device_id": "", "auth_payload": {"username": "a", "password": "bbbbbb"}},
    )
    expect(
        "login bad password short -> 401",
        "POST",
        "/api/v1/auth/login",
        401,
        {
            "provider": "official",
            "app_id": "test_app",
            "device_id": device_id,
            "auth_payload": {"username": username, "password": "123"},
        },
    )
    expect("me no auth -> 401", "GET", "/api/v1/auth/me", 401)
    expect(
        "me bad bearer -> 401",
        "GET",
        "/api/v1/auth/me",
        401,
        headers={"Authorization": "Bearer not.a.jwt"},
    )
    expect(
        "refresh bad id -> 400",
        "POST",
        "/api/v1/auth/refresh",
        400,
        {"mp_account_id": "not-a-guid", "refresh_token": "x", "device_id": device_id},
    )

    print("--- auth happy ---")
    code, raw = expect(
        "login official (register) -> 200",
        "POST",
        "/api/v1/auth/login",
        200,
        {
            "provider": "official",
            "app_id": "test_app",
            "device_id": device_id,
            "auth_payload": {"username": username, "password": password},
        },
    )
    access = refresh = account_id = ""
    if code == 200:
        try:
            access, refresh, account_id = parse_login(raw)
        except json.JSONDecodeError:
            pass

    if not access:
        print("  FAIL  no access_token; skip token-dependent tests")
        FAIL += 1
    else:
        if JWT_SECRET:
            parts = access.split(".")
            if len(parts) == 3:
                sig_input = f"{parts[0]}.{parts[1]}".encode()
                exp = (
                    base64.urlsafe_b64encode(hmac.new(JWT_SECRET.encode(), sig_input, hashlib.sha256).digest())
                    .decode()
                    .rstrip("=")
                    .replace("+", "-")
                    .replace("/", "_")
                )
                if exp == parts[2]:
                    print("  PASS  access_token HMAC matches JWT_SECRET")
                    PASS += 1
                else:
                    print("  FAIL  access_token HMAC mismatch (check JWT_SECRET)")
                    FAIL += 1
            else:
                print("  FAIL  access_token not 3-part JWT")
                FAIL += 1

        expect(
            "me with token -> 200",
            "GET",
            "/api/v1/auth/me",
            200,
            headers={"Authorization": f"Bearer {access}"},
        )

        # refresh MUST use tokens from the latest login for this device_id
        # (a second login rotates refresh in Redis and invalidates the old one)
        if refresh and account_id:
            code2, raw2 = expect(
                "refresh -> 200",
                "POST",
                "/api/v1/auth/refresh",
                200,
                {
                    "mp_account_id": account_id,
                    "refresh_token": refresh,
                    "device_id": device_id,
                },
            )
            if code2 == 200:
                try:
                    j2 = json.loads(raw2)
                    access = j2.get("access_token") or access
                    refresh = j2.get("refresh_token") or refresh
                except json.JSONDecodeError:
                    pass

        # login again AFTER refresh: proves re-login works and rotates tokens
        code3, raw3 = expect(
            "login again same user -> 200",
            "POST",
            "/api/v1/auth/login",
            200,
            {
                "provider": "official",
                "app_id": "test_app",
                "device_id": device_id,
                "auth_payload": {"username": username, "password": password},
            },
        )
        if code3 == 200:
            try:
                access, refresh, account_id = parse_login(raw3)
            except json.JSONDecodeError:
                pass

        # old refresh after re-login should fail (rotation semantics)
        if refresh and account_id:
            # use a deliberately stale token: empty is not useful; skip if we only have latest
            pass

        expect(
            "login wrong password -> 401",
            "POST",
            "/api/v1/auth/login",
            401,
            {
                "provider": "official",
                "device_id": device_id,
                "auth_payload": {"username": username, "password": "wrong-password"},
            },
        )

    print("--- steam (dev login) ---")
    steam_id = f"7656119{suffix.zfill(10)[:10]}"
    expect(
        "steam dev login missing steam_id -> 401",
        "POST",
        "/api/v1/auth/login",
        401,
        {
            "provider": "steam",
            "app_id": "480",
            "device_id": device_id,
            "auth_payload": {},
        },
    )
    code_s, raw_s = expect(
        "steam dev login -> 200",
        "POST",
        "/api/v1/auth/login",
        200,
        {
            "provider": "steam",
            "app_id": "480",
            "device_id": f"steam-{device_id}",
            "auth_payload": {"steam_id": steam_id},
        },
    )
    if code_s == 200:
        try:
            access_s, _, account_s = parse_login(raw_s)
            if access_s:
                expect(
                    "me after steam login -> 200",
                    "GET",
                    "/api/v1/auth/me",
                    200,
                    headers={"Authorization": f"Bearer {access_s}"},
                )
        except json.JSONDecodeError:
            pass

    print("--- catalog ---")
    expect("list games -> 200", "GET", "/api/v1/catalog/games", 200)
    expect("list games status filter -> 200", "GET", "/api/v1/catalog/games?status=active", 200)
    expect(
        "get missing game -> 404",
        "GET",
        "/api/v1/catalog/games/__smoke_missing_game__",
        404,
    )

    print(f"=== summary: PASS={PASS} FAIL={FAIL} ===")

    out_dir = os.path.join(os.path.dirname(__file__), "..", "test-results")
    os.makedirs(out_dir, exist_ok=True)
    report = {
        "suite": "MP API smoke",
        "baseUrl": BASE,
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "pass": PASS,
        "fail": FAIL,
        "results": RESULTS,
        "python": sys.version.split()[0],
        "platform": sys.platform,
    }
    json_path = os.path.join(out_dir, "smoke-report.json")
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2, ensure_ascii=False)
    junit_path = os.path.join(out_dir, "smoke-junit.xml")
    with open(junit_path, "w", encoding="utf-8") as f:
        f.write(
            f"""<?xml version="1.0" encoding="UTF-8"?>
<testsuite name="MP.Smoke" tests="{PASS + FAIL}" failures="{FAIL}" timestamp="{report['timestamp']}">
  <testcase classname="MP.Smoke" name="all">
    {"" if FAIL == 0 else '<failure message="one or more checks failed"/>'}
  </testcase>
</testsuite>
"""
        )
    print(f"Wrote {json_path}")
    print(f"Wrote {junit_path}")
    sys.exit(1 if FAIL else 0)


if __name__ == "__main__":
    main()
