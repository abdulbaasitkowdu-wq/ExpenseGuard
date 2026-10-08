using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpenseGuard.Api.Services;

public interface IClaimReviewClient
{
    Task<ClaimReviewResponse?> ReviewAsync(ClaimReviewRequest request, CancellationToken ct);
    Task<ClaimReviewReceipt?> ExtractAsync(string receiptText, CancellationToken ct);
}

public sealed class ClaimReviewRequest
{
    [JsonPropertyName("workflow_id")] public string WorkflowId { get; init; } = "";
    [JsonPropertyName("expense_claim_id")] public int ExpenseClaimId { get; init; }
    [JsonPropertyName("reimbursement_id")] public int ReimbursementId { get; init; }
    [JsonPropertyName("kind")] public string Kind { get; init; } = "claim";
    [JsonPropertyName("objective")] public string Objective { get; init; } = "review";
    [JsonPropertyName("description")] public string Description { get; init; } = "";
    [JsonPropertyName("receipt_text")] public string ReceiptText { get; init; } = "";
    [JsonPropertyName("amount")] public decimal Amount { get; init; }
    [JsonPropertyName("category")] public string Category { get; init; } = "Unknown";
    [JsonPropertyName("currency")] public string Currency { get; init; } = "LKR";
    [JsonPropertyName("vendor")] public string? Vendor { get; init; }
    [JsonPropertyName("policies")] public IReadOnlyList<ClaimReviewPolicy> Policies { get; init; } = [];
    [JsonPropertyName("budgets")] public IReadOnlyList<ClaimReviewBudget> Budgets { get; init; } = [];
}

public sealed class ClaimReviewPolicy
{
    [JsonPropertyName("policy_code")] public string PolicyCode { get; init; } = "";
    [JsonPropertyName("category")] public string Category { get; init; } = "";
    [JsonPropertyName("min_amount")] public decimal? MinAmount { get; init; }
    [JsonPropertyName("max_amount")] public decimal? MaxAmount { get; init; }
    [JsonPropertyName("receipt_required")] public bool ReceiptRequired { get; init; }
}

public sealed class ClaimReviewBudget
{
    [JsonPropertyName("budget_id")] public int BudgetId { get; init; }
    [JsonPropertyName("currency")] public string Currency { get; init; } = "LKR";
    [JsonPropertyName("allocated")] public decimal Allocated { get; init; }
    [JsonPropertyName("reserved")] public decimal Reserved { get; init; }
    [JsonPropertyName("spent")] public decimal Spent { get; init; }
    [JsonPropertyName("reported_available")] public decimal ReportedAvailable { get; init; }
}

public sealed class ClaimReviewResponse
{
    [JsonPropertyName("findings")] public List<ClaimReviewFinding> Findings { get; set; } = [];
    [JsonPropertyName("receipt")] public ClaimReviewReceipt? Receipt { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
}

public sealed class ClaimReviewFinding
{
    [JsonPropertyName("agent")] public string Agent { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("summary")] public string Summary { get; set; } = "";
    [JsonPropertyName("evidence")] public JsonElement Evidence { get; set; }
}

public sealed class ClaimReviewReceipt
{
    [JsonPropertyName("vendor")] public string? Vendor { get; set; }
    [JsonPropertyName("amount")] public decimal? Amount { get; set; }
    [JsonPropertyName("purchase_date")] public DateTime? PurchaseDate { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("confidence")] public decimal? Confidence { get; set; }
    [JsonPropertyName("requires_manual_review")] public bool RequiresManualReview { get; set; }
    [JsonPropertyName("review_reasons")] public List<string> ReviewReasons { get; set; } = [];
}

public static class ClaimReviewJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}

public sealed class LangGraphClaimReviewClient(HttpClient http, ILogger<LangGraphClaimReviewClient> logger) : IClaimReviewClient
{
    private static readonly JsonSerializerOptions Json = ClaimReviewJson.Options;

    public async Task<ClaimReviewResponse?> ReviewAsync(ClaimReviewRequest request, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("reviews/claim", request, Json, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("LangGraph review returned {Status}", (int)response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<ClaimReviewResponse>(Json, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LangGraph review is unavailable; continuing with .NET policy and budget checks.");
            return null;
        }
    }

    public async Task<ClaimReviewReceipt?> ExtractAsync(string receiptText, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("receipts/extract", new { receipt_text = receiptText }, Json, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ClaimReviewReceipt>(Json, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LangGraph receipt extraction is unavailable; using OCR text parser.");
            return null;
        }
    }
}
