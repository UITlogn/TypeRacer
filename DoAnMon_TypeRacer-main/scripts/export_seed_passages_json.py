#!/usr/bin/env python3

import json
import re
import sys
from pathlib import Path


def parse_passages(sql_text: str) -> list[str]:
    matches = re.findall(r"N'((?:''|[^'])*)'", sql_text, flags=re.DOTALL)
    passages: list[str] = []
    for raw in matches:
        text = raw.replace("''", "'").strip()
        if text:
            passages.append(text)
    return passages


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    sql_path = root / "database" / "003_seed_passages.sql"
    sql_text = sql_path.read_text(encoding="utf-8")
    passages = parse_passages(sql_text)

    docs = [
        {
            "content": content,
            "source": "database/003_seed_passages.sql",
            "language": "en",
        }
        for content in passages
    ]

    json.dump(docs, sys.stdout, ensure_ascii=False, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
