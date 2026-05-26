#!/usr/bin/env python3
import argparse
import json
import random
import socket
import struct
import sys
import time
import traceback
from dataclasses import dataclass

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
PLAYER_READY = 208
ROOM_LIST_REQUEST = 210
ROOM_LIST_RESPONSE = 211

RACE_START = 301
TYPING_UPDATE = 302
RACE_FINISH = 304
RACE_RESULT = 305

CHAT_SEND = 400
CHAT_BROADCAST = 401

GET_LEADERBOARD = 502
LEADERBOARD_RESP = 503
GET_MATCH_HISTORY = 504
MATCH_HISTORY_RESP = 505
GET_AI_COACH = 506
AI_COACH_RESPONSE = 507

HEARTBEAT_PING = 900
HEARTBEAT_PONG = 901
ERROR = 998

# ErrorCode constants
INVALID_SESSION = 1003
INVALID_MESSAGE = 9001


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


@dataclass
class CaseResult:
    name: str
    passed: bool
    detail: str
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
            raise RuntimeError(f"[{self.name}] not connected")
        return self.sock

    def _read_exact(self, size: int, timeout: float) -> bytes:
        sock = self._ensure()
        sock.settimeout(timeout)
        data = bytearray()
        while len(data) < size:
            chunk = sock.recv(size - len(data))
            if not chunk:
                raise ConnectionError(f"[{self.name}] socket closed")
            data.extend(chunk)
        return bytes(data)

    def recv_message(self, timeout: float | None = None) -> tuple[int, int, dict | None]:
        t = self.timeout if timeout is None else timeout
        header = self._read_exact(8, t)
        body_length, msg_type, flags = struct.unpack(">IHH", header)
        body = self._read_exact(body_length, t) if body_length > 0 else b""
        payload = None
        if body:
            payload = json.loads(body.decode("utf-8"))
        return msg_type, flags, payload

    def send(self, msg_type: int, payload: dict | None = None) -> None:
        sock = self._ensure()
        body = b""
        if payload is not None:
            body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        sock.sendall(struct.pack(">IHH", len(body), msg_type, 0) + body)

    def send_empty(self, msg_type: int) -> None:
        self.send(msg_type, None)

    def _scan_inbox(self, predicate, allow_error: bool):
        for i, (m_type, _flags, payload) in enumerate(self.inbox):
            if m_type == ERROR and not allow_error:
                raise AssertionError(f"[{self.name}] server ERROR: {payload}")
            if predicate(m_type, payload):
                self.inbox.pop(i)
                return m_type, payload
        return None

    def wait_for(self, predicate, timeout: float | None = None,
                 allow_error: bool = False, desc: str = "message") -> tuple[int, dict | None]:
        t = self.timeout if timeout is None else timeout
        hit = self._scan_inbox(predicate, allow_error)
        if hit is not None:
            return hit

        deadline = time.time() + t
        while time.time() < deadline:
            remain = max(0.1, deadline - time.time())
            m_type, _flags, payload = self.recv_message(timeout=remain)
            if m_type == ERROR and not allow_error:
                raise AssertionError(f"[{self.name}] server ERROR while waiting {desc}: {payload}")
            if predicate(m_type, payload):
                return m_type, payload
            self.inbox.append((m_type, 0, payload))
        raise TimeoutError(f"[{self.name}] timeout waiting {desc}")

    def wait_type(self, msg_type: int, timeout: float | None = None,
                  allow_error: bool = False) -> dict | None:
        _type, payload = self.wait_for(
            lambda t, _p: t == msg_type,
            timeout=timeout,
            allow_error=allow_error,
            desc=f"type={msg_type}",
        )
        return payload


def assert_true(cond: bool, msg: str) -> None:
    if not cond:
        raise AssertionError(msg)


def expect_error(c: ProtoClient, code_candidates: set[int], timeout: float = 4.0) -> dict | None:
    payload = c.wait_type(ERROR, timeout=timeout, allow_error=True)
    assert_true(isinstance(payload, dict), f"[{c.name}] expected ERROR payload dict, got {payload}")
    code = int(payload.get("code", -1))
    assert_true(code in code_candidates,
                f"[{c.name}] expected error code in {sorted(code_candidates)}, got {payload}")
    return payload


def register_ok(c: ProtoClient, username: str, password: str) -> None:
    c.send(REGISTER_REQUEST, {"username": username, "password": password})
    p = c.wait_type(REGISTER_RESPONSE)
    assert_true(isinstance(p, dict) and p.get("success") is True,
                f"[{c.name}] register failed: {p}")


def login_ok(c: ProtoClient, username: str, password: str) -> dict | None:
    c.send(LOGIN_REQUEST, {"username": username, "password": password})
    p = c.wait_type(LOGIN_RESPONSE)
    assert_true(isinstance(p, dict) and p.get("success") is True,
                f"[{c.name}] login failed: {p}")
    return p


def start_race_and_wait(host: ProtoClient, guest: ProtoClient, guest_user: str,
                        room_code: str, timeout: float) -> tuple[dict, dict]:
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
        timeout=max(8.0, timeout),
        desc="room update with guest ready=true",
    )

    host.send_empty(RACE_START)
    h_start = host.wait_type(RACE_START, timeout=max(15.0, timeout))
    g_start = guest.wait_type(RACE_START, timeout=max(15.0, timeout))
    assert_true(isinstance(h_start, dict), f"missing host RACE_START payload: {h_start}")
    assert_true(isinstance(g_start, dict), f"missing guest RACE_START payload: {g_start}")
    return h_start, g_start


def finish_race_pair(host: ProtoClient, guest: ProtoClient, room_code: str,
                     passage_text: str, timeout: float) -> tuple[dict, dict]:
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

    h_result = host.wait_type(RACE_RESULT, timeout=max(15.0, timeout))
    g_result = guest.wait_type(RACE_RESULT, timeout=max(15.0, timeout))
    assert_true(isinstance(h_result, dict), f"missing host RACE_RESULT payload: {h_result}")
    assert_true(isinstance(g_result, dict), f"missing guest RACE_RESULT payload: {g_result}")
    return h_result, g_result


def assert_ai_payload_rich(resp: dict | None, expected_race_id: int, expected_user_id: int,
                           label: str) -> None:
    assert_true(isinstance(resp, dict), f"[{label}] invalid AI_COACH_RESPONSE: {resp}")
    assert_true(resp.get("success") is True, f"[{label}] expected success=true: {resp}")

    coach_text = str(resp.get("coach_text") or "").strip()
    assert_true(len(coach_text) >= 12, f"[{label}] coach_text too short: {resp}")

    tips = resp.get("tips")
    assert_true(isinstance(tips, list) and len(tips) >= 3, f"[{label}] tips should have >=3 items: {resp}")
    for idx, tip in enumerate(tips):
        assert_true(isinstance(tip, str) and len(tip.strip()) >= 6,
                    f"[{label}] invalid tips[{idx}]={tip!r} payload={resp}")

    action_plan = resp.get("action_plan")
    assert_true(isinstance(action_plan, list) and len(action_plan) >= 3,
                f"[{label}] action_plan should have >=3 items: {resp}")
    for idx, step in enumerate(action_plan):
        assert_true(isinstance(step, str) and len(step.strip()) >= 6,
                    f"[{label}] invalid action_plan[{idx}]={step!r} payload={resp}")

    provider = str(resp.get("provider") or "").strip()
    model = str(resp.get("model") or "").strip()
    assert_true(len(provider) > 0, f"[{label}] provider missing: {resp}")
    assert_true(len(model) > 0, f"[{label}] model missing: {resp}")
    assert_true(isinstance(resp.get("is_fallback"), bool), f"[{label}] is_fallback not bool: {resp}")

    training_title = str(resp.get("training_title") or "").strip()
    recommended_mode = str(resp.get("recommended_game_mode") or "").strip()
    recommended_difficulty = str(resp.get("recommended_difficulty") or "").strip()
    recommended_target_rpm = int(resp.get("recommended_target_rpm") or 0)
    assert_true(len(training_title) >= 8, f"[{label}] training_title missing: {resp}")
    assert_true(recommended_mode in {"classic", "accuracy", "no_backspace", "sudden_death", "ai_practice"},
                f"[{label}] recommended_game_mode invalid: {resp}")
    assert_true(recommended_difficulty in {"easy", "medium", "hard", "nightmare"},
                f"[{label}] recommended_difficulty invalid: {resp}")
    assert_true(15 <= recommended_target_rpm <= 180,
                f"[{label}] recommended_target_rpm invalid: {resp}")
    daily_title = str(resp.get("daily_challenge_title") or "").strip()
    daily_goal = str(resp.get("daily_challenge_goal") or "").strip()
    daily_reward = str(resp.get("daily_challenge_reward") or "").strip()
    practice_words = resp.get("practice_words")
    assert_true(len(daily_title) >= 8, f"[{label}] daily_challenge_title missing: {resp}")
    assert_true(len(daily_goal) >= 16, f"[{label}] daily_challenge_goal missing: {resp}")
    assert_true(len(daily_reward) >= 6, f"[{label}] daily_challenge_reward missing: {resp}")
    assert_true(isinstance(practice_words, list) and len(practice_words) >= 3,
                f"[{label}] practice_words should have >=3 items: {resp}")

    adaptive_micro_lessons = resp.get("adaptive_micro_lessons")
    mistake_heatmap = resp.get("mistake_heatmap")
    next_session_checklist = resp.get("next_session_checklist")
    ghost_race_plan = resp.get("ghost_race_plan")
    finger_diagnostics = resp.get("finger_diagnostics")
    progress_prediction = resp.get("progress_prediction")
    lesson_ladder = resp.get("lesson_ladder")
    attempt_replay_cues = resp.get("attempt_replay_cues")
    weak_key_drills = resp.get("weak_key_drills")
    ngram_drills = resp.get("ngram_drills")
    spaced_repetition_plan = resp.get("spaced_repetition_plan")
    mastery_checkpoints = resp.get("mastery_checkpoints")
    practice_missions = resp.get("practice_missions")
    mistake_fingerprint = resp.get("mistake_fingerprint")
    adaptive_race_strategy = resp.get("adaptive_race_strategy")
    personalization_score = float(resp.get("personalization_score") or 0)
    ai_confidence_score = float(resp.get("ai_confidence_score") or 0)
    passage_novelty_score = float(resp.get("passage_novelty_score") or 0)
    weakspot_coverage_score = float(resp.get("weakspot_coverage_score") or 0)
    ai_evidence_trail = resp.get("ai_evidence_trail")
    generated_passage_audit = resp.get("generated_passage_audit")
    training_pack_signature = str(resp.get("training_pack_signature") or "").strip()
    ghost_target_wpm = float(resp.get("ghost_target_wpm") or 0)
    ghost_target_accuracy = float(resp.get("ghost_target_accuracy") or 0)
    ghost_reward_badge = str(resp.get("ghost_reward_badge") or "").strip()
    problem_key_story_title = str(resp.get("problem_key_story_title") or "").strip()
    problem_key_story_topic = str(resp.get("problem_key_story_topic") or "").strip()
    problem_key_story_keys = resp.get("problem_key_story_keys")
    problem_key_story_passage = str(resp.get("problem_key_story_passage") or "").strip()
    assert_true(isinstance(adaptive_micro_lessons, list) and len(adaptive_micro_lessons) >= 3,
                f"[{label}] adaptive_micro_lessons should have >=3 items: {resp}")
    assert_true(isinstance(mistake_heatmap, list) and len(mistake_heatmap) >= 1,
                f"[{label}] mistake_heatmap missing: {resp}")
    assert_true(isinstance(next_session_checklist, list) and len(next_session_checklist) >= 3,
                f"[{label}] next_session_checklist should have >=3 items: {resp}")
    assert_true(isinstance(ghost_race_plan, list) and len(ghost_race_plan) >= 3,
                f"[{label}] ghost_race_plan should have >=3 items: {resp}")
    assert_true(isinstance(finger_diagnostics, list) and len(finger_diagnostics) >= 1,
                f"[{label}] finger_diagnostics missing: {resp}")
    assert_true(isinstance(progress_prediction, list) and len(progress_prediction) >= 3,
                f"[{label}] progress_prediction should have >=3 items: {resp}")
    assert_true(isinstance(lesson_ladder, list) and len(lesson_ladder) >= 3,
                f"[{label}] lesson_ladder should have >=3 items: {resp}")
    assert_true(isinstance(attempt_replay_cues, list) and len(attempt_replay_cues) >= 3,
                f"[{label}] attempt_replay_cues should have >=3 items: {resp}")
    assert_true(isinstance(weak_key_drills, list) and len(weak_key_drills) >= 2,
                f"[{label}] weak_key_drills should have >=2 items: {resp}")
    assert_true(isinstance(ngram_drills, list) and len(ngram_drills) >= 2,
                f"[{label}] ngram_drills should have >=2 items: {resp}")
    assert_true(isinstance(spaced_repetition_plan, list) and len(spaced_repetition_plan) >= 3,
                f"[{label}] spaced_repetition_plan should have >=3 items: {resp}")
    assert_true(isinstance(mastery_checkpoints, list) and len(mastery_checkpoints) >= 3,
                f"[{label}] mastery_checkpoints should have >=3 items: {resp}")
    assert_true(isinstance(practice_missions, list) and len(practice_missions) >= 3,
                f"[{label}] practice_missions should have >=3 items: {resp}")
    assert_true(isinstance(mistake_fingerprint, list) and len(mistake_fingerprint) >= 3,
                f"[{label}] mistake_fingerprint should have >=3 items: {resp}")
    assert_true(isinstance(adaptive_race_strategy, list) and len(adaptive_race_strategy) >= 3,
                f"[{label}] adaptive_race_strategy should have >=3 items: {resp}")
    assert_true(0 <= personalization_score <= 100,
                f"[{label}] personalization_score invalid: {resp}")
    assert_true(0 <= ai_confidence_score <= 100 and ai_confidence_score > 0,
                f"[{label}] ai_confidence_score invalid: {resp}")
    assert_true(0 <= passage_novelty_score <= 100 and passage_novelty_score > 0,
                f"[{label}] passage_novelty_score invalid: {resp}")
    assert_true(0 <= weakspot_coverage_score <= 100 and weakspot_coverage_score > 0,
                f"[{label}] weakspot_coverage_score invalid: {resp}")
    assert_true(isinstance(ai_evidence_trail, list) and len(ai_evidence_trail) >= 3,
                f"[{label}] ai_evidence_trail should have >=3 items: {resp}")
    assert_true(isinstance(generated_passage_audit, list) and len(generated_passage_audit) >= 3,
                f"[{label}] generated_passage_audit should have >=3 items: {resp}")
    assert_true(len(problem_key_story_title) >= 8,
                f"[{label}] problem_key_story_title missing: {resp}")
    assert_true(len(problem_key_story_topic) >= 8,
                f"[{label}] problem_key_story_topic missing: {resp}")
    assert_true(isinstance(problem_key_story_keys, list) and len(problem_key_story_keys) >= 2,
                f"[{label}] problem_key_story_keys should have >=2 items: {resp}")
    assert_true(len(problem_key_story_passage) >= 60,
                f"[{label}] problem_key_story_passage too short: {resp}")
    assert_true(len(training_pack_signature) >= 8,
                f"[{label}] training_pack_signature missing: {resp}")
    for idx, mission in enumerate(practice_missions[:3]):
        assert_true(isinstance(mission, dict), f"[{label}] practice_missions[{idx}] not object: {resp}")
        assert_true(len(str(mission.get("title") or "").strip()) >= 6,
                    f"[{label}] mission title missing at {idx}: {resp}")
        assert_true(len(str(mission.get("objective") or "").strip()) >= 12,
                    f"[{label}] mission objective missing at {idx}: {resp}")
        assert_true(len(str(mission.get("passage") or "").strip()) >= 40,
                    f"[{label}] mission passage too short at {idx}: {resp}")
        assert_true(30 <= int(mission.get("duration_seconds") or 0) <= 600,
                    f"[{label}] mission duration invalid at {idx}: {resp}")
        assert_true(70 <= float(mission.get("target_accuracy") or 0) <= 99.9,
                    f"[{label}] mission target_accuracy invalid at {idx}: {resp}")
        assert_true(10 <= float(mission.get("target_wpm") or 0) <= 250,
                    f"[{label}] mission target_wpm invalid at {idx}: {resp}")
    assert_true(10 <= ghost_target_wpm <= 250,
                f"[{label}] ghost_target_wpm invalid: {resp}")
    assert_true(70 <= ghost_target_accuracy <= 99.9,
                f"[{label}] ghost_target_accuracy invalid: {resp}")
    assert_true(len(ghost_reward_badge) >= 5,
                f"[{label}] ghost_reward_badge missing: {resp}")

    race_raw = resp.get("race_id")
    user_raw = resp.get("user_id")
    race_id = int(race_raw) if race_raw is not None else -1
    user_id = int(user_raw) if user_raw is not None else -1
    assert_true(race_id == expected_race_id, f"[{label}] race_id mismatch expected={expected_race_id}, got={resp}")
    assert_true(user_id == expected_user_id, f"[{label}] user_id mismatch expected={expected_user_id}, got={resp}")

    if resp.get("error_message") is not None:
        assert_true(isinstance(resp.get("error_message"), str),
                    f"[{label}] error_message type invalid: {resp}")


def run_case(name: str, fn):
    start = time.perf_counter()
    try:
        detail = fn()
        elapsed = (time.perf_counter() - start) * 1000
        return CaseResult(name=name, passed=True, detail=detail or "ok", elapsed_ms=elapsed)
    except Exception as ex:
        elapsed = (time.perf_counter() - start) * 1000
        tb = traceback.format_exc(limit=6)
        return CaseResult(name=name, passed=False, detail=f"{type(ex).__name__}: {ex}\n{tb}", elapsed_ms=elapsed)


def case_heartbeat(host: str, port: int, timeout: float) -> str:
    c = ProtoClient(host, port, timeout, "heartbeat")
    c.connect()
    try:
        # After login, heartbeat ping/pong should work.
        u = rand_user("heartbeat")
        pw = "Passw0rd_123"
        register_ok(c, u, pw)
        login_ok(c, u, pw)

        c.send_empty(HEARTBEAT_PING)
        c.wait_type(HEARTBEAT_PONG, timeout=timeout)
        return "heartbeat pong received on authenticated session"
    finally:
        c.close()


def case_unauthorized_before_login(host: str, port: int, timeout: float) -> str:
    c = ProtoClient(host, port, timeout, "unauth")
    c.connect()
    try:
        c.send(ROOM_LIST_REQUEST, {})
        expect_error(c, {INVALID_SESSION})

        c.send(CREATE_ROOM, {})
        expect_error(c, {INVALID_SESSION})
        return "protected actions blocked before login"
    finally:
        c.close()


def case_invalid_auth_payloads(host: str, port: int, timeout: float) -> str:
    c = ProtoClient(host, port, timeout, "invalid-auth")
    c.connect()
    try:
        # empty body should trigger invalid message
        c.send_empty(REGISTER_REQUEST)
        expect_error(c, {INVALID_MESSAGE})

        c.send_empty(LOGIN_REQUEST)
        expect_error(c, {INVALID_MESSAGE})

        # missing/blank fields should fail gracefully (REGISTER_RESPONSE/LOGIN_RESPONSE)
        c.send(REGISTER_REQUEST, {})
        rp = c.wait_type(REGISTER_RESPONSE)
        assert_true(isinstance(rp, dict) and rp.get("success") is False,
                    f"expected register fail for empty payload, got {rp}")

        c.send(REGISTER_REQUEST, {"username": "", "password": ""})
        rp2 = c.wait_type(REGISTER_RESPONSE)
        assert_true(isinstance(rp2, dict) and rp2.get("success") is False,
                    f"expected register fail for blank fields, got {rp2}")

        c.send(LOGIN_REQUEST, {})
        lp = c.wait_type(LOGIN_RESPONSE)
        assert_true(isinstance(lp, dict) and lp.get("success") is False,
                    f"expected login fail for empty payload, got {lp}")
        return "invalid auth payloads handled"
    finally:
        c.close()


def case_duplicate_login_kicks_old_session(host: str, port: int, timeout: float) -> str:
    user = rand_user("duplogin")
    pw = "Passw0rd_123"

    c1 = ProtoClient(host, port, timeout, "duplogin-c1")
    c2 = ProtoClient(host, port, timeout, "duplogin-c2")
    c1.connect(); c2.connect()
    try:
        register_ok(c1, user, pw)
        login_ok(c1, user, pw)

        login_ok(c2, user, pw)

        # old session should be invalidated: either receives ERROR immediately or on next protected action
        got_invalidation = False

        try:
            p = c1.wait_type(ERROR, timeout=1.5, allow_error=True)
            if isinstance(p, dict) and int(p.get("code", -1)) == INVALID_SESSION:
                got_invalidation = True
        except Exception:
            pass

        if not got_invalidation:
            try:
                c1.send(ROOM_LIST_REQUEST, {})
                p = c1.wait_type(ERROR, timeout=2.0, allow_error=True)
                if isinstance(p, dict) and int(p.get("code", -1)) == INVALID_SESSION:
                    got_invalidation = True
            except (ConnectionError, OSError, TimeoutError):
                # socket closure is also acceptable invalidation behavior
                got_invalidation = True

        assert_true(got_invalidation, "old session was not invalidated after duplicate login")

        # new session must work
        c2.send(ROOM_LIST_REQUEST, {})
        room_list = c2.wait_type(ROOM_LIST_RESPONSE)
        assert_true(isinstance(room_list, dict), f"new session not functional: {room_list}")
        return "old session invalidated, new session active"
    finally:
        c1.close(); c2.close()


def case_chat_truncation(host: str, port: int, timeout: float) -> str:
    h = ProtoClient(host, port, timeout, "chat-host")
    g = ProtoClient(host, port, timeout, "chat-guest")
    h.connect(); g.connect()
    try:
        h_user = rand_user("chatH")
        g_user = rand_user("chatG")
        pw = "Passw0rd_123"

        register_ok(h, h_user, pw); login_ok(h, h_user, pw)
        register_ok(g, g_user, pw); login_ok(g, g_user, pw)

        h.send(CREATE_ROOM, {})
        create = h.wait_type(CREATE_ROOM_RESP)
        assert_true(isinstance(create, dict) and create.get("success") is True,
                    f"create room failed: {create}")
        room_code = str(create.get("room_code", ""))

        g.send(JOIN_ROOM, {"room_code": room_code})
        join = g.wait_type(JOIN_ROOM_RESP)
        assert_true(isinstance(join, dict) and join.get("success") is True,
                    f"join failed: {join}")

        original = "X" * 700
        expected = original[:500]

        h.send(CHAT_SEND, {"room_code": room_code, "content": original})
        hp = h.wait_type(CHAT_BROADCAST, timeout=max(4.0, timeout))
        gp = g.wait_type(CHAT_BROADCAST, timeout=max(4.0, timeout))

        for payload, name in [(hp, "host"), (gp, "guest")]:
            assert_true(isinstance(payload, dict), f"chat payload invalid for {name}: {payload}")
            msg = payload.get("message") if isinstance(payload, dict) else None
            assert_true(isinstance(msg, dict), f"chat message missing for {name}: {payload}")
            content = str(msg.get("content", ""))
            assert_true(len(content) == 500,
                        f"chat length expected 500 for {name}, got {len(content)}")
            assert_true(content == expected,
                        f"chat truncation mismatch for {name}")
        return "chat truncates to 500 chars and broadcasts consistently"
    finally:
        h.close(); g.close()


def case_logout_then_protected(host: str, port: int, timeout: float) -> str:
    c = ProtoClient(host, port, timeout, "logout")
    c.connect()
    try:
        u = rand_user("logout")
        pw = "Passw0rd_123"
        register_ok(c, u, pw)
        login_ok(c, u, pw)

        c.send_empty(LOGOUT)
        # server may not send direct logout response
        time.sleep(0.2)

        c.send(ROOM_LIST_REQUEST, {})
        expect_error(c, {INVALID_SESSION})
        return "logout invalidates session"
    finally:
        c.close()


def case_stats_bounds(host: str, port: int, timeout: float) -> str:
    c = ProtoClient(host, port, timeout, "stats-bounds")
    c.connect()
    try:
        u = rand_user("stats")
        pw = "Passw0rd_123"
        register_ok(c, u, pw)
        login_ok(c, u, pw)

        c.send(GET_LEADERBOARD, {"top": -50, "sort_by": "???"})
        lb1 = c.wait_type(LEADERBOARD_RESP)
        assert_true(isinstance(lb1, dict) and isinstance(lb1.get("entries", []), list),
                    f"invalid leaderboard payload (top<0): {lb1}")

        c.send(GET_LEADERBOARD, {"top": 10000, "sort_by": "total_wins"})
        lb2 = c.wait_type(LEADERBOARD_RESP)
        assert_true(isinstance(lb2, dict) and isinstance(lb2.get("entries", []), list),
                    f"invalid leaderboard payload (top high): {lb2}")
        assert_true(len(lb2.get("entries", [])) <= 100,
                    f"leaderboard top clamp failed, got {len(lb2.get('entries', []))}")

        c.send(GET_MATCH_HISTORY, {"limit": -10})
        mh1 = c.wait_type(MATCH_HISTORY_RESP)
        assert_true(isinstance(mh1, dict) and isinstance(mh1.get("matches", []), list),
                    f"invalid match history payload (limit<0): {mh1}")

        c.send(GET_MATCH_HISTORY, {"limit": 10000})
        mh2 = c.wait_type(MATCH_HISTORY_RESP)
        assert_true(isinstance(mh2, dict) and isinstance(mh2.get("matches", []), list),
                    f"invalid match history payload (limit high): {mh2}")
        assert_true(len(mh2.get("matches", [])) <= 100,
                    f"match history limit clamp failed, got {len(mh2.get('matches', []))}")
        return "stats endpoints clamp bounds and return valid payload"
    finally:
        c.close()


def case_malformed_flow_without_room(host: str, port: int, timeout: float) -> str:
    c = ProtoClient(host, port, timeout, "malformed-flow")
    c.connect()
    try:
        u = rand_user("flow")
        pw = "Passw0rd_123"
        register_ok(c, u, pw)
        login_ok(c, u, pw)

        # JOIN non-existing room should fail gracefully
        c.send(JOIN_ROOM, {"room_code": "AAAAAA"})
        jp = c.wait_type(JOIN_ROOM_RESP)
        assert_true(isinstance(jp, dict) and jp.get("success") is False,
                    f"join missing room should fail: {jp}")

        # CREATE room while already in none should succeed; then creating another should fail
        c.send(CREATE_ROOM, {})
        cp1 = c.wait_type(CREATE_ROOM_RESP)
        assert_true(isinstance(cp1, dict) and cp1.get("success") is True,
                    f"first create should succeed: {cp1}")

        c.send(CREATE_ROOM, {})
        cp2 = c.wait_type(CREATE_ROOM_RESP)
        assert_true(isinstance(cp2, dict) and cp2.get("success") is False,
                    f"second create should fail while already in room: {cp2}")

        # Leave room to test race actions without room
        c.send(LEAVE_ROOM, {"room_code": str(cp1.get('room_code') or '')})
        time.sleep(0.2)

        # Expect explicit error when starting race without room
        c.send_empty(RACE_START)
        try:
            ep = c.wait_type(ERROR, timeout=1.8, allow_error=True)
            assert_true(isinstance(ep, dict), f"expected error payload for RACE_START without room, got {ep}")
        except TimeoutError as ex:
            raise AssertionError(
                "RACE_START without room returned no response (expected explicit ERROR NotInRoom)"
            ) from ex

        # Expect explicit error when submitting typing update without room
        c.send(TYPING_UPDATE, {
            "room_code": "NOROOM",
            "current_position": 1,
            "correct_chars": 1,
            "wrong_chars": 0,
            "timestamp": int(time.time() * 1000),
        })
        try:
            ep2 = c.wait_type(ERROR, timeout=1.8, allow_error=True)
            assert_true(isinstance(ep2, dict), f"expected error payload for TYPING_UPDATE without room, got {ep2}")
        except TimeoutError as ex:
            raise AssertionError(
                "TYPING_UPDATE without room returned no response (expected explicit ERROR)"
            ) from ex

        # Expect explicit error when finishing race without room
        c.send(RACE_FINISH, {
            "room_code": "NOROOM",
            "correct_chars": 10,
            "wrong_chars": 1,
            "time_taken_ms": 1000,
        })
        try:
            ep3 = c.wait_type(ERROR, timeout=1.8, allow_error=True)
            assert_true(isinstance(ep3, dict), f"expected error payload for RACE_FINISH without room, got {ep3}")
        except TimeoutError as ex:
            raise AssertionError(
                "RACE_FINISH without room returned no response (expected explicit ERROR)"
            ) from ex

        return "malformed flows rejected with explicit errors"
    finally:
        c.close()


def case_room_language_matrix_and_practice_protocol(host: str, port: int, timeout: float) -> str:
    h = ProtoClient(host, port, timeout, "lang-host")
    g = ProtoClient(host, port, timeout, "lang-guest")
    h.connect(); g.connect()
    try:
        h_user = rand_user("langH")
        g_user = rand_user("langG")
        pw = "Passw0rd_123"
        register_ok(h, h_user, pw); login_ok(h, h_user, pw)
        register_ok(g, g_user, pw); login_ok(g, g_user, pw)

        language_matrix = [
            ("en", "en"),
            ("any", "any"),
            ("INVALID_LANGUAGE_CODE", "en"),
            ("vi", "vi"),
        ]

        for requested_language, expected_room_language in language_matrix:
            h.send(CREATE_ROOM, {"passage_language": requested_language})
            create = h.wait_type(CREATE_ROOM_RESP)
            assert_true(isinstance(create, dict) and create.get("success") is True,
                        f"create room failed for language={requested_language}: {create}")

            room_code = str(create.get("room_code", ""))
            room_payload = create.get("room") if isinstance(create, dict) else None
            actual_room_language = normalize_language(room_payload.get("passage_language") if isinstance(room_payload, dict) else None)
            assert_true(actual_room_language == expected_room_language,
                        f"create room language mismatch request={requested_language} expected={expected_room_language} got={create}")

            g.send(JOIN_ROOM, {"room_code": room_code})
            join = g.wait_type(JOIN_ROOM_RESP)
            assert_true(isinstance(join, dict) and join.get("success") is True,
                        f"join failed language={requested_language}: {join}")

            join_room = join.get("room") if isinstance(join, dict) else None
            actual_join_language = normalize_language(join_room.get("passage_language") if isinstance(join_room, dict) else None)
            assert_true(actual_join_language == expected_room_language,
                        f"join room language mismatch request={requested_language} expected={expected_room_language} got={join}")

            h_start, g_start = start_race_and_wait(h, g, g_user, room_code, timeout)
            for payload, who in [(h_start, "host"), (g_start, "guest")]:
                race_room_code = str(payload.get("room_code", ""))
                race_language = normalize_language(str(payload.get("passage_language") or "en"))
                passage_text = str(payload.get("passage_text", ""))
                passage_id = int(payload.get("passage_id") or 0)

                assert_true(race_room_code == room_code,
                            f"{who} race_start room_code mismatch language={requested_language}: {payload}")
                assert_true(passage_id > 0, f"{who} race_start passage_id invalid: {payload}")
                assert_true(len(passage_text) > 0, f"{who} race_start passage_text empty: {payload}")
                if expected_room_language == "any":
                    assert_true(race_language in ("vi", "en"),
                                f"{who} race language for any must be en/vi, got={payload}")
                else:
                    assert_true(race_language == expected_room_language,
                                f"{who} race language mismatch expected={expected_room_language}, got={payload}")

            host_text = str(h_start.get("passage_text", ""))
            if expected_room_language == "vi":
                assert_true(has_vietnamese_diacritic(host_text),
                            f"vi passage must contain Vietnamese diacritic chars, got={host_text!r}")

            h_result, _ = finish_race_pair(h, g, room_code, host_text, timeout)
            assert_true(isinstance(h_result.get("results", []), list) and len(h_result.get("results", [])) >= 2,
                        f"race result invalid for language={requested_language}: {h_result}")

            g.send(LEAVE_ROOM, {"room_code": room_code})
            h.send(LEAVE_ROOM, {"room_code": room_code})
            time.sleep(0.2)

        return "room language vi/en/any/invalid works, race_start carries practice fields, vi passage has diacritics"
    finally:
        h.close(); g.close()


def case_ai_coach_auth_and_shape(host: str, port: int, timeout: float) -> str:
    unauth = ProtoClient(host, port, timeout, "ai-unauth")
    auth = ProtoClient(host, port, timeout, "ai-auth")
    unauth.connect()
    auth.connect()
    try:
        # unauthorized call should be blocked
        unauth.send(GET_AI_COACH, {"room_code": "AAAAAA", "race_id": 1})
        expect_error(unauth, {INVALID_SESSION})

        # authenticated call should return structured response
        user = rand_user("ai")
        pw = "Passw0rd_123"
        register_ok(auth, user, pw)
        login_resp = login_ok(auth, user, pw)
        auth_user = login_resp.get("user") if isinstance(login_resp, dict) else None
        auth_user_id = int(auth_user.get("id") or 0) if isinstance(auth_user, dict) else 0
        assert_true(auth_user_id > 0, f"[ai-auth] missing user.id in login response: {login_resp}")

        request_matrix = [
            (
                "vi-normal",
                {
                    "room_code": "DUMMY1",
                    "race_id": 17,
                    "user_id": auth_user_id,
                    "username": user,
                    "position": 2,
                    "total_players": 4,
                    "wpm": 48.5,
                    "accuracy": 93.2,
                    "chars_correct": 210,
                    "chars_wrong": 12,
                    "time_taken_ms": 42000,
                    "is_completed": True,
                    "language": "vi",
                },
                17,
                auth_user_id,
            ),
            (
                "en-clamp-negative",
                {
                    "room_code": " dummy2 ",
                    "race_id": -9,
                    "user_id": 0,
                    "username": user,
                    "position": 0,
                    "total_players": 0,
                    "wpm": -1,
                    "accuracy": -20,
                    "chars_correct": -99,
                    "chars_wrong": -1,
                    "time_taken_ms": -300,
                    "is_completed": False,
                    "language": "en",
                },
                0,
                auth_user_id,
            ),
            (
                "any-clamp-high",
                {
                    "room_code": "RoomX",
                    "race_id": 999,
                    "user_id": auth_user_id,
                    "username": user,
                    "position": 9,
                    "total_players": 1,
                    "wpm": 99999,
                    "accuracy": 888,
                    "chars_correct": 99999,
                    "chars_wrong": 5000,
                    "time_taken_ms": 1,
                    "is_completed": True,
                    "language": "any",
                },
                999,
                auth_user_id,
            ),
        ]

        for label, request_payload, expected_race_id, expected_user_id in request_matrix:
            auth.send(GET_AI_COACH, request_payload)
            resp = auth.wait_type(AI_COACH_RESPONSE, timeout=max(4.0, timeout))
            assert_ai_payload_rich(resp, expected_race_id, expected_user_id, label=label)

        return "AI coach blocks unauthenticated requests and returns rich payload for vi/en/any + boundary inputs"
    finally:
        unauth.close()
        auth.close()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="TypeRacer edge matrix protocol test")
    parser.add_argument("--host", default="134.209.108.82")
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--timeout", type=float, default=60.0)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    print(f"Target: {args.host}:{args.port} timeout={args.timeout}s")

    cases = [
        ("heartbeat_ping_pong", case_heartbeat),
        ("unauthorized_before_login", case_unauthorized_before_login),
        ("invalid_auth_payloads", case_invalid_auth_payloads),
        ("duplicate_login_invalidates_old_session", case_duplicate_login_kicks_old_session),
        ("chat_truncation_and_broadcast", case_chat_truncation),
        ("logout_then_protected_action", case_logout_then_protected),
        ("stats_request_bounds", case_stats_bounds),
        ("ai_coach_auth_and_shape", case_ai_coach_auth_and_shape),
        ("room_language_matrix_practice_protocol", case_room_language_matrix_and_practice_protocol),
        ("malformed_flow_without_room", case_malformed_flow_without_room),
    ]

    results: list[CaseResult] = []
    for case_name, fn in cases:
        res = run_case(case_name, lambda fn=fn: fn(args.host, args.port, args.timeout))
        results.append(res)
        state = "PASS" if res.passed else "FAIL"
        print(f"- {case_name}: {state} ({res.elapsed_ms:.1f} ms)")
        if not res.passed:
            print(f"  detail: {res.detail.splitlines()[0]}")

    passed = sum(1 for r in results if r.passed)
    failed = len(results) - passed
    print("\n=== EDGE MATRIX SUMMARY ===")
    print(f"passed={passed} failed={failed} total={len(results)}")

    if failed:
        print("\nFirst failing details:")
        bad = next(r for r in results if not r.passed)
        print(f"case={bad.name}")
        print(bad.detail)
        return 1

    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("Interrupted", file=sys.stderr)
        sys.exit(130)
