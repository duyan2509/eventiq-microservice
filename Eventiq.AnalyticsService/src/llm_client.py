"""Groq LLM wrapper — single entry point for all LLM calls in the
pipeline. Handles rate limiting (free tier: 30 RPM, 12k TPM, 1000 RPD)
and exposes a `call()` returning plain text.
"""
from __future__ import annotations

import os
import time
from typing import Iterable

from dotenv import load_dotenv
from groq import Groq, APIStatusError, RateLimitError

load_dotenv()

DEFAULT_MODEL = os.getenv("GROQ_MODEL", "llama-3.3-70b-versatile")
RATE_LIMIT_SLEEP = float(os.getenv("LLM_RATE_LIMIT_SLEEP", "2.5"))

_client: Groq | None = None
_last_call_at: float = 0.0


def _get_client() -> Groq:
    global _client
    if _client is None:
        _client = Groq(api_key=os.environ["GROQ_API_KEY"])
    return _client


def _throttle() -> None:
    """Sleep so consecutive calls stay below the configured RPM."""
    global _last_call_at
    elapsed = time.monotonic() - _last_call_at
    if elapsed < RATE_LIMIT_SLEEP:
        time.sleep(RATE_LIMIT_SLEEP - elapsed)
    _last_call_at = time.monotonic()


def call(
    prompt: str,
    *,
    model: str | None = None,
    max_tokens: int = 800,
    temperature: float = 0.0,
    system: str | None = None,
    stop: Iterable[str] | None = None,
) -> str:
    """Send a single-turn prompt and return the text reply.

    Retries once on 429 (Groq rate limit) after a 30s backoff.
    """
    _throttle()
    messages: list[dict] = []
    if system:
        messages.append({"role": "system", "content": system})
    messages.append({"role": "user", "content": prompt})

    request_kwargs = {
        "model": model or DEFAULT_MODEL,
        "messages": messages,
        "max_tokens": max_tokens,
        "temperature": temperature,
    }
    if stop:
        request_kwargs["stop"] = list(stop)

    try:
        resp = _get_client().chat.completions.create(**request_kwargs)
    except RateLimitError:
        time.sleep(30)
        resp = _get_client().chat.completions.create(**request_kwargs)
    except APIStatusError as e:
        raise RuntimeError(f"Groq API error {e.status_code}: {e.message}") from e

    return (resp.choices[0].message.content or "").strip()


if __name__ == "__main__":
    print(call("Say OK in one word."))
