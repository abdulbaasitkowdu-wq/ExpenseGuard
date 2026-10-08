namespace ExpenseGuard.Api.Models;

public class Employee
{
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; }
    public int RoleId { get; set; }
    public int? ManagerId { get; set; }
    public int DepartmentId { get; set; }
    public int? DesignationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;
    public Employee? Manager { get; set; }
    public Department Department { get; set; } = null!;
    public Designation? Designation { get; set; }
    public ICollection<Employee> DirectReports { get; set; } = [];
    public ICollection<ExpenseClaim> ExpenseClaims { get; set; } = [];
    public ICollection<PurchaseRequest> PurchaseRequests { get; set; } = [];
}
