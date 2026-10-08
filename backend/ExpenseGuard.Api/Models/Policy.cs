namespace ExpenseGuard.Api.Models;

public class Policy
{
    public int PolicyId { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string Category { get; set; } = string.Empty;
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string Currency { get; set; } = "LKR";
    public bool ReceiptRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int? DepartmentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Department? Department { get; set; }
    public ICollection<PolicyDesignation> Designations { get; set; } = [];
    public ICollection<PolicyEvaluation> Evaluations { get; set; } = [];
}
