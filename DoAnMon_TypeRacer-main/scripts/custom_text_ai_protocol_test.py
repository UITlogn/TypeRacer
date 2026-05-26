#!/usr/bin/env python3
import argparse
import random
import time

from edge_matrix_test import (
    AI_COACH_RESPONSE,
    CREATE_ROOM,
    CREATE_ROOM_RESP,
    GET_AI_COACH,
    LEAVE_ROOM,
    LOGIN_RESPONSE,
    ProtoClient,
    RACE_FINISH,
    RACE_RESULT,
    RACE_START,
    REGISTER_RESPONSE,
    ROOM_LIST_REQUEST,
    ROOM_LIST_RESPONSE,
    TYPING_UPDATE,
    assert_ai_payload_rich,
    assert_true,
)


def rand_user(prefix: str) -> str:
    return f"{prefix}_{int(time.time())}_{random.randint(1000, 9999)}"


def register_and_login(client: ProtoClient, username: str, password: str) -> dict:
    client.send(102, {"username": username, "password": password})
    register_resp = client.wait_type(REGISTER_RESPONSE)
    assert_true(isinstance(register_resp, dict) and register_resp.get("success") is True,
                f"register failed: {register_resp}")

    client.send(100, {"username": username, "password": password})
    login_resp = client.wait_type(LOGIN_RESPONSE)
    assert_true(isinstance(login_resp, dict) and login_resp.get("success") is True,
                f"login failed: {login_resp}")
    return login_resp


def wait_race_start(client: ProtoClient, timeout: float) -> dict:
    payload = client.wait_for(
        lambda m_type, _payload: m_type == RACE_START,
        timeout=timeout,
        desc="custom-text RACE_START",
    )[1]
    assert_true(isinstance(payload, dict), f"missing RACE_START payload: {payload}")
    return payload


def main() -> int:
    parser = argparse.ArgumentParser(description="TypeRacer custom text + AI training-pack protocol test")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--timeout", type=float, default=25.0)
    parser.add_argument("--require-real-ai", action="store_true",
                        help="Fail if AI_COACH_RESPONSE falls back to local bank.")
    args = parser.parse_args()

    password = "Passw0rd_123"
    username = rand_user("customai")
    custom_text = (
        "Khởi động giáo án AI bằng nhịp gõ chắc, kiểm tra từng dấu tiếng Việt, "
        "rồi tăng tốc vừa phải khi đã giữ được độ chính xác ổn định."
    )
    typed_wrong_then_corrected = "x" + custom_text[1:]

    client = ProtoClient(args.host, args.port, args.timeout, "custom-ai")
    client.connect()
    try:
        login_resp = register_and_login(client, username, password)
        user_id = int((login_resp.get("user") or {}).get("id") or 0)
        assert_true(user_id > 0, f"user id missing from login response: {login_resp}")

        client.send(CREATE_ROOM, {
            "passage_language": "vi",
            "race_duration_seconds": 30,
            "enable_ai_mode": True,
            "game_mode": "accuracy",
            "ai_practice_difficulty": "medium",
            "custom_passage_text": custom_text,
        })
        room_resp = client.wait_type(CREATE_ROOM_RESP)
        assert_true(isinstance(room_resp, dict) and room_resp.get("success") is True,
                    f"create custom room failed: {room_resp}")
        assert_true((room_resp.get("room") or {}).get("has_custom_passage") is True,
                    f"room response did not expose has_custom_passage: {room_resp}")
        room_code = room_resp.get("room_code")
        assert_true(isinstance(room_code, str) and len(room_code) == 6,
                    f"room_code invalid: {room_resp}")

        client.send_empty(ROOM_LIST_REQUEST)
        list_resp = client.wait_type(ROOM_LIST_RESPONSE)
        rooms = (list_resp or {}).get("rooms") or []
        listed = [room for room in rooms if room.get("room_code") == room_code]
        assert_true(listed and listed[0].get("has_custom_passage") is True,
                    f"room list did not expose custom text room: {list_resp}")

        client.send_empty(RACE_START)
        start_payload = wait_race_start(client, max(args.timeout, 12.0))
        assert_true(start_payload.get("passage_text") == custom_text,
                    f"RACE_START did not use custom text: {start_payload}")
        assert_true(start_payload.get("game_mode") == "accuracy",
                    f"RACE_START did not preserve game mode: {start_payload}")

        client.send(TYPING_UPDATE, {
            "room_code": room_code,
            "current_position": 1,
            "correct_chars": 0,
            "wrong_chars": 1,
            "typed_text": typed_wrong_then_corrected[:24],
            "timestamp": int(time.time() * 1000),
        })
        client.send(TYPING_UPDATE, {
            "room_code": room_code,
            "current_position": len(custom_text),
            "correct_chars": len(custom_text),
            "wrong_chars": 0,
            "typed_text": custom_text,
            "timestamp": int(time.time() * 1000),
        })

        client.send(RACE_FINISH, {
            "room_code": room_code,
            "correct_chars": len(custom_text),
            "wrong_chars": 0,
            "time_taken_ms": 1800,
            "typed_text": custom_text,
            "backspace_count": 0,
        })
        result_payload = client.wait_type(RACE_RESULT, timeout=max(args.timeout, 12.0))
        assert_true(isinstance(result_payload, dict), f"missing result payload: {result_payload}")
        race_id = int(result_payload.get("race_id") or 0)
        assert_true(race_id > 0, f"race_id missing in result: {result_payload}")

        client.send(GET_AI_COACH, {
            "room_code": room_code,
            "race_id": race_id,
            "user_id": user_id,
            "username": username,
            "position": 1,
            "total_players": 1,
            "wpm": 48.5,
            "accuracy": 98.0,
            "chars_correct": len(custom_text),
            "chars_wrong": 0,
            "time_taken_ms": 1800,
            "is_completed": True,
            "language": "vi",
            "passage_text": custom_text,
            "typed_text": custom_text,
        })
        ai_resp = client.wait_type(AI_COACH_RESPONSE, timeout=max(args.timeout, 20.0))
        assert_ai_payload_rich(ai_resp, race_id, user_id, "custom_text_ai")
        assert_true(int((ai_resp or {}).get("mistake_sample_count") or 0) >= 1,
                    f"AI did not use volatile mistake memory: {ai_resp}")
        top_ngrams = (ai_resp or {}).get("top_mistyped_ngrams") or []
        assert_true(isinstance(top_ngrams, list) and len(top_ngrams) >= 1,
                    f"AI did not expose observed n-gram mistakes: {ai_resp}")
        missions = (ai_resp or {}).get("practice_missions") or []
        assert_true(isinstance(missions, list) and len(missions) >= 3,
                    f"AI did not expose playable practice missions: {ai_resp}")
        fingerprint = (ai_resp or {}).get("mistake_fingerprint") or []
        strategy = (ai_resp or {}).get("adaptive_race_strategy") or []
        assert_true(isinstance(fingerprint, list) and len(fingerprint) >= 3,
                    f"AI did not expose typing fingerprint: {ai_resp}")
        assert_true(isinstance(strategy, list) and len(strategy) >= 3,
                    f"AI did not expose adaptive race strategy: {ai_resp}")
        evidence = (ai_resp or {}).get("ai_evidence_trail") or []
        passage_audit = (ai_resp or {}).get("generated_passage_audit") or []
        confidence = float((ai_resp or {}).get("ai_confidence_score") or 0)
        novelty = float((ai_resp or {}).get("passage_novelty_score") or 0)
        coverage = float((ai_resp or {}).get("weakspot_coverage_score") or 0)
        story_title = str((ai_resp or {}).get("problem_key_story_title") or "").strip()
        story_keys = (ai_resp or {}).get("problem_key_story_keys") or []
        story_passage = str((ai_resp or {}).get("problem_key_story_passage") or "").strip()
        assert_true(isinstance(evidence, list) and len(evidence) >= 3,
                    f"AI did not expose evidence trail: {ai_resp}")
        assert_true(isinstance(passage_audit, list) and len(passage_audit) >= 3,
                    f"AI did not expose generated passage audit: {ai_resp}")
        assert_true(len(story_title) >= 8,
                    f"AI did not expose TypeAI problem-key story title: {ai_resp}")
        assert_true(isinstance(story_keys, list) and len(story_keys) >= 2,
                    f"AI did not expose TypeAI problem-key story keys: {ai_resp}")
        assert_true(len(story_passage) >= 60,
                    f"AI did not expose TypeAI problem-key story passage: {ai_resp}")
        assert_true(0 <= confidence <= 100 and confidence > 0,
                    f"AI confidence score invalid: {ai_resp}")
        assert_true(0 <= novelty <= 100 and novelty > 0,
                    f"AI passage novelty score invalid: {ai_resp}")
        assert_true(0 <= coverage <= 100 and coverage > 0,
                    f"AI weakspot coverage score invalid: {ai_resp}")
        if args.require_real_ai:
            assert_true(ai_resp.get("is_fallback") is False,
                        f"Expected real AI response, got fallback: {ai_resp}")
        print(
            "AI_COACH ok: "
            f"provider={ai_resp.get('provider')} "
            f"model={ai_resp.get('model')} "
            f"is_fallback={ai_resp.get('is_fallback')} "
            f"passages={ai_resp.get('generated_passage_count')} "
            f"heatmap={len(ai_resp.get('mistake_heatmap') or [])} "
            f"ghost_wpm={ai_resp.get('ghost_target_wpm')} "
            f"ghost_plan={len(ai_resp.get('ghost_race_plan') or [])} "
            f"finger={len(ai_resp.get('finger_diagnostics') or [])} "
            f"prediction={len(ai_resp.get('progress_prediction') or [])} "
            f"ladder={len(ai_resp.get('lesson_ladder') or [])} "
            f"weak_keys={len(ai_resp.get('weak_key_drills') or [])} "
            f"ngrams={len(ai_resp.get('ngram_drills') or [])} "
            f"top_ngrams={len(ai_resp.get('top_mistyped_ngrams') or [])} "
            f"spaced={len(ai_resp.get('spaced_repetition_plan') or [])} "
            f"mastery={len(ai_resp.get('mastery_checkpoints') or [])} "
            f"missions={len(missions)} "
            f"story_keys={len(story_keys)} "
            f"fingerprint={len(fingerprint)} "
            f"strategy={len(strategy)} "
            f"personalization={ai_resp.get('personalization_score')} "
            f"confidence={confidence} "
            f"novelty={novelty} "
            f"coverage={coverage} "
            f"evidence={len(evidence)} "
            f"passage_audit={len(passage_audit)} "
            f"pack={ai_resp.get('training_pack_signature')}"
        )

        client.send(LEAVE_ROOM, {"room_code": room_code})
    finally:
        client.close()

    print("CUSTOM TEXT + AI TRAINING PACK TEST PASSED")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
