from datetime import date
from decimal import Decimal

from expenseguard_agents import ReceiptExtraction, ReceiptExtractionAgent


class FakeProvider:
    def __init__(self, result: ReceiptExtraction):
        self.result = result

    def extract(self, receipt_text: str) -> ReceiptExtraction:
        return self.result


def test_complete_high_confidence_result_needs_no_review():
    result = ReceiptExtractionAgent(
        FakeProvider(
            ReceiptExtraction(
                vendor="Cafe", amount=Decimal("12.34"), purchase_date=date(2026, 1, 2),
                currency="usd", confidence=0.95, requires_manual_review=False
            )
        )
    ).run("valid receipt")
    assert result.currency == "USD"
    assert result.requires_manual_review is False
    assert result.review_reasons == []


def test_low_confidence_and_missing_fields_require_review():
    result = ReceiptExtractionAgent(
        FakeProvider(ReceiptExtraction(confidence=0.4, requires_manual_review=False))
    ).run("blurred")
    assert result.requires_manual_review is True
    assert "low_confidence" in result.review_reasons
    assert "missing_amount" in result.review_reasons


def test_empty_text_does_not_call_provider():
    result = ReceiptExtractionAgent(
        FakeProvider(ReceiptExtraction(confidence=1, requires_manual_review=False))
    ).run(" ")
    assert result.confidence == 0
    assert result.review_reasons == ["empty_receipt_text"]
