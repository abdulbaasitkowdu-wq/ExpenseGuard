from decimal import Decimal

from expenseguard_agents.claim_review import ClaimReviewRequest, review_claim
from expenseguard_agents.settings import Settings


def test_review_reads_ocr_text_then_policy_and_budget(monkeypatch):
    monkeypatch.setenv("EXPENSEGUARD_FAKE_MODE", "true")
    result = review_claim(
        ClaimReviewRequest(
            workflow_id="demo",
            expense_claim_id=1,
            receipt_text="AdCo\nCampaign ads\nTotal 15000.00 USD",
            amount=Decimal("15000"),
            category="Advertising",
            currency="USD",
            policies=[{"policy_code": "ADV-DEMO", "category": "Advertising", "max_amount": "50000", "receipt_required": True}],
            budgets=[{
                "budget_id": 1, "currency": "USD", "allocated": "100000",
                "reserved": "0", "spent": "0", "reported_available": "100000",
            }],
        ),
        Settings(),
    )
    agents = [item.agent for item in result.findings]
    assert agents == ["receipt", "policy", "fraud", "budget"]
    assert result.receipt is not None
    assert result.receipt.amount == Decimal("15000.00")
    dumped = result.receipt.model_dump(mode="json")
    assert dumped["amount"] == 15000.0
    assert isinstance(dumped["amount"], float)
    assert result.status == "awaiting_human"
    assert result.findings[1].status == "ok"
    assert result.findings[3].status == "ok"


def test_purchase_request_review_skips_receipt_and_flags_policy_cap(monkeypatch):
    monkeypatch.setenv("EXPENSEGUARD_FAKE_MODE", "true")
    result = review_claim(
        ClaimReviewRequest(
            workflow_id="pr-1",
            expense_claim_id=4,
            kind="purchase_request",
            description="GitHub seats for the engineering team",
            amount=Decimal("25000"),
            category="GitHub",
            currency="USD",
            vendor="GitHub",
            policies=[{"policy_code": "ENG-GH", "category": "GitHub", "max_amount": "1000", "receipt_required": True}],
            budgets=[{
                "budget_id": 2, "currency": "USD", "allocated": "10000",
                "reserved": "0", "spent": "0", "reported_available": "10000",
            }],
        ),
        Settings(),
    )
    assert result.findings[0].agent == "receipt"
    assert result.findings[0].status == "ok"
    assert result.findings[1].agent == "policy"
    assert result.findings[1].status == "review"
    assert "POLICY_CAP_EXCEEDED" in result.findings[1].evidence["authoritative_violations"]
