#!/usr/bin/env python3
import argparse
import json
import random
import socket
import struct
import sys
import time

LOGIN_REQUEST = 100
LOGIN_RESPONSE = 101
REGISTER_REQUEST = 102
REGISTER_RESPONSE = 103
CREATE_ROOM = 200
CREATE_ROOM_RESP = 201
RACE_COUNTDOWN = 300
RACE_START = 301
RACE_FINISH = 304
RACE_RESULT = 305
LEAVE_ROOM = 204
ERROR = 998


def read_exact(sock: socket.socket, size: int) -> bytes:
    data = bytearray()
    while len(data) < size:
        chunk = sock.recv(size - len(data))
        if not chunk:
            raise ConnectionError("Socket closed while reading.")
        data.extend(chunk)
    return bytes(data)


def send_message(sock: socket.socket, msg_type: int, payload: dict | None = None) -> None:
    body = b""
    if payload is not None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    sock.sendall(struct.pack(">IHH", len(body), msg_type, 0) + body)


def recv_message(sock: socket.socket) -> tuple[int, dict | None]:
    header = read_exact(sock, 8)
    body_length, msg_type, _ = struct.unpack(">IHH", header)
    body_bytes = read_exact(sock, body_length) if body_length else b""
    payload = json.loads(body_bytes.decode("utf-8")) if body_bytes else None
    return msg_type, payload


def wait_for_type(sock: socket.socket, expected: int, timeout_sec: float) -> dict | None:
    deadline = time.time() + timeout_sec
    while time.time() < deadline:
        sock.settimeout(max(0.5, deadline - time.time()))
        msg_type, payload = recv_message(sock)
        if msg_type == expected:
            return payload
        if msg_type == ERROR:
            raise RuntimeError(f"Server error: {payload}")
    raise TimeoutError(f"Timeout waiting for message type {expected}")


def mutate_first_char(text: str) -> str:
    if not text:
        return "x"
    replacement = "x" if text[0] != "x" else "z"
    return replacement + text[1:]


def connect_user(host: str, port: int, timeout: float, prefix: str) -> socket.socket:
    username = f"mode_{int(time.time())}_{random.randint(100, 999)}"
    password = "Passw0rd_123"
    sock = socket.create_connection((host, port), timeout=timeout)

    send_message(sock, REGISTER_REQUEST, {"username": f"{prefix}_{username}", "password": password})
    register_resp = wait_for_type(sock, REGISTER_RESPONSE, timeout)
    if not register_resp or not register_resp.get("success"):
        sock.close()
        raise RuntimeError(f"REGISTER failed: {register_resp}")

    send_message(sock, LOGIN_REQUEST, {"username": f"{prefix}_{username}", "password": password})
    login_resp = wait_for_type(sock, LOGIN_RESPONSE, timeout)
    if not login_resp or not login_resp.get("success"):
        sock.close()
        raise RuntimeError(f"LOGIN failed: {login_resp}")

    return sock


def start_single_player_race(sock: socket.socket, mode: str, timeout: float, ai_difficulty: str | None = None) -> tuple[str, dict]:
    payload = {
        "passage_language": "vi",
        "race_duration_seconds": 30,
        "enable_ai_mode": False,
        "game_mode": mode,
    }
    if ai_difficulty:
        payload["ai_practice_difficulty"] = ai_difficulty

    send_message(sock, CREATE_ROOM, payload)
    room_resp = wait_for_type(sock, CREATE_ROOM_RESP, timeout)
    if not room_resp or not room_resp.get("success"):
        raise RuntimeError(f"CREATE_ROOM failed for {mode}: {room_resp}")

    room = room_resp.get("room", {})
    room_code = room_resp.get("room_code")
    if room.get("game_mode") != mode:
        raise RuntimeError(f"Room game_mode was not preserved for {mode}: {room_resp}")
    if ai_difficulty and room.get("ai_practice_difficulty") != ai_difficulty:
        raise RuntimeError(f"Room AI difficulty was not preserved for {mode}/{ai_difficulty}: {room_resp}")

    send_message(sock, RACE_START)
    deadline = time.time() + timeout
    while time.time() < deadline:
        msg_type, payload = recv_message(sock)
        if msg_type == RACE_COUNTDOWN:
            continue
        if msg_type == RACE_START:
            if payload.get("game_mode") != mode:
                raise RuntimeError(f"RaceStart missing {mode} mode: {payload}")
            if ai_difficulty and payload.get("ai_practice_difficulty") != ai_difficulty:
                raise RuntimeError(f"RaceStart missing AI difficulty {ai_difficulty}: {payload}")
            return room_code, payload
        if msg_type == ERROR:
            raise RuntimeError(f"Race start error for {mode}: {payload}")

    raise TimeoutError(f"Race did not start for {mode}.")


def finish_and_assert(sock: socket.socket, mode: str, room_code: str, start_payload: dict, timeout: float, ai_difficulty: str | None = None) -> None:
    passage = start_payload.get("passage_text", "")
    typed_text = passage
    backspace_count = 0

    if mode == "sudden_death":
        typed_text = mutate_first_char(passage)
    elif mode == "no_backspace":
        backspace_count = 1

    send_message(sock, RACE_FINISH, {
        "room_code": room_code,
        "correct_chars": len(passage),
        "wrong_chars": 0,
        "time_taken_ms": 1500,
        "typed_text": typed_text,
        "backspace_count": backspace_count,
    })

    result_payload = wait_for_type(sock, RACE_RESULT, timeout)
    results = result_payload.get("results", []) if isinstance(result_payload, dict) else []
    expected_result_count = 2 if ai_difficulty else 1
    if len(results) != expected_result_count:
        raise RuntimeError(f"Expected {expected_result_count} result(s) for {mode}: {result_payload}")

    if ai_difficulty:
        bot_results = [r for r in results if r.get("is_ai_bot")]
        if len(bot_results) != 1:
            raise RuntimeError(f"AI practice did not include exactly one AI bot result: {result_payload}")
        bot = bot_results[0]
        if "AI" not in bot.get("username", ""):
            raise RuntimeError(f"AI bot username was not marked clearly: {bot}")
        if bot.get("game_mode") != mode:
            raise RuntimeError(f"AI bot result missing {mode} mode: {bot}")
        if not isinstance(bot.get("achievements"), list) or not any("AI" in x for x in bot.get("achievements", [])):
            raise RuntimeError(f"AI bot result missing AI achievement badge: {bot}")

    human_results = [r for r in results if not r.get("is_ai_bot")]
    if len(human_results) != 1:
        raise RuntimeError(f"Expected one human result for {mode}: {result_payload}")

    result = human_results[0]
    if result.get("game_mode") != mode:
        raise RuntimeError(f"Result missing {mode} mode: {result}")
    if result.get("best_streak") is None or result.get("consistency_score") is None:
        raise RuntimeError(f"Missing streak/consistency fields for {mode}: {result}")
    if not isinstance(result.get("achievements"), list):
        raise RuntimeError(f"Missing achievements list for {mode}: {result}")

    if mode in {"sudden_death", "no_backspace"}:
        if not result.get("is_disqualified"):
            raise RuntimeError(f"{mode} did not disqualify invalid finish: {result}")
        if result.get("is_completed"):
            raise RuntimeError(f"Disqualified {mode} race should not be completed: {result}")
        if "Bị loại" not in result.get("achievements", []):
            raise RuntimeError(f"Missing disqualification badge for {mode}: {result}")
    else:
        if result.get("is_disqualified"):
            raise RuntimeError(f"{mode} unexpectedly disqualified clean finish: {result}")
        if not result.get("is_completed"):
            raise RuntimeError(f"{mode} clean finish was not completed: {result}")
        if "Về đích" not in result.get("achievements", []):
            raise RuntimeError(f"Missing completion badge for {mode}: {result}")


def run_mode(host: str, port: int, timeout: float, mode: str, ai_difficulty: str | None = None) -> None:
    prefix = f"{mode}_{ai_difficulty}" if ai_difficulty else mode
    sock = connect_user(host, port, timeout, prefix)
    try:
        room_code, start_payload = start_single_player_race(sock, mode, timeout, ai_difficulty)
        finish_and_assert(sock, mode, room_code, start_payload, timeout, ai_difficulty)
        send_message(sock, LEAVE_ROOM, {"room_code": room_code})
    finally:
        sock.close()


def main() -> int:
    parser = argparse.ArgumentParser(description="TypeRacer game-mode protocol test")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--timeout", type=float, default=20.0)
    args = parser.parse_args()

    for mode in ("classic", "accuracy", "no_backspace", "sudden_death"):
        run_mode(args.host, args.port, args.timeout, mode)
        print(f"- {mode}: PASS")

    for difficulty in ("easy", "medium", "hard", "nightmare"):
        run_mode(args.host, args.port, args.timeout, "ai_practice", difficulty)
        print(f"- ai_practice/{difficulty}: PASS")

    print("GAME MODE PROTOCOL TEST PASSED")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print(f"GAME MODE PROTOCOL TEST FAILED: {exc}", file=sys.stderr)
        sys.exit(1)
