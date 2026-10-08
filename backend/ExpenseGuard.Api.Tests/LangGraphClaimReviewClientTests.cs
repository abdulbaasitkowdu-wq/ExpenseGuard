using System.Text.Json;
using ExpenseGuard.Api.Services;
using Xunit;

namespace ExpenseGuard.Api.Tests;

public sealed class LangGraphClaimReviewClientTests
{
    [Fact]
    public void Review_payload_reads_decimal_fields_from_json_strings()
    {
        const string json = """
            {
              "findings": [],
              "status": "awaiting_human",
              "receipt": {
                "vendor": "IKEA",
                "amount": "85.50",
                "purchase_date": "2026-10-06",
                "currency": "USD",
                "confidence": "0.86",
                "requires_manual_review": true,
                "review_reasons": []
              }
            }
            """;

        var review = JsonSerializer.Deserialize<ClaimReviewResponse>(json, ClaimReviewJson.Options);

        Assert.NotNull(review?.Receipt);
        Assert.Equal(85.50m, review.Receipt.Amount);
        Assert.Equal(0.86m, review.Receipt.Confidence);
        Assert.Equal("IKEA", review.Receipt.Vendor);
    }
}
