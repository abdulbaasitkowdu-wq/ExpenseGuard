namespace ExpenseGuard.Api.Models;

public class Department
{
    public int DepartmentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();

    public ICollection<Employee> Employees { get; set; } = [];
    public ICollection<Policy> Policies { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
}