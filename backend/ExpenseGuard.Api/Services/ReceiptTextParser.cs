using System.Globalization;
using System.Text.RegularExpressions;

namespace ExpenseGuard.Api.Services;

public static class ReceiptTextParser
{
    private static readonly Regex Money = new(
        @"\$\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?|\d+\.\d{2})",
        RegexOptions.Compiled);
    private static readonly Regex DateLabel = new(
        @"\bDate\s*:\s*([A-Za-z]+\s+\d{1,2},\s+\d{4}|\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}-\d{2}-\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] AddressHints =
    [
        "lane", "street", "st.", "road", "rd.", "avenue", "ave.", "blvd", "suite", "floor",
        "united states", "sri lanka", "email", "phone", "www.", "http", "fax", "zip"
    ];
    private static readonly string[] SkipLines =
    [
        "receipt", "bill to", "description", "subtotal", "thank you", "authorized signature",
        "payment method", "amount in words", "more clicks", "digital advertising"
    ];

    public static ReceiptExtraction Enrich(ReceiptExtraction current)
    {
        var text = current.RawText;
        if (string.IsNullOrWhiteSpace(text)) return current;

        var vendor = IsAddressLine(current.Vendor) || string.IsNullOrWhiteSpace(current.Vendor)
            ? GuessVendor(text) ?? current.Vendor
            : current.Vendor;
        var amount = current.Amount ?? GuessAmount(text);
        var date = current.Date ?? GuessDate(text);
        var currency = string.IsNullOrWhiteSpace(current.Currency) ? GuessCurrency(text) : current.Currency;
        var complete = !string.IsNullOrWhiteSpace(vendor) && amount is not null && date is not null
            && !string.IsNullOrWhiteSpace(currency);
        return current with
        {
            Vendor = vendor,
            Amount = amount,
            Date = date,
            Currency = currency,
            Confidence = complete ? Math.Max(current.Confidence, 0.86m) : current.Confidence,
            RequiresManualReview = !complete
        };
    }

    public static string? GuessVendor(string text)
    {
        string? branded = null;
        foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.Length < 3 || raw.Length > 80 || IsAddressLine(raw) || IsSkip(raw)) continue;
            if (Regex.IsMatch(raw, @"\b(LLC|Inc\.?|Ltd\.?|Limited|Media|Company|Corp\.?)\b", RegexOptions.IgnoreCase))
                return raw;
            branded ??= raw;
        }
        return branded;
    }

    public static decimal? GuessAmount(string text)
    {
        var labeled = Regex.Match(text, @"Total Paid\s*:?\s*\$?\s*([\d,]+(?:\.\d{2})?)", RegexOptions.IgnoreCase);
        if (!labeled.Success)
            labeled = Regex.Match(text, @"Grand Total\s*:?\s*\$?\s*([\d,]+(?:\.\d{2})?)", RegexOptions.IgnoreCase);
        if (labeled.Success && decimal.TryParse(labeled.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var labeledAmount))
            return labeledAmount;

        decimal? max = null;
        foreach (Match match in Money.Matches(text))
        {
            if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                continue;
            if (max is null || value > max) max = value;
        }
        return max;
    }

    public static DateTime? GuessDate(string text)
    {
        var labeled = DateLabel.Match(text);
        if (labeled.Success && DateTime.TryParse(labeled.Groups[1].Value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        return null;
    }

    public static string? GuessCurrency(string text)
        => Regex.IsMatch(text, @"\bUSD\b|\$", RegexOptions.IgnoreCase) ? "USD" : null;

    public static bool IsAddressLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var lower = value.ToLowerInvariant();
        if (AddressHints.Any(hint => lower.Contains(hint))) return true;
        return Regex.IsMatch(value, @"\d{5}(?:-\d{4})?\b") || Regex.IsMatch(value, @"^\d+\s");
    }

    private static bool IsSkip(string value)
    {
        var lower = value.ToLowerInvariant();
        return SkipLines.Any(skip => lower.StartsWith(skip, StringComparison.Ordinal));
    }
}
