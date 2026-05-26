#!/usr/bin/env python3
import argparse
import json
import random
import socket
import statistics
import struct
import sys
import threading
import time
import traceback
from collections import defaultdict

# MessageType constants
LOGIN_REQUEST = 100
LOGIN_RESPONSE = 101
REGISTER_REQUEST = 102
REGISTER_RESPONSE = 103
LOGOUT = 104

CREATE_ROOM = 200
CREATE_ROOM_RESP = 201
JOIN_ROOM = 202
JOIN_ROOM_RESP = 203
LEAVE_ROOM = 204
ROOM_UPDATE = 205
PLAYER_LEFT = 207
PLAYER_READY = 208

RACE_COUNTDOWN = 300
RACE_START = 301
TYPING_UPDATE = 302
RACE_FINISH = 304
RACE_RESULT = 305

HEARTBEAT_PING = 900
HEARTBEAT_PONG = 901
ERROR = 998

MAX_PLAYERS_PER_ROOM = 5
RACE_TIMEOUT_SECONDS = 300


class TestFailure(Exception):
    pass


def rand_user(prefix: str) -> str:
    return f"{prefix}_{int(time.time())}_{random.randint(1000, 9999)}"


class ProtoClient:
    def __init__(self, host: str, port: int, timeout: float, name: str):
        self.host = host
        self.port = port
        self.timeout = timeout
        self.name = name
        self.sock: socket.socket | None = None
        self.inbox: list[tuple[int, dict | None]] = []

    def connect(self) -> None:
        self.close()
        self.sock = socket.create_connection((self.host, self.port), timeout=self.timeout)
        self.sock.settimeout(self.timeout)

    def close(self) -> None:
        if self.sock is not None:
            try:
                self.sock.close()
            except Exception:
                pass
        self.sock = None
        self.inbox.clear()

    def _sock(self) -> socket.socket:
        if self.sock is None:
            raise TestFailure(f"[{self.name}] socket not connected")
        return self.sock

    def _read_exact(self, n: int, timeout: float) -> bytes:
        s = self._sock()
        s.settimeout(timeout)
        data = bytearray()
        while len(data) < n:
            chunk = s.recv(n - len(data))
            if not chunk:
                raise TestFailure(f"[{self.name}] socket closed while reading")
            data.extend(chunk)
        return bytes(data)

    def send(self, msg_type: int, payload: dict | None = None) -> None:
        s = self._sock()
        body = b""
        if payload is not None:
            body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        header = struct.pack(">IHH", len(body), msg_type, 0)
        s.sendall(header + body)

    def recv(self, timeout: float | None = None) -> tuple[int, dict | None]:
        t = self.timeout if timeout is None else timeout
        header = self._read_exact(8, t)
        body_len, msg_type, _flags = struct.unpack(">IHH", header)
        body = self._read_exact(body_len, t) if body_len > 0 else b""
        payload = None
        if body:
            payload = json.loads(body.decode("utf-8"))
        return msg_type, payload

    def wait_for(self, predicate, timeout: float | None = None, desc: str = "message") -> tuple[int, dict | None]:
        t = self.timeout if timeout is None else timeout

        for idx, (m_type, payload) in enumerate(self.inbox):
            if m_type == ERROR:
                raise TestFailure(f"[{self.name}] server ERROR (inbox): {payload}")
            if predicate(m_type, payload):
                self.inbox.pop(idx)
                return m_type, payload

        deadline = time.time() + t
        while time.time() < deadline:
            remain = max(0.1, deadline - time.time())
            m_type, payload = self.recv(timeout=remain)
            if m_type == ERROR:
                raise TestFailure(f"[{self.name}] server ERROR while waiting {desc}: {payload}")
            if predicate(m_type, payload):
                return m_type, payload
            self.inbox.append((m_type, payload))

        raise TestFailure(f"[{self.name}] timeout waiting {desc}")

    def wait_type(self, msg_type: int, timeout: float | None = None) -> dict | None:
        _, payload = self.wait_for(lambda t, _p: t == msg_type, timeout=timeout, desc=f"type={msg_type}")
        return payload


class RaceConcurrencySuite:
    def __init__(self, host: str, port: int, timeout: float, loops: int, include_timeout_case: bool):
        self.host = host
        self.port = port
        self.timeout = timeout
        self.loops = loops
        self.include_timeout_case = include_timeout_case
        self.results: list[tuple[str, bool, str]] = []
        self.latencies_ms: dict[str, list[float]] = defaultdict(list)
        self.first_fail_case: str | None = None
        self.first_fail_detail: str | None = None

    def _client(self, name: str) -> ProtoClient:
        c = ProtoClient(self.host, self.port, self.timeout, name)
        c.connect()
        return c

    @staticmethod
    def _assert(cond: bool, msg: str) -> None:
        if not cond:
            raise TestFailure(msg)

    def _measure(self, step: str, fn) -> None:
        start = time.perf_counter()
        fn()
        self.latencies_ms[step].append((time.perf_counter() - start) * 1000.0)

    def register_login(self, c: ProtoClient, username: str, password: str = "Passw0rd_123") -> int:
        c.send(REGISTER_REQUEST, {"username": username, "password": password})
        reg = c.wait_type(REGISTER_RESPONSE)
        self._assert(isinstance(reg, dict) and bool(reg.get("success")), f"[{c.name}] register failed: {reg}")

        c.send(LOGIN_REQUEST, {"username": username, "password": password})
        login = c.wait_type(LOGIN_RESPONSE)
        self._assert(isinstance(login, dict) and bool(login.get("success")), f"[{c.name}] login failed: {login}")
        user = login.get("user") if isinstance(login, dict) else None
        self._assert(isinstance(user, dict) and isinstance(user.get("id"), int), f"[{c.name}] missing login user id: {login}")
        return int(user["id"])

    def create_room(self, c: ProtoClient) -> str:
        c.send(CREATE_ROOM, {})
        payload = c.wait_type(CREATE_ROOM_RESP)
        self._assert(isinstance(payload, dict) and bool(payload.get("success")), f"[{c.name}] create room failed: {payload}")
        room_code = payload.get("room_code")
        self._assert(isinstance(room_code, str) and len(room_code) == 6, f"[{c.name}] invalid room code: {payload}")
        return room_code

    def join_room(self, c: ProtoClient, room_code: str) -> dict | None:
        c.send(JOIN_ROOM, {"room_code": room_code})
        payload = c.wait_type(JOIN_ROOM_RESP)
        self._assert(isinstance(payload, dict), f"[{c.name}] invalid JOIN_ROOM_RESP: {payload}")
        return payload

    def race_start_with_guest(self, host: ProtoClient, guest: ProtoClient, guest_username: str) -> str:
        guest.send(PLAYER_READY, {"room_code": "", "is_ready": True})
        host.wait_for(
            lambda t, p: (
                t == ROOM_UPDATE
                and isinstance(p, dict)
                and any(
                    isinstance(x, dict)
                    and x.get("username") == guest_username
                    and bool(x.get("is_ready")) is True
                    for x in (p.get("players") or [])
                )
            ),
            timeout=max(self.timeout, 10.0),
            desc="room update with guest ready",
        )
        host.send(RACE_START, None)
        for c in (host, guest):
            c.wait_type(RACE_COUNTDOWN, timeout=max(self.timeout, 6.0))
            c.wait_type(RACE_COUNTDOWN, timeout=max(self.timeout, 6.0))
            c.wait_type(RACE_COUNTDOWN, timeout=max(self.timeout, 6.0))
        host_start = host.wait_type(RACE_START, timeout=max(self.timeout, 12.0))
        guest.wait_type(RACE_START, timeout=max(self.timeout, 12.0))
        self._assert(isinstance(host_start, dict), f"[{host.name}] missing race start payload")
        passage = str(host_start.get("passage_text") or "")
        self._assert(len(passage) > 0, "empty passage_text")
        return passage

    def run_case(self, name: str, fn) -> None:
        try:
            start = time.perf_counter()
            fn()
            elapsed = (time.perf_counter() - start) * 1000.0
            self.results.append((name, True, f"ok ({elapsed:.1f} ms)"))
        except Exception as ex:
            detail = f"{type(ex).__name__}: {ex}\n{traceback.format_exc(limit=6)}"
            self.results.append((name, False, detail))
            if self.first_fail_case is None:
                self.first_fail_case = name
                self.first_fail_detail = detail
            raise

    def case_simultaneous_joins_capacity(self) -> None:
        host = self._client("sim-host")
        joiners: list[ProtoClient] = []
        try:
            self.register_login(host, rand_user("simh"))
            room = self.create_room(host)

            user_ids = []
            for i in range(8):
                c = self._client(f"sim-j{i+1}")
                joiners.append(c)
                user_ids.append(self.register_login(c, rand_user(f"sim{i+1}")))

            barrier = threading.Barrier(len(joiners))
            results = [None] * len(joiners)

            def worker(idx: int, c: ProtoClient) -> None:
                barrier.wait(timeout=5.0)
                payload = self.join_room(c, room)
                ok = bool(payload.get("success"))
                results[idx] = (ok, payload)

            threads = [threading.Thread(target=worker, args=(idx, c), daemon=True) for idx, c in enumerate(joiners)]
            for t in threads:
                t.start()
            for t in threads:
                t.join(timeout=10.0)

            self._assert(all(r is not None for r in results), "missing join results in simultaneous join")
            success_count = sum(1 for ok, _ in results if ok)
            fail_count = len(results) - success_count
            self._assert(success_count == MAX_PLAYERS_PER_ROOM - 1,
                         f"success join count mismatch: success={success_count} fail={fail_count}")
            self._assert(fail_count == len(results) - (MAX_PLAYERS_PER_ROOM - 1),
                         f"fail join count mismatch: success={success_count} fail={fail_count}")

            for ok, payload in results:
                if not ok:
                    err = str(payload.get("error_message") or "")
                    self._assert("đầy" in err.lower(), f"non-full failure during capacity test: {payload}")
        finally:
            for c in joiners:
                c.close()
            host.close()

    def case_rapid_leave_join_cycles(self) -> None:
        host = self._client("cycle-host")
        guest = self._client("cycle-guest")
        try:
            self.register_login(host, rand_user("cycleh"))
            self.register_login(guest, rand_user("cycleg"))
            room = self.create_room(host)
            first = self.join_room(guest, room)
            self._assert(bool(first.get("success")), f"initial join failed: {first}")

            for _ in range(30):
                guest.send(LEAVE_ROOM, {"room_code": room})
                time.sleep(0.03)
                resp = self.join_room(guest, room)
                self._assert(bool(resp.get("success")), f"rejoin failed in cycle: {resp}")
        finally:
            guest.close()
            host.close()

    def case_host_leave_waiting_transfer(self) -> None:
        host = self._client("wait-host")
        guest = self._client("wait-guest")
        try:
            host_id = self.register_login(host, rand_user("waith"))
            guest_id = self.register_login(guest, rand_user("waitg"))
            room = self.create_room(host)
            joined = self.join_room(guest, room)
            self._assert(bool(joined.get("success")), f"guest join failed: {joined}")

            host.send(LEAVE_ROOM, {"room_code": room})
            payload = guest.wait_type(PLAYER_LEFT, timeout=max(self.timeout, 8.0))
            self._assert(isinstance(payload, dict), f"guest missing PLAYER_LEFT payload: {payload}")
            new_host = payload.get("new_host_user_id")
            self._assert(new_host == guest_id, f"host transfer failed, expected {guest_id}, got {payload}")

            # New host should be able to start race as solo player.
            guest.send(RACE_START, None)
            guest.wait_type(RACE_COUNTDOWN, timeout=max(self.timeout, 6.0))
            guest.wait_type(RACE_COUNTDOWN, timeout=max(self.timeout, 6.0))
            guest.wait_type(RACE_COUNTDOWN, timeout=max(self.timeout, 6.0))
            started = guest.wait_type(RACE_START, timeout=max(self.timeout, 8.0))
            self._assert(isinstance(started, dict), f"new host could not start race: {started}")
        finally:
            guest.close()
            host.close()

    def case_host_leave_during_race_transfer(self) -> None:
        host = self._client("raceleave-host")
        guest = self._client("raceleave-guest")
        try:
            self.register_login(host, rand_user("rlh"))
            guest_user = rand_user("rlg")
            guest_id = self.register_login(guest, guest_user)

            room = self.create_room(host)
            joined = self.join_room(guest, room)
            self._assert(bool(joined.get("success")), f"guest join failed: {joined}")

            passage = self.race_start_with_guest(host, guest, guest_user)
            _ = passage  # only ensure race started

            host.send(LEAVE_ROOM, {"room_code": room})
            left = guest.wait_type(PLAYER_LEFT, timeout=max(self.timeout, 8.0))
            self._assert(isinstance(left, dict), f"missing PLAYER_LEFT after host leave in race: {left}")
            self._assert(left.get("new_host_user_id") == guest_id, f"host transfer in race failed: {left}")

            guest.send(RACE_FINISH, {"room_code": room, "correct_chars": 30, "wrong_chars": 1, "time_taken_ms": 1500})
            result = guest.wait_type(RACE_RESULT, timeout=max(self.timeout, 10.0))
            self._assert(isinstance(result, dict) and len(result.get("results", [])) >= 1,
                         f"race result missing after host leave during race: {result}")
        finally:
            guest.close()
            host.close()

    def case_duplicate_finish(self) -> None:
        host = self._client("dupfinish-host")
        guest = self._client("dupfinish-guest")
        try:
            self.register_login(host, rand_user("dfh"))
            guest_user = rand_user("dfg")
            self.register_login(guest, guest_user)
            room = self.create_room(host)
            joined = self.join_room(guest, room)
            self._assert(bool(joined.get("success")), f"join failed: {joined}")

            passage = self.race_start_with_guest(host, guest, guest_user)
            cc = max(20, min(100, len(passage)))

            host.send(RACE_FINISH, {"room_code": room, "correct_chars": cc, "wrong_chars": 2, "time_taken_ms": 1300})
            host.send(RACE_FINISH, {"room_code": room, "correct_chars": cc, "wrong_chars": 2, "time_taken_ms": 1300})
            guest.send(RACE_FINISH, {"room_code": room, "correct_chars": cc - 5, "wrong_chars": 3, "time_taken_ms": 1400})

            payload = host.wait_type(RACE_RESULT, timeout=max(self.timeout, 12.0))
            self._assert(isinstance(payload, dict), f"missing race result for duplicate finish test: {payload}")
            results = payload.get("results", [])
            self._assert(isinstance(results, list) and len(results) == 2, f"unexpected race result size: {payload}")
            user_ids = [x.get("user_id") for x in results if isinstance(x, dict)]
            self._assert(len(set(user_ids)) == 2, f"duplicate user in race result: {payload}")
        finally:
            guest.close()
            host.close()

    def case_typing_burst_out_of_range(self) -> None:
        host = self._client("typing-host")
        guest = self._client("typing-guest")
        try:
            self.register_login(host, rand_user("th"))
            guest_user = rand_user("tg")
            self.register_login(guest, guest_user)
            room = self.create_room(host)
            joined = self.join_room(guest, room)
            self._assert(bool(joined.get("success")), f"join failed: {joined}")

            passage = self.race_start_with_guest(host, guest, guest_user)
            cc = max(20, min(120, len(passage)))

            for i in range(250):
                host.send(TYPING_UPDATE, {
                    "room_code": room,
                    "current_position": random.choice([-100, 0, 5, 99999]),
                    "correct_chars": random.choice([-999, 0, cc, 99999]),
                    "wrong_chars": random.choice([-100, 0, 3, 5000]),
                    "timestamp": int(time.time() * 1000) + i,
                })

            host.send(RACE_FINISH, {"room_code": room, "correct_chars": cc, "wrong_chars": 1, "time_taken_ms": 1400})
            guest.send(RACE_FINISH, {"room_code": room, "correct_chars": cc - 4, "wrong_chars": 2, "time_taken_ms": 1450})

            for c in (host, guest):
                result = c.wait_type(RACE_RESULT, timeout=max(self.timeout, 15.0))
                self._assert(isinstance(result, dict), f"[{c.name}] missing result after typing burst: {result}")
        finally:
            guest.close()
            host.close()

    def case_race_timeout_never_finish(self) -> None:
        host = self._client("timeout-host")
        guest = self._client("timeout-guest")
        stop_heartbeat = threading.Event()
        heartbeat_threads: list[threading.Thread] = []

        def start_heartbeat(client: ProtoClient) -> None:
            def _loop() -> None:
                while not stop_heartbeat.is_set():
                    try:
                        client.send(HEARTBEAT_PING, None)
                    except Exception:
                        return
                    stop_heartbeat.wait(10.0)

            t = threading.Thread(target=_loop, daemon=True)
            t.start()
            heartbeat_threads.append(t)

        try:
            self.register_login(host, rand_user("toh"))
            guest_user = rand_user("tog")
            self.register_login(guest, guest_user)
            room = self.create_room(host)
            joined = self.join_room(guest, room)
            self._assert(bool(joined.get("success")), f"join failed: {joined}")

            # Keep connections alive during the long race timeout window.
            start_heartbeat(host)
            start_heartbeat(guest)

            passage = self.race_start_with_guest(host, guest, guest_user)
            cc = max(20, min(120, len(passage)))

            host.send(RACE_FINISH, {"room_code": room, "correct_chars": cc, "wrong_chars": 1, "time_taken_ms": 1300})

            timeout = RACE_TIMEOUT_SECONDS + 30
            result = host.wait_type(RACE_RESULT, timeout=timeout)
            self._assert(isinstance(result, dict), f"missing timeout race result: {result}")
            rows = result.get("results", [])
            self._assert(isinstance(rows, list) and len(rows) == 2, f"timeout result should include 2 players: {result}")
            self._assert(any(isinstance(x, dict) and not bool(x.get("is_completed", True)) for x in rows),
                         f"timeout result missing uncompleted player: {result}")
        finally:
            stop_heartbeat.set()
            for t in heartbeat_threads:
                t.join(timeout=0.2)
            guest.close()
            host.close()

    def case_repeated_create_leave_loops(self) -> None:
        c = self._client("loops")
        try:
            self.register_login(c, rand_user("loop"))
            for i in range(self.loops):
                room_box = {"code": ""}

                def _create() -> None:
                    room_box["code"] = self.create_room(c)

                self._measure("loop_create_room", _create)

                def _leave() -> None:
                    c.send(LEAVE_ROOM, {"room_code": room_box["code"]})

                self._measure("loop_leave_room", _leave)

                if i % 20 == 0:
                    time.sleep(0.02)
        finally:
            c.close()

    def run(self) -> int:
        cases = [
            ("simultaneous_joins_capacity", self.case_simultaneous_joins_capacity),
            ("rapid_leave_join_cycles", self.case_rapid_leave_join_cycles),
            ("host_leave_waiting_transfer", self.case_host_leave_waiting_transfer),
            ("host_leave_during_race_transfer", self.case_host_leave_during_race_transfer),
            ("duplicate_race_finish", self.case_duplicate_finish),
            ("typing_burst_out_of_range", self.case_typing_burst_out_of_range),
            ("repeated_create_leave_loops", self.case_repeated_create_leave_loops),
        ]
        if self.include_timeout_case:
            cases.append(("race_timeout_never_finish", self.case_race_timeout_never_finish))

        for case_name, fn in cases:
            try:
                self.run_case(case_name, fn)
            except Exception:
                break

        self.print_summary()
        return 0 if self.first_fail_case is None else 1

    def print_summary(self) -> None:
        print("\n=== RACE CONCURRENCY SUMMARY ===")
        for name, ok, detail in self.results:
            print(f"- {name}: {'PASS' if ok else 'FAIL'}")
            if not ok:
                print(f"  detail: {detail.splitlines()[0]}")

        if self.latencies_ms:
            print("\n--- Loop step latency (ms) ---")
            for step in sorted(self.latencies_ms.keys()):
                vals = self.latencies_ms[step]
                avg = statistics.fmean(vals)
                p95 = sorted(vals)[max(0, int(0.95 * len(vals)) - 1)]
                print(f"{step}: count={len(vals)} avg={avg:.2f} p95={p95:.2f} min={min(vals):.2f} max={max(vals):.2f}")

        if self.first_fail_case:
            print("\n--- First failure ---")
            print(f"case: {self.first_fail_case}")
            print(self.first_fail_detail or "")

        passed = sum(1 for _, ok, _ in self.results if ok)
        failed = len(self.results) - passed
        print(f"\nResult: passed={passed} failed={failed} overall={'PASS' if failed == 0 else 'FAIL'}")


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="TypeRacer race/room concurrency edge-case tester")
    p.add_argument("--host", default="134.209.108.82")
    p.add_argument("--port", type=int, default=5000)
    p.add_argument("--timeout", type=float, default=8.0, help="default socket wait timeout (seconds)")
    p.add_argument("--loops", type=int, default=200, help="repeated create/leave loop count")
    p.add_argument(
        "--include-timeout-case",
        action="store_true",
        help="run race-timeout case (waits ~5 minutes by design)",
    )
    return p.parse_args()


def main() -> int:
    args = parse_args()
    print(
        f"Target: {args.host}:{args.port} | timeout={args.timeout}s | loops={args.loops} "
        f"| include_timeout_case={args.include_timeout_case}"
    )
    suite = RaceConcurrencySuite(args.host, args.port, args.timeout, args.loops, args.include_timeout_case)
    return suite.run()


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("Interrupted", file=sys.stderr)
        sys.exit(130)
