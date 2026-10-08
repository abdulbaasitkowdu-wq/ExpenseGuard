using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Infrastructure;
using ExpenseGuard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Services;

public sealed record StoredObject(string Url, string PublicId);
public sealed record ReceiptExtraction(string? Vendor, decimal? Amount, DateTime? Date, string? Currency, decimal Confidence, bool RequiresManualReview, string? RawText = null);

public interface IReceiptStorage
{
    Task<StoredObject> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct);
}

public interface IReceiptOcr
{
    Task<ReceiptExtraction> ExtractAsync(Stream content, string fileName, string contentType, CancellationToken ct, string? sourceUrl = null);
}

public sealed class CloudinaryReceiptStorage(HttpClient http) : IReceiptStorage
{
    public async Task<StoredObject> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct)
    {
        var cloud = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
        var preset = Environment.GetEnvironmentVariable("CLOUDINARY_UPLOAD_PRESET");
        if (string.IsNullOrWhiteSpace(cloud) || string.IsNullOrWhiteSpace(preset))
            throw new InvalidOperationException("Cloudinary environment configuration is missing.");
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(preset), "upload_preset");
        using var response = await http.PostAsync($"https://api.cloudinary.com/v1_1/{cloud}/auto/upload", form, ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        return new(json.RootElement.GetProperty("secure_url").GetString()!, json.RootElement.GetProperty("public_id").GetString()!);
    }
}

public sealed class OcrSpaceReceiptOcr(HttpClient http) : IReceiptOcr
{
    private const int DirectUploadLimitBytes = 900_000;

    public async Task<ReceiptExtraction> ExtractAsync(Stream content, string fileName, string contentType, CancellationToken ct, string? sourceUrl = null)
    {
        var key = Environment.GetEnvironmentVariable("OCR_SPACE_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("OCR.Space environment configuration is missing.");
        var length = content.CanSeek ? content.Length : 0;
        if (!string.IsNullOrWhiteSpace(sourceUrl) && (length <= 0 || length > DirectUploadLimitBytes))
        {
            var fromUrl = await ParseAsync(await PostAsync(key, sourceUrl, null, fileName, contentType, ct), ct);
            if (fromUrl is not null) return fromUrl;
        }

        if (content.CanSeek) content.Position = 0;
        var fromFile = await ParseAsync(await PostAsync(key, null, content, fileName, contentType, ct), ct);
        if (fromFile is not null) return fromFile;
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            var retry = await ParseAsync(await PostAsync(key, sourceUrl, null, fileName, contentType, ct), ct);
            if (retry is not null) return retry;
        }
        return new(null, null, null, null, 0, true, null);
    }

    private async Task<HttpResponseMessage> PostAsync(string key, string? url, Stream? content, string fileName, string contentType, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ocr.space/parse/image");
        request.Headers.Add("apikey", key);
        var form = new MultipartFormDataContent();
        if (!string.IsNullOrWhiteSpace(url))
            form.Add(new StringContent(url), "url");
        else if (content is not null)
        {
            var file = new StreamContent(content);
            file.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            form.Add(file, "file", fileName);
        }
        form.Add(new StringContent("true"), "isTable");
        form.Add(new StringContent("2"), "OCREngine");
        request.Content = form;
        return await http.SendAsync(request, ct);
    }

    private static async Task<ReceiptExtraction?> ParseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode) return null;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            var root = json.RootElement;
            if (root.TryGetProperty("OCRExitCode", out var exit) && exit.TryGetInt32(out var code) && code != 1)
                return null;
            string? text = null;
            if (root.TryGetProperty("ParsedResults", out var parsed) && parsed.ValueKind == JsonValueKind.Array && parsed.GetArrayLength() > 0
                && parsed[0].TryGetProperty("ParsedText", out var parsedText))
                text = parsedText.GetString();
            if (string.IsNullOrWhiteSpace(text)) return new(null, null, null, null, 0.20m, true, text);
            return ReceiptTextParser.Enrich(new(null, null, null, null, 0.50m, true, text));
        }
    }
}

public sealed class FakeReceiptStorage : IReceiptStorage
{
    public Task<StoredObject> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct)
        => Task.FromResult(new StoredObject($"https://local.invalid/receipts/{Uri.EscapeDataString(fileName)}", $"fake-{fileName}"));
}

public sealed class FakeReceiptOcr : IReceiptOcr
{
    public Task<ReceiptExtraction> ExtractAsync(Stream content, string fileName, string contentType, CancellationToken ct, string? sourceUrl = null)
        => Task.FromResult(new ReceiptExtraction("LOCAL TEST VENDOR", 12.34m,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "USD", 0.99m, false,
            "LOCAL TEST VENDOR\nTotal 12.34 USD\nDate 2026-01-01"));
}

public interface IReceiptService
{
    Task<ReceiptDto> UploadAsync(int claimId, Stream content, string fileName, string contentType, long length, int actorId, CancellationToken ct);
    Task<ReceiptDto> CorrectAsync(int claimId, int receiptId, ReceiptCorrectionDto input, int actorId, CancellationToken ct);
}

public sealed class ReceiptService(AppDbContext db, IReceiptStorage storage, IReceiptOcr ocr, IClaimReviewClient? reviews = null) : IReceiptService
{
    private const long MaxBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = ["image/jpeg", "image/png", "application/pdf"];

    public async Task<ReceiptDto> UploadAsync(int claimId, Stream content, string fileName, string contentType, long length, int actorId, CancellationToken ct)
    {
        var claim = await OwnedClaim(claimId, actorId, ct);
        if (claim.Status is not (ClaimStatus.Draft or ClaimStatus.NeedsCorrection))
            throw new InvalidOperationException("Receipts can only be added to draft or correction claims.");
        if (length <= 0 || length > MaxBytes) throw new ArgumentException("Receipt must be between 1 byte and 10 MB.");
        if (!AllowedTypes.Contains(contentType.ToLowerInvariant())) throw new ArgumentException("Only JPEG, PNG and PDF receipts are supported.");

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length != length || buffer.Length > MaxBytes) throw new ArgumentException("Receipt length is invalid.");
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (await db.Receipts.AnyAsync(r => r.ExpenseClaimId == claimId && r.Sha256 == hash, ct))
            throw new InvalidOperationException("This receipt has already been uploaded to the claim.");

        await using var uploadStream = new MemoryStream(bytes, writable: false);
        var stored = await storage.UploadAsync(uploadStream, Path.GetFileName(fileName), contentType, ct);
        await using var ocrStream = new MemoryStream(bytes, writable: false);
        ReceiptExtraction extraction;
        try
        {
            extraction = await ocr.ExtractAsync(ocrStream, Path.GetFileName(fileName), contentType, ct, CompactOcrUrl(stored, contentType));
        }
        catch (Exception)
        {
            extraction = new ReceiptExtraction(null, null, null, null, 0, true, null);
        }
        extraction = ReceiptTextParser.Enrich(extraction);
        if (!string.IsNullOrWhiteSpace(extraction.RawText) && reviews is not null)
        {
            var ai = await reviews.ExtractAsync(extraction.RawText, ct);
            if (ai is not null)
                extraction = MergeAi(extraction, ai);
        }
        var receipt = new Receipt
        {
            ExpenseClaimId = claimId, StorageUrl = stored.Url, PublicId = stored.PublicId,
            FileName = Path.GetFileName(fileName), ContentType = contentType.ToLowerInvariant(),
            SizeBytes = length, Sha256 = hash, ProcessingStatus = extraction.RequiresManualReview
                ? ReceiptProcessingStatus.NeedsReview : ReceiptProcessingStatus.Processed,
            ExtractedVendor = extraction.Vendor, ExtractedAmount = extraction.Amount,
            ExtractedDate = UtcDate.ToUtc(extraction.Date), ExtractedCurrency = extraction.Currency?.ToUpperInvariant(),
            ExtractedText = extraction.RawText,
            Confidence = extraction.Confidence, RequiresManualReview = extraction.RequiresManualReview
        };
        db.Add(receipt);
        await db.SaveChangesAsync(ct);
        return Map(receipt);
    }

    public async Task<ReceiptDto> CorrectAsync(int claimId, int receiptId, ReceiptCorrectionDto input, int actorId, CancellationToken ct)
    {
        _ = await OwnedClaim(claimId, actorId, ct);
        var receipt = await db.Receipts.SingleOrDefaultAsync(r => r.ReceiptId == receiptId && r.ExpenseClaimId == claimId, ct)
            ?? throw new KeyNotFoundException("Receipt not found.");
        receipt.ExtractedVendor = input.Vendor?.Trim();
        receipt.ExtractedAmount = input.Amount;
        receipt.ExtractedDate = UtcDate.ToUtc(input.PurchaseDate);
        receipt.ExtractedCurrency = input.Currency?.ToUpperInvariant();
        receipt.RequiresManualReview = false;
        receipt.ProcessingStatus = ReceiptProcessingStatus.Processed;
        receipt.CorrectedAt = DateTime.UtcNow;
        receipt.CorrectedByEmployeeId = actorId;
        await db.SaveChangesAsync(ct);
        return Map(receipt);
    }

    private static ReceiptExtraction MergeAi(ReceiptExtraction current, ClaimReviewReceipt ai)
    {
        var vendor = ReceiptTextParser.IsAddressLine(ai.Vendor) ? current.Vendor : ai.Vendor ?? current.Vendor;
        var amount = ai.Amount ?? current.Amount;
        var date = ai.PurchaseDate ?? current.Date;
        var currency = string.IsNullOrWhiteSpace(ai.Currency) ? current.Currency : ai.Currency;
        var complete = !string.IsNullOrWhiteSpace(vendor) && amount is not null && date is not null
            && !string.IsNullOrWhiteSpace(currency);
        return current with
        {
            Vendor = vendor,
            Amount = amount,
            Date = date,
            Currency = currency,
            Confidence = ai.Confidence ?? current.Confidence,
            RequiresManualReview = ai.RequiresManualReview && !complete
        };
    }

    private static string? CompactOcrUrl(StoredObject stored, string contentType)
    {
        if (string.IsNullOrWhiteSpace(stored.PublicId)) return stored.Url;
        var cloud = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
        if (string.IsNullOrWhiteSpace(cloud) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return stored.Url;
        return $"https://res.cloudinary.com/{cloud}/image/upload/c_limit,w_1600,q_auto,f_jpg/{stored.PublicId}";
    }

    private async Task<ExpenseClaim> OwnedClaim(int id, int actorId, CancellationToken ct)
    {
        var claim = await db.ExpenseClaims.SingleOrDefaultAsync(c => c.ExpenseClaimId == id && c.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Claim not found.");
        if (claim.EmployeeId != actorId) throw new UnauthorizedAccessException("Claim is owned by another employee.");
        return claim;
    }

    private static ReceiptDto Map(Receipt r) => new(r.ReceiptId, r.ExpenseClaimId, r.StorageUrl, r.FileName,
        r.ContentType, r.SizeBytes, r.Sha256, r.ProcessingStatus, r.ExtractedVendor, r.ExtractedAmount,
        r.ExtractedDate, r.ExtractedCurrency, r.ExtractedText, r.Confidence, r.RequiresManualReview, r.CorrectedAt);
}
