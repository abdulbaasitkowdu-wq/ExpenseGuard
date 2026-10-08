namespace ExpenseGuard.Api.Models;

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public decimal ApprovalLimit { get; set; }
    public bool CanApprove { get; set; }
    public bool CanProcessPayments { get; set; }
    public bool CanManageRoles { get; set; }
    public string PermissionsJson { get; set; } = "[]";

    public ICollection<Employee> Employees { get; set; } = [];
}

public static class RoleNames
{
    public const string Employee = "Employee";
    public const string Manager = "Manager";
    public const string DepartmentHead = "DepartmentHead";
    public const string Finance = "Finance";
    public const string Admin = "Admin";

    public static readonly string[] All = [Employee, Manager, DepartmentHead, Finance, Admin];
}