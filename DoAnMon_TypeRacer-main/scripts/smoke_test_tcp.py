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
ROOM_LIST_REQUEST = 210
ROOM_LIST_RESPONSE = 211
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
    header = struct.pack(">IHH", len(body), msg_type, 0)
    sock.sendall(header + body)


def recv_message(sock: socket.socket) -> tuple[int, int, dict | None]:
    header = read_exact(sock, 8)
    body_length, msg_type, flags = struct.unpack(">IHH", header)
    body_bytes = read_exact(sock, body_length) if body_length > 0 else b""
    payload = json.loads(body_bytes.decode("utf-8")) if body_bytes else None
    return msg_type, flags, payload


def wait_for_type(sock: socket.socket, expected: int, timeout_sec: float = 6.0) -> dict | None:
    deadline = time.time() + timeout_sec
    while time.time() < deadline:
        remaining = deadline - time.time()
        sock.settimeout(max(0.5, remaining))
        msg_type, _, payload = recv_message(sock)
        if msg_type == expected:
            return payload
        if msg_type == ERROR:
            raise RuntimeError(f"Server error payload: {payload}")
    raise TimeoutError(f"Timeout waiting for message type {expected}")


def main() -> int:
    parser = argparse.ArgumentParser(description="TypeRacer TCP protocol smoke test")
    parser.add_argument("--host", default="134.209.108.82")
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--username")
    parser.add_argument("--password", default="Passw0rd_123")
    args = parser.parse_args()

    username = args.username or f"smoke_{int(time.time())}_{random.randint(100, 999)}"
    password = args.password

    print(f"Connecting to {args.host}:{args.port}")
    with socket.create_connection((args.host, args.port), timeout=10) as sock:
        send_message(sock, REGISTER_REQUEST, {"username": username, "password": password})
        register_resp = wait_for_type(sock, REGISTER_RESPONSE)
        if not register_resp or not register_resp.get("success", False):
            raise RuntimeError(f"REGISTER failed: {register_resp}")
        print(f"REGISTER ok: {username}")

        send_message(sock, LOGIN_REQUEST, {"username": username, "password": password})
        login_resp = wait_for_type(sock, LOGIN_RESPONSE)
        if not login_resp or not login_resp.get("success", False):
            raise RuntimeError(f"LOGIN failed: {login_resp}")
        user = login_resp.get("user", {}) if isinstance(login_resp, dict) else {}
        print(f"LOGIN ok: user_id={user.get('id')} username={user.get('username')}")

        send_message(sock, CREATE_ROOM, {})
        room_resp = wait_for_type(sock, CREATE_ROOM_RESP)
        if not room_resp or not room_resp.get("success", False):
            raise RuntimeError(f"CREATE_ROOM failed: {room_resp}")
        room_code = room_resp.get("room_code")
        print(f"CREATE_ROOM ok: room_code={room_code}")

        send_message(sock, ROOM_LIST_REQUEST, {})
        room_list_resp = wait_for_type(sock, ROOM_LIST_RESPONSE)
        rooms = room_list_resp.get("rooms", []) if isinstance(room_list_resp, dict) else []
        exists = any(r.get("room_code") == room_code for r in rooms if isinstance(r, dict))
        if not exists:
            raise RuntimeError(f"ROOM_LIST missing created room {room_code}. Payload={room_list_resp}")
        print(f"ROOM_LIST ok: found {len(rooms)} rooms including {room_code}")

    print("SMOKE TEST PASSED")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print(f"SMOKE TEST FAILED: {exc}", file=sys.stderr)
        sys.exit(1)
