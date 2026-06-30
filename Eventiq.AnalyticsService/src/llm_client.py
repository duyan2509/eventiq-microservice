"""Groq LLM wrapper — single entry point for all LLM calls in the
pipeline. Handles rate limiting (free tier: 30 RPM, 12k TPM, 1000 RPD)
and exposes `call()` (sync) and `async_call()` (async) returning plain text.
"""
from __future__ import annotations

import asyncio
import os
import time
from typing import Iterable

import httpx
from dotenv import load_dotenv
from groq import AsyncGroq, Groq, APIStatusError, RateLimitError

load_dotenv()

DEFAULT_MODEL = os.getenv("GROQ_MODEL", "llama-3.3-70b-versatile")
# Faster/smaller model for cheap classification tasks (entity extraction).
# Together AI equivalent: meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo
FAST_MODEL = os.getenv("LLM_FAST_MODEL", "llama-3.1-8b-instant")
# Default 0 for live requests. Set LLM_RATE_LIMIT_SLEEP=2.5 when running batch eval
# to stay under Groq free-tier 30 RPM.
RATE_LIMIT_SLEEP = float(os.getenv("LLM_RATE_LIMIT_SLEEP", "0"))
# When set, route through a generic OpenAI-compatible POST (SambaNova, Together,
# Cerebras, ...). The Groq SDK rewrites the request path in a way some providers
# reject, so for non-Groq endpoints we call /chat/completions directly via httpx.
_LLM_BASE_URL = os.getenv("LLM_BASE_URL")

_client: Groq | None = None
_async_client: AsyncGroq | None = None
_last_call_at: float = 0.0


def _get_client() -> Groq:
    global _client
    if _client is None:
        _client = Groq(api_key=os.environ["GROQ_API_KEY"])
    return _client


def _get_async_client() -> AsyncGroq:
    global _async_client
    if _async_client is None:
        _async_client = AsyncGroq(api_key=os.environ["GROQ_API_KEY"])
    return _async_client


def _post_openai(payload: dict) -> str:
    """Direct OpenAI-compatible chat-completions POST for `_LLM_BASE_URL`.

    Retries on 429 with exponential backoff (free tiers like SambaNova have
    tight RPM/TPM windows); since each question is cached on success, a slow
    run still makes durable progress across restarts.
    """
    url = _LLM_BASE_URL.rstrip("/") + "/chat/completions"
    headers = {"Authorization": f"Bearer {os.environ['GROQ_API_KEY']}"}
    delay = 8.0
    last = ""
    for _ in range(7):
        try:
            resp = httpx.post(url, headers=headers, json=payload, timeout=120)
        except httpx.HTTPError as e:               # transient network/timeout
            last = str(e)
            time.sleep(delay)
            delay = min(delay * 2, 60)
            continue
        # Retry on rate limit (429) and transient server errors (5xx).
        if resp.status_code == 429 or resp.status_code >= 500:
            last = f"{resp.status_code}: {resp.text[:200]}"
            time.sleep(delay)
            delay = min(delay * 2, 60)
            continue
        if resp.status_code >= 400:                # real client error — don't retry
            raise RuntimeError(f"LLM API error {resp.status_code}: {resp.text[:300]}")
        return resp.json()["choices"][0]["message"]["content"] or ""
    raise RuntimeError(f"LLM API error: retries exhausted (last: {last})")


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
    response_format: dict | None = None,
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
    if response_format:
        request_kwargs["response_format"] = response_format

    if _LLM_BASE_URL:
        return _post_openai(request_kwargs).strip()

    try:
        resp = _get_client().chat.completions.create(**request_kwargs)
    except RateLimitError:
        time.sleep(30)
        resp = _get_client().chat.completions.create(**request_kwargs)
    except APIStatusError as e:
        raise RuntimeError(f"Groq API error {e.status_code}: {e.message}") from e

    return (resp.choices[0].message.content or "").strip()


async def async_call(
    prompt: str,
    *,
    model: str | None = None,
    max_tokens: int = 800,
    temperature: float = 0.0,
    system: str | None = None,
    stop: Iterable[str] | None = None,
    response_format: dict | None = None,
) -> str:
    """Async variant of `call()` for use inside async generators / endpoints."""
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
    if response_format:
        request_kwargs["response_format"] = response_format

    try:
        resp = await _get_async_client().chat.completions.create(**request_kwargs)
    except RateLimitError:
        await asyncio.sleep(30)
        resp = await _get_async_client().chat.completions.create(**request_kwargs)
    except APIStatusError as e:
        raise RuntimeError(f"Groq API error {e.status_code}: {e.message}") from e

    return (resp.choices[0].message.content or "").strip()


if __name__ == "__main__":
    print(call("Say OK in one word."))
