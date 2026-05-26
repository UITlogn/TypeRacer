# Edge Matrix Report (Task A)

Date: 2026-05-25
Target: `134.209.108.82:5000`
Script: [`scripts/edge_matrix_test.py`](/root/UIT/NT106/DoAnMon_TypeRacer/scripts/edge_matrix_test.py)

## Run command

```bash
python3 scripts/edge_matrix_test.py --host 134.209.108.82 --port 5000 --timeout 60
```

## Summary (latest rerun after fixes)

- Passed: `9`
- Failed: `0`
- Total cases: `9`

## Case results

1. `heartbeat_ping_pong`: PASS
2. `unauthorized_before_login`: PASS
3. `invalid_auth_payloads`: PASS
4. `duplicate_login_invalidates_old_session`: PASS
5. `chat_truncation_and_broadcast`: PASS
6. `logout_then_protected_action`: PASS
7. `stats_request_bounds`: PASS
8. `ai_coach_auth_and_shape`: PASS
9. `malformed_flow_without_room`: PASS

## Resolution note

The previously failing case (`malformed_flow_without_room`) is now fixed:
- `RACE_START`, `TYPING_UPDATE`, `RACE_FINISH` sent outside room now return explicit `ERROR` instead of silent timeout.
- `HEARTBEAT_PING` pre-login is also handled correctly and returns `HEARTBEAT_PONG`.

## Notes

- `HEARTBEAT_PING/PONG` is verified on authenticated session.
- Pre-login protected actions are blocked with `InvalidSession` as expected.
- Duplicate login behavior works: old session is invalidated, new session remains active.
- Chat message truncation to 500 chars is consistent on host + guest broadcasts.
- AI coach response shape is verified (`coach_text`, `tips`, `action_plan`, `training_title`, `daily_challenge_*`, `practice_words`, `mistake_heatmap`, `adaptive_micro_lessons`, `ghost_race_plan`, `finger_diagnostics`, `progress_prediction`, `lesson_ladder`, `attempt_replay_cues`, `weak_key_drills`, `ngram_drills`, `spaced_repetition_plan`, `mastery_checkpoints`, `ai_confidence_score`, `passage_novelty_score`, `weakspot_coverage_score`, `ai_evidence_trail`, `generated_passage_audit`, `recommended_game_mode`, `recommended_difficulty`, `recommended_target_rpm`, `provider`, `model`, `is_fallback`).
