from decimal import Decimal
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, field_validator


class StrictModel(BaseModel):
    model_config = ConfigDict(extra="forbid", str_strip_whitespace=True)


class AdvisoryModel(BaseModel):
    model_config = ConfigDict(extra="ignore", str_strip_whitespace=True)
    confidence: float = Field(ge=0, le=1)
    advisory_only: Literal[True] = True

    @field_validator("advisory_only", mode="before")
    @classmethod
    def require_advisory(cls, value: object) -> bool:
        if value in (True, "true", "True", 1, "1"):
            return True
        raise ValueError("advisory_only must be true")

    @field_validator("confidence", mode="before")
    @classmethod
    def normalize_confidence(cls, value: object) -> object:
        if isinstance(value, (int, float)) and value > 1:
            return value / 100
        return value


class PolicyAgentInput(StrictModel):
    claim_id: int = Field(gt=0)
    category: str = Field(min_length=1, max_length=100)
    amount: Decimal = Field(ge=0)
    currency: str = Field(pattern=r"^[A-Z]{3}$")
    authoritative_violations: list[str] = Field(max_length=50)
    context: str | None = Field(default=None, max_length=2000)


class PolicyRecommendation(AdvisoryModel):
    recommendation: Literal["approve", "request_information", "escalate", "reject"]
    explanation: str = Field(min_length=1, max_length=1000)
    cited_rule_codes: list[str] = Field(default_factory=list, max_length=50)

    @field_validator("recommendation", mode="before")
    @classmethod
    def normalize_policy_recommendation(cls, value: object) -> object:
        aliases = {
            "approved": "approve", "ok": "approve",
            "rejected": "reject", "deny": "reject",
            "info": "request_information", "request_info": "request_information",
        }
        if isinstance(value, str):
            return aliases.get(value.strip().lower(), value.strip().lower())
        return value


class FraudAgentInput(StrictModel):
    claim_id: int = Field(gt=0)
    risk_score: Decimal = Field(ge=0, le=100)
    deterministic_flags: list[str] = Field(max_length=50)
    evidence_summary: dict[str, str | int | float | bool | None] = Field(max_length=50)
    analyst_context: str | None = Field(default=None, max_length=2000)


class FraudRecommendation(AdvisoryModel):
    recommendation: Literal["dismiss", "review", "escalate"]
    explanation: str = Field(min_length=1, max_length=1000)
    cited_flag_codes: list[str] = Field(default_factory=list, max_length=50)

    @field_validator("recommendation", mode="before")
    @classmethod
    def normalize_fraud_recommendation(cls, value: object) -> object:
        aliases = {"dismissed": "dismiss", "ok": "dismiss", "flag": "review", "reviewed": "review"}
        if isinstance(value, str):
            return aliases.get(value.strip().lower(), value.strip().lower())
        return value
