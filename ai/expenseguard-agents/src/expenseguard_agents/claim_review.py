"""Advisory claim review: OCR text → Gemini receipt, then policy/fraud/budget findings.

Authoritative approve/reject/reserve stays in the .NET API.
"""

from __future__ import annotations

import os
from decimal import Decimal
from typing import Any

from pydantic import BaseModel, ConfigDict, Field

from .budget_monitor import BudgetMonitorInput, BudgetSnapshot, monitor_budget
from .contracts import AgentFinding
from .fraud_agent import FraudRiskAgent
from .policy_agent import PolicyComplianceAgent
from .policy_fraud_contracts import FraudAgentInput, PolicyAgentInput
from .receipt_extraction import GeminiReceiptExtractor, HeuristicReceiptExtractor, ReceiptExtraction, ReceiptExtractionAgent
from .settings import Settings


class PolicySnapshot(BaseModel):
    policy_code: str
    category: str
    min_amount: Decimal | None = None
    max_amount: Decimal | None = None
    receipt_required: bool = False


class BudgetReviewSnapshot(BaseModel):
    budget_id: int
    currency: str = Field(pattern=r"^[A-Z]{3}$")
    allocated: Decimal = Field(ge=0)
    reserved: Decimal = Field(ge=0)
    spent: Decimal = Field(ge=0)
    reported_available: Decimal


class ClaimReviewRequest(BaseModel):
    model_config = ConfigDict(extra="ignore")

    workflow_id: str
    expense_claim_id: int
    reimbursement_id: int = 0
    kind: str = "claim"
    objective: str = "review"
    description: str = ""
    receipt_text: str = ""
    amount: Decimal = Decimal("0")
    category: str = "Unknown"
    currency: str = Field(default="LKR", pattern=r"^[A-Z]{3}$")
    vendor: str | None = None
    policies: list[PolicySnapshot] = Field(default_factory=list)
    budgets: list[BudgetReviewSnapshot] = Field(default_factory=list)


class ClaimReviewResponse(BaseModel):
    findings: list[AgentFinding]
    receipt: ReceiptExtraction | None = None
    status: str = "awaiting_human"


def _prepare_gemini(settings: Settings) -> None:
    if settings.gemini_api_key:
        os.environ.setdefault("GEMINI_API_KEY", settings.gemini_api_key)
    os.environ.setdefault("GEMINI_MODEL", settings.gemini_model)


def extract_receipt_text(text: str, settings: Settings) -> ReceiptExtraction:
    return _extract_receipt(text, settings)


def _extract_receipt(text: str, settings: Settings) -> ReceiptExtraction:
    agent = ReceiptExtractionAgent(HeuristicReceiptExtractor())
    if settings.fake_mode:
        return agent.run(text)
    _prepare_gemini(settings)
    try:
        return ReceiptExtractionAgent(GeminiReceiptExtractor()).run(text)
    except Exception:
        return agent.run(text)


def _policy_violations(request: ClaimReviewRequest, extraction: ReceiptExtraction) -> list[str]:
    codes: list[str] = []
    amount = extraction.amount if extraction.amount is not None else request.amount
    has_receipt = bool(request.receipt_text.strip())
    for policy in request.policies:
        if policy.receipt_required and not has_receipt:
            codes.append("RECEIPT_REQUIRED")
        if policy.min_amount is not None and amount < policy.min_amount:
            codes.append("BELOW_MINIMUM")
        if policy.max_amount is not None and amount > policy.max_amount:
            codes.append("POLICY_CAP_EXCEEDED")
    if extraction.amount is not None and request.amount > 0 and extraction.amount != request.amount:
        codes.append("AMOUNT_MISMATCH")
    return list(dict.fromkeys(codes))


def _finding(agent: str, status: str, summary: str, evidence: dict[str, Any] | None = None) -> AgentFinding:
    return AgentFinding(agent=agent, status=status, summary=summary, evidence=evidence or {})


def review_claim(request: ClaimReviewRequest, settings: Settings) -> ClaimReviewResponse:
    is_purchase_request = request.kind == "purchase_request"
    if is_purchase_request:
        extraction = ReceiptExtraction(
            vendor=request.vendor,
            amount=request.amount if request.amount > 0 else None,
            currency=request.currency,
            confidence=1,
            requires_manual_review=False,
        )
        findings = [
            _finding(
                "receipt",
                "ok",
                "Purchase request reviewed from description, amount, and vendor.",
                {"description": request.description, "vendor": request.vendor, "amount": str(request.amount)},
            )
        ]
        violations = [code for code in _policy_violations(request, extraction) if code not in {"RECEIPT_REQUIRED", "AMOUNT_MISMATCH"}]
    else:
        extraction = _extract_receipt(request.receipt_text, settings)
        receipt_status = "review" if extraction.requires_manual_review else "ok"
        findings = [
            _finding(
                "receipt",
                receipt_status,
                "Gemini receipt extraction" if not settings.fake_mode else "OCR heuristic extraction",
                extraction.model_dump(mode="json"),
            )
        ]
        violations = _policy_violations(request, extraction)
    category = request.category.strip() or "Unknown"
    try:
        if settings.fake_mode:
            findings.append(
                _finding(
                    "policy",
                    "review" if violations else "ok",
                    "No policy violations." if not violations else f"Flags: {', '.join(violations)}",
                    {"authoritative_violations": violations, "advisory_only": True},
                )
            )
        else:
            _prepare_gemini(settings)
            recommendation = PolicyComplianceAgent().recommend(
                PolicyAgentInput(
                    claim_id=max(request.expense_claim_id, 1),
                    category=category,
                    amount=request.amount,
                    currency=request.currency,
                    authoritative_violations=violations,
                    context=request.description or None,
                )
            )
            findings.append(
                _finding(
                    "policy",
                    "ok" if recommendation.recommendation == "approve" else "review",
                    recommendation.explanation,
                    recommendation.model_dump(mode="json"),
                )
            )
    except Exception as exc:
        findings.append(_finding("policy", "failed", type(exc).__name__, {"authoritative_violations": violations}))

    flags = [code for code in violations if code in {"AMOUNT_MISMATCH", "RECEIPT_REQUIRED", "POLICY_CAP_EXCEEDED"}]
    risk = Decimal("45") if flags else Decimal("10")
    try:
        if settings.fake_mode:
            findings.append(
                _finding(
                    "fraud",
                    "review" if flags else "ok",
                    "No deterministic fraud flags." if not flags else f"Flags: {', '.join(flags)}",
                    {"deterministic_flags": flags, "advisory_only": True},
                )
            )
        else:
            _prepare_gemini(settings)
            recommendation = FraudRiskAgent().recommend(
                FraudAgentInput(
                    claim_id=max(request.expense_claim_id, 1),
                    risk_score=risk,
                    deterministic_flags=flags,
                    evidence_summary={
                        "claim_amount": float(request.amount),
                        "receipt_amount": float(extraction.amount) if extraction.amount else None,
                        "vendor": request.vendor,
                        "description": request.description or None,
                    },
                )
            )
            findings.append(
                _finding(
                    "fraud",
                    "ok" if recommendation.recommendation == "dismiss" else "review",
                    recommendation.explanation,
                    recommendation.model_dump(mode="json"),
                )
            )
    except Exception as exc:
        findings.append(_finding("fraud", "failed", type(exc).__name__, {"deterministic_flags": flags}))

    budget = monitor_budget(
        BudgetMonitorInput(
            budgets=[
                BudgetSnapshot(
                    budget_id=item.budget_id,
                    currency=item.currency,
                    allocated=item.allocated,
                    reserved=item.reserved,
                    spent=item.spent,
                    reported_available=item.reported_available,
                )
                for item in request.budgets
            ]
        )
    )
    level = max((item.level for item in budget.findings), default="normal")
    findings.append(
        _finding(
            "budget",
            "review" if level != "normal" else "ok",
            f"Budget monitor level: {level}",
            budget.model_dump(mode="json"),
        )
    )
    return ClaimReviewResponse(findings=findings, receipt=extraction, status="awaiting_human")
