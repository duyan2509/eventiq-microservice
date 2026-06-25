"""Eval configuration.

Environment variables to set before running:
  GROQ_API_KEY=<your_together_api_key>
  LLM_BASE_URL=https://api.together.xyz/v1
  LLM_RATE_LIMIT_SLEEP=1.0          # 60 RPM → 1 sec between calls
  EVAL_MODEL=Qwen/Qwen2.5-Coder-32B-Instruct
  EVAL_MAX_QUESTIONS=0               # 0 = all 1034
  EVAL_SKIP_ENTITY_LLM=0             # 1 = pure keyword matching (faster, no LLM call 1)
"""
from __future__ import annotations

import os
from pathlib import Path

EVAL_DIR = Path(__file__).parent
DATA_DIR = EVAL_DIR / "data" / "spider"
SPIDER_DEV = DATA_DIR / "dev.json"
SPIDER_TABLES = DATA_DIR / "tables.json"
SPIDER_DATABASES = DATA_DIR / "database"
RESULTS_DIR = EVAL_DIR / "results"
RESULTS_FILE = RESULTS_DIR / "dev_results.jsonl"

MODEL = os.getenv("EVAL_MODEL", "Qwen/Qwen2.5-Coder-32B-Instruct")
CONFIDENCE_THRESHOLD = float(os.getenv("EVAL_CONFIDENCE_THRESHOLD", "0.7"))
MAX_QUESTIONS = int(os.getenv("EVAL_MAX_QUESTIONS", "0"))
SKIP_ENTITY_LLM = os.getenv("EVAL_SKIP_ENTITY_LLM", "0") == "1"
