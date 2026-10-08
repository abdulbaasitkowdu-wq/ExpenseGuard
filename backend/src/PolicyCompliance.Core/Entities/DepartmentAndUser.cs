using PolicyCompliance.Core.Enums;

namespace PolicyCompliance.Core.Entities;

public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal MonthlyBudgetLimit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<ExpenseClaim> ExpenseClaims { get; set; } = new List<ExpenseClaim>();
    public ICollection<ExpensePolicy> Policies { get; set; } = new List<ExpensePolicy>();
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Employee;
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ExpenseClaim> ExpenseClaims { get; set; } = new List<ExpenseClaim>();
    public ICollection<ManagerReview> Reviews { get; set; } = new List<ManagerReview>();
}
