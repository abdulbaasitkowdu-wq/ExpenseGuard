import inspect
import sys
from contextlib import asynccontextmanager
from typing import Any

from fastapi import FastAPI, HTTPException
from langgraph.types import Command

from .claim_review import ClaimReviewRequest, extract_receipt_text, review_claim
from .contracts import CoordinatorRequest, HumanDecision
from .graph import build_graph, postgres_checkpointer
from .settings import Settings

settings = Settings()


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings.validate_runtime()
    async with postgres_checkpointer(settings) as checkpointer:
        app.state.graph = build_graph(checkpointer)
        yield


app = FastAPI(title="ExpenseGuard Coordinator", lifespan=lifespan)


def _run_graph(payload: Any, config: dict) -> Any:
    graph = app.state.graph
    # Windows uses the sync PostgresSaver (psycopg cannot use ProactorEventLoop).
    if sys.platform == "win32" and not settings.fake_mode:
        return graph.invoke(payload, config)
    return graph.ainvoke(payload, config)


@app.get("/health")
async def health() -> dict[str, str | bool]:
    return {"status": "healthy", "fake_mode": settings.fake_mode}


@app.post("/receipts/extract")
def extract_receipt_fields(body: dict[str, Any]) -> Any:
    try:
        return extract_receipt_text(str(body.get("receipt_text") or ""), settings)
    except Exception as exc:
        raise HTTPException(503, detail={"code": "WORKFLOW_SAFE_FAILURE", "message": type(exc).__name__}) from exc


@app.post("/reviews/claim")
def review_submitted_claim(request: ClaimReviewRequest) -> Any:
    try:
        return review_claim(request, settings)
    except Exception as exc:
        raise HTTPException(503, detail={"code": "WORKFLOW_SAFE_FAILURE", "message": type(exc).__name__}) from exc


@app.post("/workflows")
async def start(request: CoordinatorRequest) -> dict:
    config = {"configurable": {"thread_id": request.workflow_id}}
    try:
        result = _run_graph(request.model_dump(), config)
        return await result if inspect.isawaitable(result) else result
    except Exception as exc:
        raise HTTPException(503, detail={"code": "WORKFLOW_SAFE_FAILURE", "message": type(exc).__name__}) from exc


@app.post("/workflows/{workflow_id}/resume")
async def resume(workflow_id: str, decision: HumanDecision) -> dict:
    config = {"configurable": {"thread_id": workflow_id}}
    try:
        result = _run_graph(Command(resume=decision.model_dump()), config)
        return await result if inspect.isawaitable(result) else result
    except Exception as exc:
        raise HTTPException(503, detail={"code": "WORKFLOW_SAFE_FAILURE", "message": type(exc).__name__}) from exc
