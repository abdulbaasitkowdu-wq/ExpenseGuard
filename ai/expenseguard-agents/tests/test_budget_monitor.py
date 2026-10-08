from decimal import Decimal

from expenseguard_agents import BudgetMonitorInput, BudgetSnapshot, monitor_budget


def test_monitor_is_deterministic_and_read_only_without_api_key(monkeypatch):
    monkeypatch.delenv("EXPENSEGUARD_GEMINI_API_KEY", raising=False)
    request = BudgetMonitorInput(
        budgets=[
            BudgetSnapshot(
                budget_id=7,
                currency="LKR",
                allocated=Decimal("1000"),
                reserved=Decimal("200"),
                spent=Decimal("750"),
                reported_available=Decimal("60"),
                recent_spend=Decimal("300"),
                baseline_spend=Decimal("100"),
            )
        ]
    )

    result = monitor_budget(request)

    assert result.findings[0].utilization_percent == Decimal("95.00")
    assert set(result.findings[0].anomalies) == {
        "balance_mismatch",
        "high_utilization",
        "spend_spike",
    }
    assert result.findings[0].level == "critical"
    assert result.authoritative_mutation_performed is False
    assert result.model_enrichment_configured is False
