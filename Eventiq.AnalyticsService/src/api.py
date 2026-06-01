"""FastAPI HTTP wrapper for the Text2SQL pipeline.

Exposes:
  POST /api/analytics/query  { question }  -> result + sql + chart type
  GET  /health
"""
from __future__ import annotations

import logging
from contextlib import asynccontextmanager
from typing import Any

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

from .pipeline import run_pipeline
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
    sql: str
    rows: list[dict[str, Any]]
    columns: list[str]
    chartType: str
    relevantTables: list[str]
    method: str
    retries: int
    error: str | None = None


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "graphLoaded": "yes" if _state["graph"] is not None else "no"}


@app.post("/api/analytics/query", response_model=QueryResponse)
def query(req: QueryRequest) -> QueryResponse:
    g = _state["graph"]
    if g is None:
        raise HTTPException(status_code=503, detail="Schema graph not loaded yet")

    try:
        out = run_pipeline(req.question, g, SCHEMA)
    except Exception as e:
        logger.exception("Pipeline crashed for: %s", req.question)
        raise HTTPException(status_code=500, detail=f"Pipeline crashed: {e}")

    rows = out["result"] or []
    columns = list(rows[0].keys()) if rows else []

    return QueryResponse(
        question=req.question,
        sql=out["predicted_sql"],
        rows=rows,
        columns=columns,
        chartType=out["chart_type"],
        relevantTables=out["relevant_tables"],
        method=out["schema_linking_method"],
        retries=out["retries"],
        error=out["error"],
    )
