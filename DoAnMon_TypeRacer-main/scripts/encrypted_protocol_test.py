#!/usr/bin/env python3
import argparse
import hashlib
import json
import os
import random
import socket
import struct
import sys
import time

from cryptography.hazmat.primitives import padding
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes


ENCRYPTED_FLAG = 1
SHARED_SECRET = "TypeRacer2026NT106UIT!SecretKey32".encode("utf-8")
AES_KEY = hashlib.sha256(SHARED_SECRET).digest()

LOGIN_REQUEST = 100
LOGIN_RESPONSE = 101
REGISTER_REQUEST = 102
REGISTER_RESPONSE = 103
CREATE_ROOM = 200
CREATE_ROOM_RESP = 201
ROOM_LIST_REQUEST = 210
ROOM_LIST_RESPONSE = 211
ERROR = 998


def aes_encrypt(plain: bytes) -> bytes:
    iv = os.urandom(16)
    padder = padding.PKCS7(128).padder()
    padded = padder.update(plain) + padder.finalize()
    encryptor = Cipher(algorithms.AES(AES_KEY), modes.CBC(iv)).encryptor()
    return iv + encryptor.update(padded) + encryptor.finalize()


def aes_decrypt(cipher_body: bytes) -> bytes:
    if len(cipher_body) <= 16:
        raise ValueError("encrypted body is too short")

    iv = cipher_body[:16]
    cipher_text = cipher_body[16:]
    decryptor = Cipher(algorithms.AES(AES_KEY), modes.CBC(iv)).decryptor()
    padded = decryptor.update(cipher_text) + decryptor.finalize()
    unpadder = padding.PKCS7(128).unpadder()
    return unpadder.update(padded) + unpadder.finalize()


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
    flags = 0
    if payload is not None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        body = aes_encrypt(body)
        flags |= ENCRYPTED_FLAG

    sock.sendall(struct.pack(">IHH", len(body), msg_type, flags) + body)


def recv_message(sock: socket.socket) -> tuple[int, int, dict | None]:
    header = read_exact(sock, 8)
    body_length, msg_type, flags = struct.unpack(">IHH", header)
    body_bytes = read_exact(sock, body_length) if body_length > 0 else b""

    if flags & ENCRYPTED_FLAG:
        body_bytes = aes_decrypt(body_bytes)

    payload = json.loads(body_bytes.decode("utf-8")) if body_bytes else None
    return msg_type, flags, payload


def wait_for_type(sock: socket.socket, expected: int, timeout_sec: float = 6.0) -> tuple[int, dict | None]:
    deadline = time.time() + timeout_sec
    while time.time() < deadline:
        remaining = deadline - time.time()
        sock.settimeout(max(0.5, remaining))
        msg_type, flags, payload = recv_message(sock)
        if msg_type == expected:
            return flags, payload
        if msg_type == ERROR:
            raise RuntimeError(f"Server error payload: {payload}")
    raise TimeoutError(f"Timeout waiting for message type {expected}")


def assert_encrypted_response(flags: int, payload: dict | None, label: str) -> None:
    if payload is None:
        raise AssertionError(f"{label}: expected payload")
    if not (flags & ENCRYPTED_FLAG):
        raise AssertionError(f"{label}: server response was not encrypted; flags={flags}")


def main() -> int:
    parser = argparse.ArgumentParser(description="TypeRacer AES-encrypted TCP protocol smoke test")
    parser.add_argument("--host", default="134.209.108.82")
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--timeout", type=float, default=10.0)
    parser.add_argument("--username")
    parser.add_argument("--password", default="Passw0rd_123")
    args = parser.parse_args()

    username = args.username or f"enc_{int(time.time())}_{random.randint(1000, 9999)}"

    print(f"Connecting encrypted protocol to {args.host}:{args.port}")
    with socket.create_connection((args.host, args.port), timeout=args.timeout) as sock:
        send_message(sock, REGISTER_REQUEST, {"username": username, "password": args.password})
        flags, register_resp = wait_for_type(sock, REGISTER_RESPONSE, args.timeout)
        assert_encrypted_response(flags, register_resp, "REGISTER_RESPONSE")
        if not register_resp.get("success", False):
            raise RuntimeError(f"REGISTER failed: {register_resp}")
        print(f"REGISTER encrypted ok: {username}")

        send_message(sock, LOGIN_REQUEST, {"username": username, "password": args.password})
        flags, login_resp = wait_for_type(sock, LOGIN_RESPONSE, args.timeout)
        assert_encrypted_response(flags, login_resp, "LOGIN_RESPONSE")
        if not login_resp.get("success", False):
            raise RuntimeError(f"LOGIN failed: {login_resp}")
        print("LOGIN encrypted ok")

        send_message(sock, CREATE_ROOM, {"race_duration_seconds": 30, "game_mode": "classic"})
        flags, room_resp = wait_for_type(sock, CREATE_ROOM_RESP, args.timeout)
        assert_encrypted_response(flags, room_resp, "CREATE_ROOM_RESP")
        if not room_resp.get("success", False):
            raise RuntimeError(f"CREATE_ROOM failed: {room_resp}")
        room_code = room_resp.get("room_code")
        print(f"CREATE_ROOM encrypted ok: {room_code}")

        send_message(sock, ROOM_LIST_REQUEST, {})
        flags, room_list_resp = wait_for_type(sock, ROOM_LIST_RESPONSE, args.timeout)
        assert_encrypted_response(flags, room_list_resp, "ROOM_LIST_RESPONSE")
        rooms = room_list_resp.get("rooms", []) if isinstance(room_list_resp, dict) else []
        if not any(r.get("room_code") == room_code for r in rooms if isinstance(r, dict)):
            raise RuntimeError(f"ROOM_LIST missing created room {room_code}. Payload={room_list_resp}")
        print("ROOM_LIST encrypted ok")

    print("ENCRYPTED PROTOCOL TEST PASSED")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print(f"ENCRYPTED PROTOCOL TEST FAILED: {exc}", file=sys.stderr)
        sys.exit(1)
