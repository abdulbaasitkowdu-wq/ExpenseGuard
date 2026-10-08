from typing import Any, Literal, TypedDict
from decimal import Decimal
from pydantic import BaseModel, Field


class AgentFinding(BaseModel):
    agent: Literal["receipt", "policy", "fraud", "budget"]
    status: Literal["ok", "review", "failed", "not_implemented"]
    summary: str
    evidence: dict[str, Any] = Field(default_factory=dict)


class HumanDecision(BaseModel):
    decision: Literal["approved", "rejected", "revision_required"]
    approver_employee_id: int
    comment: str | None = None


class CoordinatorRequest(BaseModel):
    workflow_id: str
    expense_claim_id: int
    reimbursement_id: int
    objective: str
    receipt_text: str = ""
    amount: Decimal = Decimal("0")
    category: str = "Unknown"
    currency: str = "LKR"
    vendor: str | None = None
    policies: list[dict[str, Any]] = Field(default_factory=list)
    budgets: list[dict[str, Any]] = Field(default_factory=list)


class CoordinatorState(TypedDict, total=False):
    workflow_id: str
    expense_claim_id: int
    reimbursement_id: int
    objective: str
    findings: list[dict[str, Any]]
    human_decision: dict[str, Any]
    status: str
    error: str


class ToolResult(BaseModel):
    ok: bool
    status_code: int | None = None
    data: dict[str, Any] | list[Any] | None = None
    error: str | None = None
