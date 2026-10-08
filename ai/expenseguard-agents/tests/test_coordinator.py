import httpx
import pytest
from langgraph.types import Command
from expenseguard_agents.graph import build_graph
from expenseguard_agents.tools import AspNetToolClient


@pytest.mark.asyncio
async def test_workflow_always_pauses_for_human():
    graph = build_graph()
    config = {"configurable": {"thread_id": "mandatory-pause"}}
    result = await graph.ainvoke({
        "workflow_id": "mandatory-pause", "expense_claim_id": 1,
        "reimbursement_id": 2, "objective": "review",
    }, config=config)
    assert "__interrupt__" in result
    assert result["status"] == "awaiting_human"
    resumed = await graph.ainvoke(Command(resume={
        "decision": "approved", "approver_employee_id": 7,
    }), config=config)
    assert resumed["status"] == "approved"


@pytest.mark.asyncio
async def test_tool_client_rejects_non_allow_listed_tool():
    result = await AspNetToolClient("http://unused", None).call("run_shell", {})
    assert not result.ok
    assert "allow-listed" in result.error


@pytest.mark.asyncio
async def test_tool_transport_failure_is_safe(monkeypatch):
    async def fail(*args, **kwargs):
        raise httpx.ConnectError("offline")
    monkeypatch.setattr(httpx.AsyncClient, "request", fail)
    result = await AspNetToolClient("http://offline", None, retries=0).call(
        "get_reimbursement", {"reimbursement_id": 1})
    assert not result.ok
    assert result.error == "Tool unavailable: ConnectError"
