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

from . import auth, saved_queries
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


class SaveQueryRequest(BaseModel):
    title: str = Field(..., min_length=1, max_length=200)
    question: str = Field(..., min_length=2, max_length=500)
    sql: str = Field(..., min_length=1)


class SavedQueryResponse(BaseModel):
    id: str
    title: str
    question: str
    sql: str
    createdAt: str


def _require_org(principal: dict) -> str:
    if principal["role"] not in (auth.ORGANIZATION, auth.STAFF):
        raise HTTPException(status_code=403, detail="Only org users can manage saved queries")
    org_id = principal["org_id"]
    if not org_id:
        raise HTTPException(status_code=403, detail="No organization context in token")
    return org_id


def _to_saved(row: dict) -> SavedQueryResponse:
    return SavedQueryResponse(
        id=str(row["id"]),
        title=row["title"],
        question=row["question"],
        sql=row["sql"],
        createdAt=row["created_at"].isoformat(),
    )


@app.post("/api/analytics/saved-queries", response_model=SavedQueryResponse, status_code=201)
def save_query(req: SaveQueryRequest, authorization: str | None = Header(default=None)) -> SavedQueryResponse:
    principal = _authenticate(authorization)
    org_id = _require_org(principal)
    row = saved_queries.create(org_id, principal["user_id"], req.title, req.question, req.sql)
    return _to_saved(row)


@app.get("/api/analytics/saved-queries", response_model=list[SavedQueryResponse])
def list_saved_queries(authorization: str | None = Header(default=None)) -> list[SavedQueryResponse]:
    principal = _authenticate(authorization)
    org_id = _require_org(principal)
    return [_to_saved(r) for r in saved_queries.list_for_org(org_id)]


@app.delete("/api/analytics/saved-queries/{query_id}", status_code=204, response_model=None)
def delete_saved_query(query_id: str, authorization: str | None = Header(default=None)) -> None:
    principal = _authenticate(authorization)
    org_id = _require_org(principal)
    if not saved_queries.delete(query_id, org_id):
        raise HTTPException(status_code=404, detail="Query not found")


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
