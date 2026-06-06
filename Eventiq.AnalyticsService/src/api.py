"""FastAPI HTTP wrapper for the Text2SQL pipeline.

Exposes:
  POST /api/analytics/query  { question }  -> result + sql + chart type
  GET  /health
"""
from __future__ import annotations

import logging
from contextlib import asynccontextmanager
from typing import Any

from fastapi import FastAPI, Header, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

from . import auth
from .org_scope import build_org_graph
from .pipeline import run_pipeline, run_pipeline_org
from .response_builder import generate_answer
from .schema_dump import SCHEMA
from .schema_graph import build_graph_from_db

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("analytics-api")

# Built once at startup; cheap to traverse per request.
_state: dict[str, Any] = {"graph": None}


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("Building schema graph...")
    _state["graph"] = build_graph_from_db()
    _state["org_graph"] = build_org_graph()
    logger.info("Ready. %d tables loaded.", len(SCHEMA))
    yield


app = FastAPI(title="Eventiq Analytics", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173", "http://localhost:5001"],
    allow_methods=["GET", "POST"],
    allow_headers=["*"],
)


class QueryRequest(BaseModel):
    question: str = Field(..., min_length=2, max_length=500)


class QueryResponse(BaseModel):
    question: str
    title: str
    sql: str
    rows: list[dict[str, Any]]
    columns: list[str]
    chartType: str
    chartConfig: dict[str, Any]
    relevantTables: list[str]
    method: str
    retries: int
    error: str | None = None
    answer: str | None = None   # natural-language answer (chat endpoint only)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "graphLoaded": "yes" if _state["graph"] is not None else "no"}


def _run_for_principal(principal: dict, question: str) -> dict:
    """Run the right pipeline for the caller's role. Raises HTTPException on
    missing graph / missing org context / disallowed role."""
    g = _state["graph"]
    if g is None:
        raise HTTPException(status_code=503, detail="Schema graph not loaded yet")

    role = principal["role"]
    org_id = principal["org_id"]
    if role == auth.ADMIN:
        return run_pipeline(question, g, SCHEMA)            # full Text2SQL
    if role in (auth.ORGANIZATION, auth.STAFF):
        if not org_id:
            raise HTTPException(status_code=403, detail="No organization context in token")
        return run_pipeline_org(question, _state["org_graph"], org_id)  # DB-scoped
    raise HTTPException(status_code=403, detail="Analytics not available for this role")


def _to_response(question: str, out: dict, answer: str | None = None) -> QueryResponse:
    rows = out["result"] or []
    columns = list(rows[0].keys()) if rows else []
    return QueryResponse(
        question=question,
        title=out["title"],
        sql=out["predicted_sql"],
        rows=rows,
        columns=columns,
        chartType=out["chart_type"],
        chartConfig=out["chart_config"],
        relevantTables=out["relevant_tables"],
        method=out["schema_linking_method"],
        retries=out["retries"],
        error=out["error"],
        answer=answer,
    )


def _authenticate(authorization: str | None) -> dict:
    try:
        return auth.principal_from_header(authorization)
    except auth.AuthError as e:
        raise HTTPException(status_code=401, detail=str(e))


@app.post("/api/analytics/query", response_model=QueryResponse)
def query(req: QueryRequest, authorization: str | None = Header(default=None)) -> QueryResponse:
    """Text2SQL → rows + chart config (for the Statistics view)."""
    principal = _authenticate(authorization)
    try:
        out = _run_for_principal(principal, req.question)
    except HTTPException:
        raise
    except Exception as e:
        logger.exception("Pipeline crashed for: %s", req.question)
        raise HTTPException(status_code=500, detail=f"Pipeline crashed: {e}")
    return _to_response(req.question, out)


@app.post("/api/analytics/chat", response_model=QueryResponse)
def chat(req: QueryRequest, authorization: str | None = Header(default=None)) -> QueryResponse:
    """Text2SQL → rows + a natural-language answer (for the Chat view).

    Same scoping as /query; adds one LLM step to summarise the rows in prose.
    """
    principal = _authenticate(authorization)
    try:
        out = _run_for_principal(principal, req.question)
        answer = generate_answer(
            req.question,
            list(out["result"][0].keys()) if out["result"] else [],
            out["result"] or [],
            out["error"],
        )
    except HTTPException:
        raise
    except Exception as e:
        logger.exception("Chat pipeline crashed for: %s", req.question)
        raise HTTPException(status_code=500, detail=f"Pipeline crashed: {e}")
    return _to_response(req.question, out, answer)
