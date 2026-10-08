import sys
from contextlib import asynccontextmanager
from typing import Any, AsyncIterator

from langgraph.checkpoint.memory import MemorySaver
from langgraph.graph import END, START, StateGraph
from langgraph.types import interrupt

from .contracts import AgentFinding, CoordinatorState, HumanDecision
from .settings import Settings


def _placeholder(agent: str) -> dict[str, Any]:
    return AgentFinding(
        agent=agent, status="not_implemented",
        summary=f"{agent} agent contract placeholder; implementation is owned by another branch",
    ).model_dump()


def coordinate(state: CoordinatorState) -> dict[str, Any]:
    if (state.get("receipt_text") or "").strip() or state.get("policies") or state.get("budgets"):
        from .claim_review import ClaimReviewRequest, review_claim

        review = review_claim(ClaimReviewRequest.model_validate(state), Settings())
        return {
            "findings": [item.model_dump() for item in review.findings],
            "status": "awaiting_human",
        }
    return {
        "findings": [_placeholder(name) for name in ("receipt", "policy", "fraud", "budget")],
        "status": "awaiting_human",
    }


def require_human(state: CoordinatorState) -> dict[str, Any]:
    raw = interrupt({
        "kind": "reimbursement_approval",
        "workflow_id": state["workflow_id"],
        "reimbursement_id": state["reimbursement_id"],
        "findings": state["findings"],
    })
    decision = HumanDecision.model_validate(raw)
    return {"human_decision": decision.model_dump(), "status": decision.decision}


def build_graph(checkpointer: Any | None = None):
    graph = StateGraph(CoordinatorState)
    graph.add_node("coordinate", coordinate)
    graph.add_node("human_approval", require_human)
    graph.add_edge(START, "coordinate")
    graph.add_edge("coordinate", "human_approval")
    graph.add_edge("human_approval", END)
    return graph.compile(checkpointer=checkpointer or MemorySaver())


@asynccontextmanager
async def postgres_checkpointer(settings: Settings) -> AsyncIterator[Any]:
    if settings.fake_mode:
        yield MemorySaver()
        return
    # Uvicorn on Windows always runs ProactorEventLoop. psycopg async refuses that loop,
    # so use the sync saver here; Linux/macOS keep AsyncPostgresSaver.
    if sys.platform == "win32":
        from langgraph.checkpoint.postgres import PostgresSaver

        with PostgresSaver.from_conn_string(settings.postgres_dsn) as saver:
            saver.setup()
            yield saver
        return
    from langgraph.checkpoint.postgres.aio import AsyncPostgresSaver

    async with AsyncPostgresSaver.from_conn_string(settings.postgres_dsn) as saver:
        await saver.setup()
        yield saver
