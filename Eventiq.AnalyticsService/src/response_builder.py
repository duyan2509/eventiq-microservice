"""Stage 8 — response builder.

`generate_title` is a rule-based (no LLM) title derived from the question.
`generate_answer` is the conversational step: it turns the result rows into a
short natural-language answer (used by the chat endpoint, not the chart view).
"""
from __future__ import annotations

from . import llm_client

# Cap rows fed to the answer LLM — a summary doesn't need the full result set,
# and a huge prompt is slow + risks the model inventing detail.
_ANSWER_MAX_ROWS = 30


def generate_title(question: str) -> str:
    """Derive a display title from the question, no LLM call.

    Drops a trailing `?` and uppercases the first character only —
    `str.capitalize()` would lowercase the rest and mangle acronyms.
    """
    text = question.strip().rstrip("?").strip()
    if not text:
        return question.strip()
    return text[0].upper() + text[1:]


def _render_rows(columns: list[str], rows: list[dict]) -> str:
    lines = [" | ".join(columns)]
    for r in rows[:_ANSWER_MAX_ROWS]:
        lines.append(" | ".join(str(r.get(c, "")) for c in columns))
    if len(rows) > _ANSWER_MAX_ROWS:
        lines.append(f"... (+{len(rows) - _ANSWER_MAX_ROWS} more rows)")
    return "\n".join(lines)


def generate_answer(
    question: str,
    columns: list[str],
    rows: list[dict],
    error: str | None = None,
    *,
    max_tokens: int = 400,
) -> str:
    """A short answer grounded in the result rows, in the user's language.

    Returns a canned message for the error / no-rows cases (no LLM call), so the
    model is only asked to summarise when there is actually data to summarise.
    """
    if error:
        return f"Query failed: {error}"
    if not rows:
        return "No data found for this question."

    prompt = (
        "You are a data analysis assistant. Based ONLY on the result table below, "
        "answer the question in the same language as the question, concisely (1-3 sentences), "
        "citing specific numbers. NEVER fabricate numbers not present in the data. "
        "Do NOT mention SQL or databases.\n\n"
        f"Question: {question}\n\n"
        f"Result ({len(rows)} rows):\n{_render_rows(columns, rows)}\n\n"
        "Answer:"
    )
    return llm_client.call(prompt, max_tokens=max_tokens, temperature=0.2).strip()
