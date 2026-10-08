import json
import os
import re
from datetime import date, datetime
from decimal import Decimal, InvalidOperation
from typing import Protocol

from pydantic import BaseModel, ConfigDict, Field, field_serializer, field_validator


class ReceiptExtraction(BaseModel):
    model_config = ConfigDict(extra="forbid")

    vendor: str | None = None
    amount: Decimal | None = Field(default=None, gt=0)
    purchase_date: date | None = None
    currency: str | None = Field(default=None, pattern=r"^[A-Z]{3}$")
    confidence: float = Field(ge=0, le=1)
    requires_manual_review: bool
    review_reasons: list[str] = Field(default_factory=list)

    @field_validator("currency", mode="before")
    @classmethod
    def normalize_currency(cls, value: str | None) -> str | None:
        return value.upper() if value else None

    @field_validator("amount", mode="before")
    @classmethod
    def coerce_amount(cls, value: object) -> Decimal | None:
        if value is None or value == "":
            return None
        if isinstance(value, Decimal):
            return value
        if isinstance(value, (int, float)):
            return Decimal(str(value))
        if isinstance(value, str):
            cleaned = value.strip().replace(",", "").replace("$", "")
            try:
                return Decimal(cleaned)
            except InvalidOperation:
                return None
        return None

    @field_serializer("amount")
    def serialize_amount(self, value: Decimal | None) -> float | None:
        return float(value) if value is not None else None


class ExtractionProvider(Protocol):
    def extract(self, receipt_text: str) -> ReceiptExtraction: ...


class ReceiptExtractionAgent:
    def __init__(self, provider: ExtractionProvider, review_threshold: float = 0.80):
        self.provider = provider
        self.review_threshold = review_threshold

    def run(self, receipt_text: str) -> ReceiptExtraction:
        if not receipt_text.strip():
            return ReceiptExtraction(
                confidence=0, requires_manual_review=True, review_reasons=["empty_receipt_text"]
            )
        result = self.provider.extract(receipt_text)
        reasons = list(result.review_reasons)
        if result.confidence < self.review_threshold:
            reasons.append("low_confidence")
        missing = [
            name
            for name in ("vendor", "amount", "purchase_date", "currency")
            if getattr(result, name) is None
        ]
        reasons.extend(f"missing_{name}" for name in missing)
        return result.model_copy(
            update={
                "requires_manual_review": bool(reasons),
                "review_reasons": sorted(set(reasons)),
            }
        )


class HeuristicReceiptExtractor:
    """Fallback when Gemini is off or fails. Uses OCR text only, no model call."""

    _amount = re.compile(r"(?<![A-Za-z0-9])(\d{1,3}(?:,\d{3})*(?:\.\d{2})|\d+\.\d{2})(?![A-Za-z0-9.])")

    def extract(self, receipt_text: str) -> ReceiptExtraction:
        lines = [line.strip() for line in receipt_text.splitlines() if line.strip()]
        vendor = None
        skip = ("receipt", "bill to", "description", "subtotal", "thank you", "date:")
        address = ("lane", "street", "road", "avenue", "united states", "email", "phone", "www.")
        for line in lines:
            lower = line.lower()
            if any(lower.startswith(s) for s in skip) or any(a in lower for a in address) or line[:1].isdigit():
                continue
            if re.search(r"\b(LLC|Inc\.?|Ltd\.?|Media|Company|Corp\.?)\b", line, re.I):
                vendor = line
                break
            vendor = vendor or line
        amount = None
        labeled = re.search(r"Total Paid\s*:?\s*\$?\s*([\d,]+(?:\.\d{2})?)", receipt_text, re.I)
        if labeled:
            try:
                amount = Decimal(labeled.group(1).replace(",", ""))
            except InvalidOperation:
                amount = None
        if amount is None:
            matches = self._amount.findall(receipt_text)
            if matches:
                try:
                    amount = Decimal(matches[-1].replace(",", ""))
                except InvalidOperation:
                    amount = None
        currency = "USD" if re.search(r"\bUSD\b|\$", receipt_text, re.I) else None
        if currency is None and re.search(r"\bLKR\b|Rs\.?", receipt_text, re.I):
            currency = "LKR"
        purchase_date = None
        dated = re.search(r"Date\s*:\s*([A-Za-z]+\s+\d{1,2},\s+\d{4}|\d{4}-\d{2}-\d{2})", receipt_text, re.I)
        if dated:
            try:
                purchase_date = datetime.strptime(dated.group(1), "%B %d, %Y").date()
            except ValueError:
                try:
                    purchase_date = date.fromisoformat(dated.group(1))
                except ValueError:
                    purchase_date = None
        complete = bool(vendor and amount and purchase_date and currency)
        return ReceiptExtraction(
            vendor=vendor[:200] if vendor else None,
            amount=amount,
            purchase_date=purchase_date,
            currency=currency,
            confidence=0.86 if complete else 0.45,
            requires_manual_review=not complete,
            review_reasons=[] if complete else ["heuristic_ocr_parse"],
        )


class GeminiReceiptExtractor:
    """Production provider. Configuration is read only from environment variables."""

    def __init__(self) -> None:
        api_key = os.environ.get("GEMINI_API_KEY")
        if not api_key:
            raise RuntimeError("GEMINI_API_KEY is not configured")
        from google import genai

        self.client = genai.Client(api_key=api_key)
        self.model = os.environ.get("GEMINI_MODEL", "gemini-3.8-flash")

    def extract(self, receipt_text: str) -> ReceiptExtraction:
        prompt = (
            "Extract receipt fields as strict JSON with keys vendor, amount, purchase_date "
            "(YYYY-MM-DD), currency (ISO-4217), confidence (0..1), "
            "requires_manual_review, and review_reasons. "
            "vendor is the business name, never a street address. "
            "amount is the grand total / Total Paid as a number. "
            "Ignore taglines, emails, and phone numbers.\nReceipt text:\n" + receipt_text
        )
        response = self.client.models.generate_content(model=self.model, contents=prompt)
        raw = response.text.strip().removeprefix("```json").removesuffix("```").strip()
        return ReceiptExtraction.model_validate(json.loads(raw))
