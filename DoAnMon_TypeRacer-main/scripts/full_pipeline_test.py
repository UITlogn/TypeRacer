#!/usr/bin/env python3
import argparse
import json
import random
import socket
import statistics
import struct
import sys
import time
import traceback
from collections import defaultdict
from dataclasses import dataclass
from typing import Callable

# MessageType constants (TypeRacer.Shared.Protocol.MessageType)
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
PLAYER_JOINED = 206
PLAYER_LEFT = 207
PLAYER_READY = 208
ROOM_LIST_REQUEST = 210
ROOM_LIST_RESPONSE = 211

RACE_COUNTDOWN = 300
RACE_START = 301
TYPING_UPDATE = 302
PROGRESS_BROADCAST = 303
RACE_FINISH = 304
RACE_RESULT = 305

CHAT_SEND = 400
CHAT_BROADCAST = 401

GET_PROFILE = 500
PROFILE_RESPONSE = 501
GET_LEADERBOARD = 502
LEADERBOARD_RESP = 503
GET_MATCH_HISTORY = 504
MATCH_HISTORY_RESP = 505
GET_AI_COACH = 506
AI_COACH_RESPONSE = 507

HEARTBEAT_PING = 900
HEARTBEAT_PONG = 901
ERROR = 998
DISCONNECT = 999


def now_ms() -> int:
    return int(time.time() * 1000)


def rand_user(prefix: str) -> str:
    return f"{prefix}_{int(time.time())}_{random.randint(1000, 9999)}"


VI_DIACRITIC_CHARS = set(
    "áàảãạăắằẳẵặâấầẩẫậ"
    "éèẻẽẹêếềểễệ"
    "íìỉĩị"
    "óòỏõọôốồổỗộơớờởỡợ"
    "úùủũụưứừửữự"
    "ýỳỷỹỵ"
    "đ"
    "ÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬ"
    "ÉÈẺẼẸÊẾỀỂỄỆ"
    "ÍÌỈĨỊ"
    "ÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢ"
    "ÚÙỦŨỤƯỨỪỬỮỰ"
    "ÝỲỶỸỴ"
    "Đ"
)


def normalize_language(raw: str | None) -> str:
    code = (raw or "en").strip().lower()
    if code in ("vi", "en", "any"):
        return code
    return "en"


def has_vietnamese_diacritic(text: str) -> bool:
    return any(ch in VI_DIACRITIC_CHARS for ch in text)


class TestFailure(Exception):
    pass


@dataclass
class StepResult:
    name: str
    elapsed_ms: float


class ProtoClient:
    def __init__(self, host: str, port: int, timeout: float, name: str):
        self.host = host
        self.port = port
        self.timeout = timeout
        self.name = name
        self.sock: socket.socket | None = None
        self.inbox: list[tuple[int, int, dict | None]] = []

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

    def _ensure(self) -> socket.socket:
        if self.sock is None:
            raise TestFailure(f"[{self.name}] socket not connected")
        return self.sock

    def _read_exact(self, size: int, timeout: float) -> bytes:
        sock = self._ensure()
        sock.settimeout(timeout)
        data = bytearray()
        while len(data) < size:
            chunk = sock.recv(size - len(data))
            if not chunk:
                raise TestFailure(f"[{self.name}] socket closed while reading")
            data.extend(chunk)
        return bytes(data)

    def recv_message(self, timeout: float | None = None) -> tuple[int, int, dict | None]:
        t = self.timeout if timeout is None else timeout
        header = self._read_exact(8, t)
        body_length, msg_type, flags = struct.unpack(">IHH", header)
        body = self._read_exact(body_length, t) if body_length > 0 else b""
        payload = None
        if body:
            try:
                payload = json.loads(body.decode("utf-8"))
            except Exception as ex:
                raise TestFailure(f"[{self.name}] invalid json payload for type={msg_type}: {ex}") from ex
        return msg_type, flags, payload

    def send(self, msg_type: int, payload: dict | None = None) -> None:
        sock = self._ensure()
        body = b""
        if payload is not None:
            body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        sock.sendall(struct.pack(">IHH", len(body), msg_type, 0) + body)

    def _scan_inbox(self, predicate: Callable[[int, dict | None], bool], allow_error: bool) -> tuple[int, dict | None] | None:
        for idx, (t, _f, p) in enumerate(self.inbox):
            if t == ERROR and not allow_error:
                raise TestFailure(f"[{self.name}] server ERROR: {p}")
            if predicate(t, p):
                self.inbox.pop(idx)
                return t, p
        return None

    def wait_for(self, predicate: Callable[[int, dict | None], bool], timeout: float | None = None,
                 allow_error: bool = False, desc: str = "message") -> tuple[int, dict | None]:
        t = self.timeout if timeout is None else timeout
        hit = self._scan_inbox(predicate, allow_error)
        if hit is not None:
            return hit

        deadline = time.time() + t
        while time.time() < deadline:
            remaining = max(0.1, deadline - time.time())
            m_type, _flags, payload = self.recv_message(timeout=remaining)
            if m_type == ERROR and not allow_error:
                raise TestFailure(f"[{self.name}] server ERROR while waiting {desc}: {payload}")
            if predicate(m_type, payload):
                return m_type, payload
            self.inbox.append((m_type, 0, payload))

        raise TestFailure(f"[{self.name}] timeout waiting for {desc}")

    def wait_type(self, expected_type: int, timeout: float | None = None, allow_error: bool = False) -> dict | None:
        _t, payload = self.wait_for(
            lambda m_type, _payload: m_type == expected_type,
            timeout=timeout,
            allow_error=allow_error,
            desc=f"type={expected_type}",
        )
        return payload


class PipelineTester:
    def __init__(self, host: str, port: int, timeout: float, loops: int):
        self.host = host
        self.port = port
        self.timeout = timeout
        self.loops = loops
        self.step_latencies: dict[str, list[float]] = defaultdict(list)
        self.case_results: list[tuple[str, bool, str]] = []

    def _client(self, name: str) -> ProtoClient:
        c = ProtoClient(self.host, self.port, self.timeout, name)
        c.connect()
        return c

    def _measure(self, name: str, fn: Callable[[], None]) -> None:
        start = time.perf_counter()
        fn()
        elapsed = (time.perf_counter() - start) * 1000.0
        self.step_latencies[name].append(elapsed)

    def _assert(self, cond: bool, msg: str) -> None:
        if not cond:
            raise TestFailure(msg)

    def register(self, c: ProtoClient, username: str, password: str, expect_success: bool,
                 err_contains: str | None = None) -> dict | None:
        c.send(REGISTER_REQUEST, {"username": username, "password": password})
        payload = c.wait_type(REGISTER_RESPONSE)
        self._assert(isinstance(payload, dict), f"[{c.name}] invalid REGISTER_RESPONSE payload")
        ok = bool(payload.get("success", False))
        self._assert(ok == expect_success, f"[{c.name}] register expect={expect_success}, got={payload}")
        if not expect_success and err_contains:
            err = str(payload.get("error_message") or "")
            self._assert(err_contains.lower() in err.lower(),
                         f"[{c.name}] expected error contains '{err_contains}', got '{err}'")
        return payload

    def login(self, c: ProtoClient, username: str, password: str, expect_success: bool) -> dict | None:
        c.send(LOGIN_REQUEST, {"username": username, "password": password})
        payload = c.wait_type(LOGIN_RESPONSE)
        self._assert(isinstance(payload, dict), f"[{c.name}] invalid LOGIN_RESPONSE payload")
        ok = bool(payload.get("success", False))
        self._assert(ok == expect_success, f"[{c.name}] login expect={expect_success}, got={payload}")
        return payload

    def create_room_payload(self, c: ProtoClient, passage_language: str | None = None) -> dict:
        request: dict[str, str] = {}
        if passage_language is not None:
            request["passage_language"] = passage_language

        c.send(CREATE_ROOM, request)
        payload = c.wait_type(CREATE_ROOM_RESP)
        self._assert(isinstance(payload, dict) and payload.get("success") is True,
                     f"[{c.name}] create room failed: {payload}")
        room_code = payload.get("room_code")
        self._assert(isinstance(room_code, str) and len(room_code) == 6,
                     f"[{c.name}] invalid room code: {payload}")
        return payload

    def create_room(self, c: ProtoClient, passage_language: str | None = None) -> str:
        payload = self.create_room_payload(c, passage_language=passage_language)
        room_code = payload.get("room_code")
        self._assert(isinstance(room_code, str) and len(room_code) == 6,
                     f"[{c.name}] invalid room code in create_room: {payload}")
        return room_code

    def join_room(self, c: ProtoClient, room_code: str, expect_success: bool,
                  err_contains: str | None = None) -> dict | None:
        c.send(JOIN_ROOM, {"room_code": room_code})
        payload = c.wait_type(JOIN_ROOM_RESP)
        self._assert(isinstance(payload, dict), f"[{c.name}] invalid JOIN_ROOM_RESP payload")
        ok = bool(payload.get("success", False))
        self._assert(ok == expect_success,
                     f"[{c.name}] join room {room_code} expect={expect_success}, got={payload}")
        if not expect_success and err_contains:
            err = str(payload.get("error_message") or "")
            self._assert(err_contains.lower() in err.lower(),
                         f"[{c.name}] expected join error contains '{err_contains}', got '{err}'")
        return payload

    def wait_chat(self, c: ProtoClient, expected_content: str) -> None:
        payload = c.wait_type(CHAT_BROADCAST, timeout=max(2.0, self.timeout))
        self._assert(isinstance(payload, dict), f"[{c.name}] invalid CHAT_BROADCAST payload")
        message = payload.get("message") if isinstance(payload, dict) else None
        self._assert(isinstance(message, dict), f"[{c.name}] missing chat message: {payload}")
        self._assert(str(message.get("content", "")) == expected_content,
                     f"[{c.name}] chat content mismatch: {payload}")

    def _validate_race_start_payload(self, payload: dict | None, room_code: str, requested_language: str) -> tuple[int, str, str]:
        self._assert(isinstance(payload, dict), f"missing race start payload: {payload}")
        race_room_code = str(payload.get("room_code", ""))
        passage_text = str(payload.get("passage_text", ""))
        passage_id = int(payload.get("passage_id") or 0)
        race_language = normalize_language(str(payload.get("passage_language") or "en"))

        self._assert(race_room_code == room_code,
                     f"race start room_code mismatch expected={room_code}, got={payload}")
        self._assert(passage_id > 0, f"race start passage_id must be >0, got={payload}")
        self._assert(len(passage_text) > 0, f"empty passage_text in race start: {payload}")

        normalized_request = normalize_language(requested_language)
        if normalized_request == "any":
            self._assert(race_language in ("en", "vi"),
                         f"race language for any must be en/vi, got={race_language} payload={payload}")
        else:
            self._assert(race_language == normalized_request,
                         f"race language mismatch expected={normalized_request}, got={race_language} payload={payload}")

        return passage_id, passage_text, race_language

    def _start_race(self, host: ProtoClient, guest: ProtoClient, guest_user: str, room_code: str,
                    requested_language: str) -> tuple[dict, dict]:
        guest.send(PLAYER_READY, {"room_code": room_code, "is_ready": True})
        host.wait_for(
            lambda m_type, payload: (
                m_type == ROOM_UPDATE
                and isinstance(payload, dict)
                and any(
                    isinstance(p, dict)
                    and p.get("username") == guest_user
                    and bool(p.get("is_ready")) is True
                    for p in (payload.get("players") or [])
                )
            ),
            timeout=max(self.timeout, 10.0),
            desc="room update with guest ready=true",
        )

        host.send(RACE_START, None)
        host_start = host.wait_type(RACE_START, timeout=max(self.timeout, 15.0))
        guest_start = guest.wait_type(RACE_START, timeout=max(self.timeout, 15.0))
        self._validate_race_start_payload(host_start, room_code, requested_language)
        self._validate_race_start_payload(guest_start, room_code, requested_language)
        return host_start, guest_start

    def _finish_race_pair(self, host: ProtoClient, guest: ProtoClient, room_code: str,
                          passage_text: str) -> tuple[dict, dict]:
        correct_chars = max(20, min(len(passage_text), 120))
        host.send(RACE_FINISH, {
            "room_code": room_code,
            "correct_chars": correct_chars,
            "wrong_chars": 1,
            "time_taken_ms": 1200,
        })
        guest.send(RACE_FINISH, {
            "room_code": room_code,
            "correct_chars": max(10, correct_chars - 5),
            "wrong_chars": 2,
            "time_taken_ms": 1400,
        })

        host_result = host.wait_type(RACE_RESULT, timeout=max(self.timeout, 15.0))
        guest_result = guest.wait_type(RACE_RESULT, timeout=max(self.timeout, 15.0))
        self._assert(isinstance(host_result, dict) and isinstance(guest_result, dict),
                     f"missing race result payload host={host_result} guest={guest_result}")
        self._assert(len(host_result.get("results", [])) >= 2,
                     f"race result too small: {host_result}")
        return host_result, guest_result

    def _find_result_row(self, race_result: dict, username: str) -> dict:
        for row in race_result.get("results", []):
            if isinstance(row, dict) and row.get("username") == username:
                return row
        raise TestFailure(f"cannot find result row for username={username}: {race_result}")

    def _assert_ai_payload_rich(self, payload: dict | None, expected_race_id: int, expected_user_id: int,
                                requested_language: str) -> None:
        self._assert(isinstance(payload, dict), f"invalid AI_COACH_RESPONSE payload: {payload}")
        self._assert(payload.get("success") is True,
                     f"ai coach success=false for language={requested_language}: {payload}")

        coach_text = str(payload.get("coach_text") or "")
        self._assert(len(coach_text.strip()) >= 12,
                     f"ai coach text too short for language={requested_language}: {payload}")

        tips = payload.get("tips")
        self._assert(isinstance(tips, list) and len(tips) >= 3,
                     f"ai coach tips should have >=3 items, got {tips}")
        for idx, tip in enumerate(tips):
            self._assert(isinstance(tip, str) and len(tip.strip()) >= 6,
                         f"ai coach tip[{idx}] invalid: {tips}")

        action_plan = payload.get("action_plan")
        self._assert(isinstance(action_plan, list) and len(action_plan) >= 3,
                     f"ai coach action_plan should have >=3 items, got {action_plan}")
        for idx, step in enumerate(action_plan):
            self._assert(isinstance(step, str) and len(step.strip()) >= 6,
                         f"ai coach action_plan[{idx}] invalid: {action_plan}")

        for field, minimum in (
            ("weak_key_drills", 2),
            ("spaced_repetition_plan", 3),
            ("mastery_checkpoints", 3),
            ("mistake_fingerprint", 3),
            ("adaptive_race_strategy", 3),
        ):
            items = payload.get(field)
            self._assert(isinstance(items, list) and len(items) >= minimum,
                         f"ai coach {field} should have >= {minimum} items, got {items}")

        personalization_score = float(payload.get("personalization_score") or 0)
        ai_confidence_score = float(payload.get("ai_confidence_score") or 0)
        passage_novelty_score = float(payload.get("passage_novelty_score") or 0)
        weakspot_coverage_score = float(payload.get("weakspot_coverage_score") or 0)
        training_pack_signature = str(payload.get("training_pack_signature") or "").strip()
        self._assert(0 <= personalization_score <= 100,
                     f"ai coach personalization_score invalid: {payload}")
        self._assert(0 <= ai_confidence_score <= 100 and ai_confidence_score > 0,
                     f"ai coach ai_confidence_score invalid: {payload}")
        self._assert(0 <= passage_novelty_score <= 100 and passage_novelty_score > 0,
                     f"ai coach passage_novelty_score invalid: {payload}")
        self._assert(0 <= weakspot_coverage_score <= 100 and weakspot_coverage_score > 0,
                     f"ai coach weakspot_coverage_score invalid: {payload}")
        for field, minimum in (
            ("ai_evidence_trail", 3),
            ("generated_passage_audit", 3),
        ):
            items = payload.get(field)
            self._assert(isinstance(items, list) and len(items) >= minimum,
                         f"ai coach {field} should have >= {minimum} items, got {items}")
        problem_key_story_title = str(payload.get("problem_key_story_title") or "").strip()
        problem_key_story_topic = str(payload.get("problem_key_story_topic") or "").strip()
        problem_key_story_keys = payload.get("problem_key_story_keys")
        problem_key_story_passage = str(payload.get("problem_key_story_passage") or "").strip()
        self._assert(len(problem_key_story_title) >= 8,
                     f"ai coach problem_key_story_title missing: {payload}")
        self._assert(len(problem_key_story_topic) >= 8,
                     f"ai coach problem_key_story_topic missing: {payload}")
        self._assert(isinstance(problem_key_story_keys, list) and len(problem_key_story_keys) >= 2,
                     f"ai coach problem_key_story_keys should have >=2 items: {payload}")
        self._assert(len(problem_key_story_passage) >= 60,
                     f"ai coach problem_key_story_passage too short: {payload}")
        self._assert(len(training_pack_signature) >= 8,
                     f"ai coach training_pack_signature missing: {payload}")

        provider = str(payload.get("provider") or "").strip()
        model = str(payload.get("model") or "").strip()
        self._assert(len(provider) > 0, f"ai coach provider missing: {payload}")
        self._assert(len(model) > 0, f"ai coach model missing: {payload}")
        self._assert(isinstance(payload.get("is_fallback"), bool),
                     f"ai coach is_fallback must be bool: {payload}")

        if payload.get("error_message") is not None:
            self._assert(isinstance(payload.get("error_message"), str),
                         f"ai coach error_message invalid: {payload}")

        race_raw = payload.get("race_id")
        user_raw = payload.get("user_id")
        race_id = int(race_raw) if race_raw is not None else -1
        user_id = int(user_raw) if user_raw is not None else -1
        self._assert(race_id == expected_race_id,
                     f"ai coach race_id mismatch expected={expected_race_id}, got={payload}")
        self._assert(user_id == expected_user_id,
                     f"ai coach user_id mismatch expected={expected_user_id}, got={payload}")

    def case_unauthorized_action(self) -> None:
        c = self._client("unauth")
        try:
            c.send(ROOM_LIST_REQUEST, {})
            payload = c.wait_type(ERROR, allow_error=True)
            self._assert(isinstance(payload, dict), "unauthorized should return ERROR payload")
            # code 1003 = InvalidSession in current server enum
            code = int(payload.get("code", -1))
            self._assert(code in (1003, 9001), f"unexpected unauthorized error code: {payload}")
        finally:
            c.close()

    def case_invalid_and_duplicate_register(self) -> None:
        c = self._client("register")
        try:
            self.register(c, "ab", "Passw0rd_123", expect_success=False, err_contains="3-50")
            self.register(c, rand_user("usr"), "123", expect_success=False, err_contains="6")

            user = rand_user("dup")
            self.register(c, user, "Passw0rd_123", expect_success=True)
            self.register(c, user, "Passw0rd_123", expect_success=False, err_contains="đã")
        finally:
            c.close()

    def case_wrong_password_login(self) -> None:
        c = self._client("wrongpass")
        try:
            user = rand_user("wp")
            self.register(c, user, "Passw0rd_123", expect_success=True)
            self.login(c, user, "wrong_password", expect_success=False)
        finally:
            c.close()

    def case_join_missing_room(self) -> None:
        c = self._client("join-missing")
        try:
            user = rand_user("join")
            self.register(c, user, "Passw0rd_123", expect_success=True)
            self.login(c, user, "Passw0rd_123", expect_success=True)
            self.join_room(c, "ZZZZZZ", expect_success=False, err_contains="không")
        finally:
            c.close()

    def case_room_full(self) -> None:
        host = self._client("roomfull-host")
        joiners: list[ProtoClient] = []
        extra: ProtoClient | None = None
        try:
            host_user = rand_user("fullh")
            self.register(host, host_user, "Passw0rd_123", expect_success=True)
            self.login(host, host_user, "Passw0rd_123", expect_success=True)
            room_code = self.create_room(host)

            # max players = 5, including host => 4 successful joiners
            for i in range(4):
                c = self._client(f"roomfull-j{i+1}")
                joiners.append(c)
                u = rand_user(f"full{i+1}")
                self.register(c, u, "Passw0rd_123", expect_success=True)
                self.login(c, u, "Passw0rd_123", expect_success=True)
                self.join_room(c, room_code, expect_success=True)

            extra = self._client("roomfull-extra")
            u = rand_user("fullextra")
            self.register(extra, u, "Passw0rd_123", expect_success=True)
            self.login(extra, u, "Passw0rd_123", expect_success=True)
            self.join_room(extra, room_code, expect_success=False, err_contains="đầy")
        finally:
            try:
                host.send(LEAVE_ROOM, {"room_code": ""})
            except Exception:
                pass
            for c in joiners:
                c.close()
            if extra:
                extra.close()
            host.close()

    def case_race_chat_leaderboard_match_history(self) -> None:
        host = self._client("race-host")
        guest = self._client("race-guest")
        try:
            host_user = rand_user("raceh")
            guest_user = rand_user("raceg")

            self.register(host, host_user, "Passw0rd_123", expect_success=True)
            self.login(host, host_user, "Passw0rd_123", expect_success=True)

            self.register(guest, guest_user, "Passw0rd_123", expect_success=True)
            self.login(guest, guest_user, "Passw0rd_123", expect_success=True)

            room_code = self.create_room(host)
            self.join_room(guest, room_code, expect_success=True)

            # chat path
            chat_text = f"hello-{rand_user('chat')}"
            host.send(CHAT_SEND, {"room_code": room_code, "content": chat_text})
            self.wait_chat(host, chat_text)
            self.wait_chat(guest, chat_text)

            host_start, _guest_start = self._start_race(
                host, guest, guest_user, room_code, requested_language="en"
            )
            _passage_id, passage, _race_language = self._validate_race_start_payload(
                host_start, room_code, requested_language="en"
            )
            host_result, _guest_result = self._finish_race_pair(host, guest, room_code, passage)

            # AI coach flow
            host_row = self._find_result_row(host_result, host_user)
            expected_race_id = int(host_result.get("race_id") or 0)
            expected_user_id = int(host_row.get("user_id") or 0)
            self._assert(expected_user_id > 0, f"host user_id invalid in race result: {host_result}")

            for ai_language in ("vi", "en", "any"):
                host.send(GET_AI_COACH, {
                    "room_code": room_code,
                    "race_id": expected_race_id,
                    "user_id": expected_user_id,
                    "username": host_user,
                    "position": int(host_row.get("position") or 1),
                    "total_players": len(host_result.get("results", [])),
                    "wpm": float(host_row.get("wpm") or 0),
                    "accuracy": float(host_row.get("accuracy") or 0),
                    "chars_correct": int(host_row.get("chars_correct") or 0),
                    "chars_wrong": int(host_row.get("chars_wrong") or 0),
                    "time_taken_ms": int(host_row.get("time_taken_ms") or 1),
                    "is_completed": bool(host_row.get("is_completed") is True),
                    "language": ai_language,
                })
                ai = host.wait_type(AI_COACH_RESPONSE, timeout=max(self.timeout, 10.0))
                self._assert_ai_payload_rich(
                    ai,
                    expected_race_id=expected_race_id,
                    expected_user_id=expected_user_id,
                    requested_language=ai_language,
                )

            # leaderboard
            host.send(GET_LEADERBOARD, {"sort_by": "avg_wpm", "top": 10})
            lb = host.wait_type(LEADERBOARD_RESP)
            self._assert(isinstance(lb, dict), f"invalid leaderboard payload: {lb}")
            self._assert(isinstance(lb.get("entries", []), list), f"invalid leaderboard entries: {lb}")

            # match history may be eventually consistent due async DB write, retry a few times
            got_matches = False
            deadline = time.time() + 10.0
            while time.time() < deadline:
                host.send(GET_MATCH_HISTORY, {"limit": 20})
                mh = host.wait_type(MATCH_HISTORY_RESP)
                if isinstance(mh, dict) and isinstance(mh.get("matches", []), list) and len(mh["matches"]) > 0:
                    got_matches = True
                    break
                time.sleep(0.4)

            self._assert(got_matches, "match history did not include latest race within retry window")
        finally:
            host.close()
            guest.close()

    def case_language_matrix_antirepeat_practice_protocol(self) -> None:
        host = self._client("lang-host")
        guest = self._client("lang-guest")
        try:
            host_user = rand_user("langh")
            guest_user = rand_user("langg")

            self.register(host, host_user, "Passw0rd_123", expect_success=True)
            self.login(host, host_user, "Passw0rd_123", expect_success=True)
            self.register(guest, guest_user, "Passw0rd_123", expect_success=True)
            self.login(guest, guest_user, "Passw0rd_123", expect_success=True)

            for requested_language in ("en", "any", "vi"):
                create_resp = self.create_room_payload(host, passage_language=requested_language)
                room_code = str(create_resp.get("room_code", ""))
                normalized_request = normalize_language(requested_language)

                room_meta = create_resp.get("room") if isinstance(create_resp, dict) else None
                self._assert(
                    isinstance(room_meta, dict) and normalize_language(room_meta.get("passage_language")) == normalized_request,
                    f"create room language mismatch request={requested_language} payload={create_resp}",
                )

                join_resp = self.join_room(guest, room_code, expect_success=True)
                join_room = join_resp.get("room") if isinstance(join_resp, dict) else None
                self._assert(
                    isinstance(join_room, dict) and normalize_language(join_room.get("passage_language")) == normalized_request,
                    f"join room language mismatch request={requested_language} payload={join_resp}",
                )

                # Race #1: verify practice-mode required protocol fields + vi unicode diacritic.
                host_start_1, _guest_start_1 = self._start_race(
                    host, guest, guest_user, room_code, requested_language=requested_language
                )
                passage_id_1, passage_text_1, _race_lang_1 = self._validate_race_start_payload(
                    host_start_1, room_code, requested_language
                )
                if normalized_request == "vi":
                    self._assert(
                        has_vietnamese_diacritic(passage_text_1),
                        f"vi passage lacks Vietnamese diacritics: passage_id={passage_id_1} text={passage_text_1!r}",
                    )
                self._finish_race_pair(host, guest, room_code, passage_text_1)

                # Race #2 in same room/language should avoid immediate repeat.
                host_start_2, _guest_start_2 = self._start_race(
                    host, guest, guest_user, room_code, requested_language=requested_language
                )
                passage_id_2, passage_text_2, _race_lang_2 = self._validate_race_start_payload(
                    host_start_2, room_code, requested_language
                )
                self._assert(
                    passage_id_2 != passage_id_1,
                    f"anti-repeat failed for language={requested_language}: consecutive passage_id={passage_id_1}",
                )
                self._assert(
                    passage_text_2 != passage_text_1,
                    f"anti-repeat failed for language={requested_language}: consecutive passage_text repeated",
                )
                self._finish_race_pair(host, guest, room_code, passage_text_2)

                # Clean room for next language loop.
                guest.send(LEAVE_ROOM, {"room_code": room_code})
                host.send(LEAVE_ROOM, {"room_code": room_code})
                time.sleep(0.2)
        finally:
            host.close()
            guest.close()

    def run_edge_cases(self) -> None:
        self.case_unauthorized_action()
        self.case_invalid_and_duplicate_register()
        self.case_wrong_password_login()
        self.case_join_missing_room()
        self.case_room_full()
        self.case_race_chat_leaderboard_match_history()
        self.case_language_matrix_antirepeat_practice_protocol()

    def run_stress(self) -> None:
        for i in range(self.loops):
            username = rand_user(f"stress{i}")
            c = self._client(f"stress-{i+1}")
            try:
                self._measure("register", lambda: self.register(c, username, "Passw0rd_123", expect_success=True))
                self._measure("login", lambda: self.login(c, username, "Passw0rd_123", expect_success=True))

                room_holder: dict[str, str] = {}

                def _create() -> None:
                    room_holder["code"] = self.create_room(c)

                self._measure("create_room", _create)

                def _list() -> None:
                    c.send(ROOM_LIST_REQUEST, {})
                    payload = c.wait_type(ROOM_LIST_RESPONSE)
                    self._assert(isinstance(payload, dict), f"invalid room list payload: {payload}")
                    rooms = payload.get("rooms", []) if isinstance(payload, dict) else []
                    self._assert(any(isinstance(r, dict) and r.get("room_code") == room_holder["code"] for r in rooms),
                                 f"created room missing from room list loop={i+1}")

                self._measure("room_list", _list)

                # best-effort leave
                def _leave() -> None:
                    c.send(LEAVE_ROOM, {"room_code": room_holder["code"]})

                self._measure("leave_room", _leave)
            finally:
                c.close()

    def run_all(self) -> tuple[bool, str | None, str | None]:
        first_fail_case = None
        first_fail_detail = None

        suites: list[tuple[str, Callable[[], None]]] = [
            ("edge_cases", self.run_edge_cases),
            (f"stress_{self.loops}", self.run_stress),
        ]

        for case_name, fn in suites:
            try:
                start = time.perf_counter()
                fn()
                elapsed = (time.perf_counter() - start) * 1000.0
                self.case_results.append((case_name, True, f"ok ({elapsed:.1f} ms)"))
            except Exception as ex:
                detail = f"{type(ex).__name__}: {ex}\n{traceback.format_exc(limit=5)}"
                self.case_results.append((case_name, False, detail))
                if first_fail_case is None:
                    first_fail_case = case_name
                    first_fail_detail = detail
                break

        success = first_fail_case is None
        return success, first_fail_case, first_fail_detail

    def print_summary(self, success: bool, first_fail_case: str | None, first_fail_detail: str | None) -> None:
        print("\n=== PIPELINE TEST SUMMARY ===")
        for name, ok, detail in self.case_results:
            print(f"- {name}: {'PASS' if ok else 'FAIL'}")
            if not ok:
                print(f"  detail: {detail.splitlines()[0]}")

        print("\n--- Step latency (ms) ---")
        for step in sorted(self.step_latencies.keys()):
            samples = self.step_latencies[step]
            if not samples:
                continue
            avg = statistics.fmean(samples)
            p95 = sorted(samples)[max(0, int(len(samples) * 0.95) - 1)]
            print(f"{step}: count={len(samples)} avg={avg:.2f} p95={p95:.2f} min={min(samples):.2f} max={max(samples):.2f}")

        if first_fail_case:
            print("\n--- First failing case ---")
            print(f"case: {first_fail_case}")
            print(first_fail_detail or "")

        total = len(self.case_results)
        passed = sum(1 for _, ok, _ in self.case_results if ok)
        failed = total - passed
        print(f"\nResult: passed={passed} failed={failed} overall={'PASS' if success else 'FAIL'}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="TypeRacer full pipeline + edge case + stress tester")
    parser.add_argument("--host", default="134.209.108.82", help="Server host")
    parser.add_argument("--port", type=int, default=5000, help="Server port")
    parser.add_argument("--loops", type=int, default=100, help="Stress loop iterations")
    parser.add_argument("--timeout", type=float, default=75.0, help="Socket/message timeout in seconds")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    print(f"Target: {args.host}:{args.port} | loops={args.loops} | timeout={args.timeout}s")

    tester = PipelineTester(args.host, args.port, args.timeout, args.loops)
    success, first_fail_case, first_fail_detail = tester.run_all()
    tester.print_summary(success, first_fail_case, first_fail_detail)

    return 0 if success else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("Interrupted", file=sys.stderr)
        sys.exit(130)
