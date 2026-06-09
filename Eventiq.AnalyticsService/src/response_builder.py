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
        lines.append(f"... (+{len(rows) - _ANSWER_MAX_ROWS} dòng nữa)")
    return "\n".join(lines)


def generate_answer(
    question: str,
    columns: list[str],
    rows: list[dict],
    error: str | None = None,
    *,
    max_tokens: int = 400,
) -> str:
    """A short Vietnamese answer grounded in the result rows.

    Returns a canned message for the error / no-rows cases (no LLM call), so the
    model is only asked to summarise when there is actually data to summarise.
    """
    if error:
        return f"Truy vấn gặp lỗi nên chưa trả lời được: {error}"
    if not rows:
        return "Không tìm thấy dữ liệu phù hợp với câu hỏi."

    prompt = (
        "Bạn là trợ lý phân tích dữ liệu. Dựa CHỈ trên bảng kết quả dưới đây, "
        "trả lời câu hỏi bằng tiếng Việt, ngắn gọn (1-3 câu), nêu số liệu cụ thể. "
        "TUYỆT ĐỐI không bịa số liệu không có trong dữ liệu. Không nhắc đến SQL hay cơ sở dữ liệu.\n\n"
        f"Câu hỏi: {question}\n\n"
        f"Kết quả ({len(rows)} dòng):\n{_render_rows(columns, rows)}\n\n"
        "Trả lời:"
    )
    return llm_client.call(prompt, max_tokens=max_tokens, temperature=0.2).strip()
