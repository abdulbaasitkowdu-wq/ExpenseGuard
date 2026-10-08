"""Read-only budget monitoring contract.

All authoritative balance changes remain in the .NET API. This module only
classifies API-provided decimal snapshots and never writes to the database.
"""

from decimal import Decimal
from enum import StrEnum
from os import getenv

from pydantic import BaseModel, Field, model_validator


class AnomalyKind(StrEnum):
    NEGATIVE_BALANCE = "negative_balance"
    BALANCE_MISMATCH = "balance_mismatch"
    HIGH_UTILIZATION = "high_utilization"
    SPEND_SPIKE = "spend_spike"


class BudgetSnapshot(BaseModel):
    budget_id: int
    currency: str = Field(pattern=r"^[A-Z]{3}$")
    allocated: Decimal = Field(ge=0)
    reserved: Decimal = Field(ge=0)
    spent: Decimal = Field(ge=0)
    reported_available: Decimal
    recent_spend: Decimal = Field(default=Decimal("0"), ge=0)
    baseline_spend: Decimal = Field(default=Decimal("0"), ge=0)


class BudgetMonitorInput(BaseModel):
    budgets: list[BudgetSnapshot]
    warning_percent: Decimal = Field(default=Decimal("80"), ge=0, le=100)
    critical_percent: Decimal = Field(default=Decimal("95"), ge=0, le=100)
    spike_multiplier: Decimal = Field(default=Decimal("2"), gt=0)

    @model_validator(mode="after")
    def thresholds_are_ordered(self) -> "BudgetMonitorInput":
        if self.critical_percent < self.warning_percent:
            raise ValueError("critical_percent must be at least warning_percent")
        return self


class BudgetFinding(BaseModel):
    budget_id: int
    utilization_percent: Decimal
    calculated_available: Decimal
    level: str
    anomalies: list[AnomalyKind]


class BudgetMonitorOutput(BaseModel):
    findings: list[BudgetFinding]
    model_enrichment_configured: bool
    authoritative_mutation_performed: bool = False


def monitor_budget(request: BudgetMonitorInput) -> BudgetMonitorOutput:
    """Produce deterministic findings using Decimal arithmetic only."""
    findings: list[BudgetFinding] = []
    for item in request.budgets:
        available = item.allocated - item.reserved - item.spent
        used = item.reserved + item.spent
        utilization = (
            (used / item.allocated * Decimal("100"))
            if item.allocated
            else (Decimal("100") if used else Decimal("0"))
        ).quantize(Decimal("0.01"))
        anomalies: list[AnomalyKind] = []
        if available < 0:
            anomalies.append(AnomalyKind.NEGATIVE_BALANCE)
        if available != item.reported_available:
            anomalies.append(AnomalyKind.BALANCE_MISMATCH)
        if utilization >= request.warning_percent:
            anomalies.append(AnomalyKind.HIGH_UTILIZATION)
        if item.baseline_spend > 0 and item.recent_spend >= item.baseline_spend * request.spike_multiplier:
            anomalies.append(AnomalyKind.SPEND_SPIKE)
        level = (
            "critical" if utilization >= request.critical_percent or available < 0
            else "warning" if anomalies
            else "normal"
        )
        findings.append(
            BudgetFinding(
                budget_id=item.budget_id,
                utilization_percent=utilization,
                calculated_available=available,
                level=level,
                anomalies=anomalies,
            )
        )
    return BudgetMonitorOutput(
        findings=findings,
        model_enrichment_configured=bool(getenv("EXPENSEGUARD_GEMINI_API_KEY")),
    )
