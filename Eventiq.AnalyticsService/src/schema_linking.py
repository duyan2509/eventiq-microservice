"""Stage 2 of the Text2SQL pipeline — schema linking.

Two strategies, routed by entity-extraction confidence:

* `graph_traversal` (high-confidence): shortest path through the
  NetworkX schema graph between every pair of LLM-picked tables.
  Pulls in intermediate tables and emits JOIN hints with correct
  PascalCase quoting.

* `keyword_matching` (low-confidence fallback): rank all tables by
  word overlap between the question and the DDL of each table, plus a
  bonus when the bare table name appears in the question. Returns the
  top-k without JOIN hints — the LLM has to guess JOINs itself.

`schema_link` is the single entry point used by the pipeline.
"""
from __future__ import annotations

import re
from typing import Iterable

import networkx as nx

from .entity_extraction import CONFIDENCE_THRESHOLD


# ----------------------------------------------------------- helpers
def quote_if_pascal(col: str) -> str:
    """Wrap PascalCase columns in double quotes for SQL.

    Heuristic: a leading uppercase letter implies PascalCase (Postgres
    folds unquoted identifiers to lowercase). snake_case columns stay
    bare.
    """
    return f'"{col}"' if col and col[0].isupper() else col


def _format_join_hint(a: str, b: str, edge_data: dict) -> str:
    """Render `a.<col_a> = b.<col_b>` with correct quoting.

    `edge_data['cols']` is keyed by node name so the direction is
    independent of path traversal order.
    """
    cols = edge_data["cols"]
    col_a = quote_if_pascal(cols[a])
    col_b = quote_if_pascal(cols[b])
    return f"{a}.{col_a} = {b}.{col_b}"


# ----------------------------------------------------------- graph
def graph_traversal(entity_tables: Iterable[str], g: nx.Graph) -> dict:
    """Find paths through `g` connecting every pair of entity tables.

    Returns
    -------
    dict
        - `tables`: union of entity tables and every node on the
          discovered shortest paths (intermediate JOINs included).
        - `join_hints`: deduplicated, ordered list of human-readable
          equality predicates ready for the prompt.
    """
    in_graph = [t for t in entity_tables if t in g.nodes]
    if not in_graph:
        return {"tables": [], "join_hints": []}

    tables: list[str] = list(dict.fromkeys(in_graph))   # preserve order
    # Dedup by edge identity (unordered pair) so mirrored equations
    # like `A.x = B.y` and `B.y = A.x` collapse to one hint.
    seen_edges: set[frozenset[str]] = set()
    hints: list[str] = []

    for i, src in enumerate(in_graph):
        for dst in in_graph[i + 1 :]:
            try:
                path = nx.shortest_path(g, src, dst)
            except nx.NetworkXNoPath:
                continue
            for node in path:
                if node not in tables:
                    tables.append(node)
            for a, b in zip(path, path[1:]):
                edge_key = frozenset((a, b))
                if edge_key in seen_edges:
                    continue
                seen_edges.add(edge_key)
                hints.append(_format_join_hint(a, b, g[a][b]))

    return {"tables": tables, "join_hints": hints}


# ----------------------------------------------------------- keyword
_WORD_RE = re.compile(r"\w+")

# Lightweight Vietnamese → English bridge. DDL is in English, but
# real questions come in Vietnamese, so we expand the question with
# these aliases before scoring. Mirrors the mapping rules already
# baked into the entity-extraction prompt.
VN_KEYWORDS: dict[str, str] = {
    "doanh thu":     "orders revenue paid total_amount",
    "đơn hàng":      "orders",
    "thanh toán":    "orders payment",
    "khách hàng":    "users customer",
    "người dùng":    "users",
    "tổ chức":       "organizations",
    "sự kiện":       "events",
    "suất diễn":     "sessions",
    "phiên":         "sessions",
    "vé":            "tickets",
    "ghế":           "seats",
    "sơ đồ":         "seat_maps",
    "loại vé":       "legends",
    "hạng vé":       "legends",
    "nhân viên":     "members staff",
    "lời mời":       "invitations",
    "bị ban":        "ban_histories banned",
    "bị khóa":       "ban_histories banned",
    "phí":           "platform_fee",
}


def _expand_vn(question: str) -> str:
    """Append English synonyms for known Vietnamese phrases. Matches
    use word boundaries to avoid `ban` lighting up inside `ban đêm`,
    `nghiêng`, etc. Multi-word keys are checked before single-word
    ones so longer phrases win.
    """
    lower = question.lower()
    extras: list[str] = []
    for vn, en in sorted(VN_KEYWORDS.items(), key=lambda kv: -len(kv[0])):
        pattern = r"(?<!\w)" + re.escape(vn) + r"(?!\w)"
        if re.search(pattern, lower):
            extras.append(en)
    return lower + " " + " ".join(extras) if extras else lower


def keyword_matching(question: str, schema: dict[str, str], top_k: int = 6) -> dict:
    """Word-overlap ranking over the DDL of each table.

    Score = |question_words ∩ ddl_words| + 3 if the bare table name
    appears in the question verbatim.

    Used as a fallback when entity extraction confidence is low. The
    question is expanded with `VN_KEYWORDS` so Vietnamese phrases can
    align with the English DDL tokens.
    """
    expanded = _expand_vn(question)
    q_words = set(_WORD_RE.findall(expanded))

    scored: list[tuple[str, int]] = []
    for table, ddl in schema.items():
        ddl_words = set(_WORD_RE.findall(ddl.lower()))
        overlap = len(q_words & ddl_words)
        short = table.split(".", 1)[1].strip('"').lower()
        bonus = 3 if short in expanded else 0
        scored.append((table, overlap + bonus))

    scored.sort(key=lambda kv: kv[1], reverse=True)
    top = [t for t, score in scored[:top_k] if score > 0]
    return {"tables": top, "join_hints": []}


# ----------------------------------------------------------- router
def schema_link(
    question: str,
    entity_result: dict,
    g: nx.Graph,
    schema: dict[str, str],
) -> dict:
    """Route between graph traversal and keyword fallback.

    `entity_result` is the dict produced by
    `entity_extraction.extract_and_normalize`, i.e. it must contain
    `confidence` and `normalized_tables`.

    Returns
    -------
    dict
        - `tables`, `join_hints`: usable downstream.
        - `method`: `"graph"` | `"keyword_fallback"` (logged for
          Phase 3 ablation analysis).
    """
    confidence = float(entity_result.get("confidence", 0.0))
    normalized = entity_result.get("normalized_tables", [])

    if confidence >= CONFIDENCE_THRESHOLD and normalized:
        sub = graph_traversal(normalized, g)
        if sub["tables"]:
            return {**sub, "method": "graph"}

    sub = keyword_matching(question, schema)
    return {**sub, "method": "keyword_fallback"}
