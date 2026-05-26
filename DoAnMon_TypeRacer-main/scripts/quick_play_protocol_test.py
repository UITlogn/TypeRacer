#!/usr/bin/env python3
import argparse
import json
import random
import socket
import struct
import sys
import time

REGISTER_REQUEST = 102
REGISTER_RESPONSE = 103
LOGIN_REQUEST = 100
LOGIN_RESPONSE = 101
JOIN_ROOM = 202
JOIN_ROOM_RESP = 203
ROOM_LIST_REQUEST = 210
ROOM_LIST_RESPONSE = 211
ERROR = 998

COMMUNITY_ROOM_CODE = "QUICK"


def read_exact(sock: socket.socket, size: int) -> bytes:
    data = bytearray()
    while len(data) < size:
        chunk = sock.recv(size - len(data))
        if not chunk:
            raise ConnectionError("socket closed")
        data.extend(chunk)
    return bytes(data)


def send_message(sock: socket.socket, msg_type: int, payload: dict | None = None) -> None:
    body = b""
    if payload is not None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    sock.sendall(struct.pack(">IHH", len(body), msg_type, 0) + body)


def recv_message(sock: socket.socket) -> tuple[int, dict | None]:
    header = read_exact(sock, 8)
    length, msg_type, _flags = struct.unpack(">IHH", header)
    body = read_exact(sock, length) if length else b""
    return msg_type, json.loads(body.decode("utf-8")) if body else None


def wait_for_type(sock: socket.socket, expected: int, timeout: float = 8.0) -> dict | None:
    deadline = time.time() + timeout
    while time.time() < deadline:
        sock.settimeout(max(0.5, deadline - time.time()))
        msg_type, payload = recv_message(sock)
        if msg_type == expected:
            return payload
        if msg_type == ERROR:
            raise RuntimeError(f"server error: {payload}")
    raise TimeoutError(f"timeout waiting for {expected}")


def main() -> int:
    parser = argparse.ArgumentParser(description="TypeRacer community Quick Play protocol test")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5000)
    args = parser.parse_args()

    username = f"quick_{int(time.time())}_{random.randint(100, 999)}"
    password = "Passw0rd_123"

    with socket.create_connection((args.host, args.port), timeout=10) as sock:
        send_message(sock, REGISTER_REQUEST, {"username": username, "password": password})
        register = wait_for_type(sock, REGISTER_RESPONSE)
        if not register or not register.get("success"):
            raise RuntimeError(f"register failed: {register}")

        send_message(sock, LOGIN_REQUEST, {"username": username, "password": password})
        login = wait_for_type(sock, LOGIN_RESPONSE)
        if not login or not login.get("success"):
            raise RuntimeError(f"login failed: {login}")

        send_message(sock, ROOM_LIST_REQUEST, {})
        room_list = wait_for_type(sock, ROOM_LIST_RESPONSE)
        rooms = room_list.get("rooms", []) if isinstance(room_list, dict) else []
        quick = next((r for r in rooms if r.get("room_code") == COMMUNITY_ROOM_CODE), None)
        if quick is None:
            raise RuntimeError(f"missing {COMMUNITY_ROOM_CODE} in room list: {room_list}")
        if not quick.get("is_community_room"):
            raise RuntimeError(f"{COMMUNITY_ROOM_CODE} is not marked community: {quick}")
        if not quick.get("is_joinable_in_progress"):
            raise RuntimeError(f"{COMMUNITY_ROOM_CODE} is not joinable in progress: {quick}")

        send_message(sock, JOIN_ROOM, {"room_code": COMMUNITY_ROOM_CODE})
        join = wait_for_type(sock, JOIN_ROOM_RESP)
        if not join or not join.get("success"):
            raise RuntimeError(f"join QUICK failed: {join}")
        room = join.get("room") or {}
        if room.get("room_code") != COMMUNITY_ROOM_CODE:
            raise RuntimeError(f"joined wrong room: {join}")

        print("QUICK room list ok")
        print(f"QUICK join ok: race_in_progress={bool(join.get('race_in_progress'))}")
        if join.get("current_race"):
            race = join["current_race"]
            print(f"current race elapsed={race.get('race_elapsed_seconds')}s passage_len={len(race.get('passage_text', ''))}")

    print("QUICK PLAY PROTOCOL TEST PASSED")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"QUICK PLAY PROTOCOL TEST FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1)
