namespace FreshServiceLakeSync.Models;

/// <summary>
/// Represents an employee from SQL Server Lake database
/// </summary>
public class Employee
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Fname { get; set; } = string.Empty;
    public string Lname { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public string? DivisionName { get; set; }
    public string? RegionName { get; set; }
    public string? TeamName { get; set; }
    public string? OfficeCode { get; set; }
    public string? OfficeSiteCode { get; set; }
    public string? OfficeName { get; set; }
    public string? OfficeAddress { get; set; }
}
