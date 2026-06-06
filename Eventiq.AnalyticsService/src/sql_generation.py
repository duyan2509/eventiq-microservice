"""Stage 5 — call the LLM to generate SQL from the assembled prompt.

`clean_sql` strips markdown fences and any trailing chatter from the
model output. `generate_sql` is the public entry point that the
pipeline calls.
"""
from __future__ import annotations

import re

from . import llm_client
from .prompt_builder import build_org_prompt, build_prompt


_FENCE_OPEN = re.compile(r"^\s*```[\w]*\s*\n?", re.MULTILINE)
_FENCE_CLOSE = re.compile(r"\n?\s*```\s*$", re.MULTILINE)


def clean_sql(raw: str) -> str:
    """Strip ```sql fences, leading/trailing whitespace, ensure `;` at end.

    Llama 3.3 sometimes returns the SQL inside a code block despite the
    prompt asking it not to — handle both cases.
    """
    s = raw.strip()
    s = _FENCE_OPEN.sub("", s, count=1)
    s = _FENCE_CLOSE.sub("", s, count=1)
    s = s.strip()
    # Drop any trailing prose after the final `;` (rare, but happens).
    if ";" in s:
        s = s[: s.rindex(";") + 1]
    return s.rstrip(";") + ";"


def generate_sql(
    question: str,
    subgraph: dict,
    schema: dict[str, str],
    *,
    max_tokens: int = 600,
    temperature: float = 0.0,
    org_mode: bool = False,
) -> str:
    """Build the prompt, call the LLM, return cleaned SQL."""
    builder = build_org_prompt if org_mode else build_prompt
    prompt = builder(question, subgraph, schema)
    raw = llm_client.call(
        prompt,
        max_tokens=max_tokens,
        temperature=temperature,
    )
    return clean_sql(raw)
