using FreshServiceLakeSync.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FreshServiceLakeSync.Services;

/// <summary>
/// Service for querying employee data from SQL Server Lake database
/// </summary>
public class SqlService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqlService> _logger;

    public SqlService(IConfiguration configuration, ILogger<SqlService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all active employees with their org/location details
    /// </summary>
    public async Task<List<Employee>> GetActiveEmployeesAsync()
    {
        var employees = new List<Employee>();
        var connectionString = _configuration["SqlConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("SqlConnectionString is not configured");
        }

        const string query = @"
            select
                emps.EmployeeCode,
                emps.Email,
                CASE WHEN emps.CommonName IS NOT NULL THEN emps.CommonName ELSE emps.FirstName END AS Fname,
                CASE WHEN emps.PreferredLastName IS NOT NULL THEN emps.PreferredLastName ELSE emps.LastName END AS Lname,
                wa.JobTitle,
                depts.DeptName DepartmentName,
                ofc.DivisionName,
                ofc.RegionName,
                ofc.TeamName,
                ofc.OfficeCode,
                ofc.SiteCode OfficeSiteCode,
                ofc.OfficeName,
                ofc.Address1 + ' ' + ofc.Address2 + ', ' + ofc.City + ', ' + Ofc.StateAbbrev + ' ' + ofc.PostalCode OfficeAddress
            from Lake.sd.Employees emps
                left join sd.Departments depts
                    on depts.DeptCode = emps.PrimaryDeptCode
                inner join sd.WorkAssignments wa
                    on wa.EmployeeCode = emps.EmployeeCode
                   and wa.IsPrimary = 1
                inner join Lake.extenders.Offices ofc
                    on ofc.OfficeCode = wa.OfficeCode
            where emps.IsActive = 1
            order by emps.firstname";

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 120; // 2 minutes timeout

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                employees.Add(new Employee
                {
                    EmployeeCode = reader.GetString(reader.GetOrdinal("EmployeeCode")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    Fname = reader.GetString(reader.GetOrdinal("Fname")),
                    Lname = reader.GetString(reader.GetOrdinal("Lname")),
                    JobTitle = reader.IsDBNull(reader.GetOrdinal("JobTitle")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("JobTitle")),
                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("DepartmentName")),
                    DivisionName = reader.IsDBNull(reader.GetOrdinal("DivisionName")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("DivisionName")),
                    RegionName = reader.IsDBNull(reader.GetOrdinal("RegionName")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("RegionName")),
                    TeamName = reader.IsDBNull(reader.GetOrdinal("TeamName")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("TeamName")),
                    OfficeCode = reader.IsDBNull(reader.GetOrdinal("OfficeCode")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("OfficeCode")),
                    OfficeSiteCode = reader.IsDBNull(reader.GetOrdinal("OfficeSiteCode")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("OfficeSiteCode")),
                    OfficeName = reader.IsDBNull(reader.GetOrdinal("OfficeName")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("OfficeName")),
                    OfficeAddress = reader.IsDBNull(reader.GetOrdinal("OfficeAddress")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("OfficeAddress"))
                });
            }

            _logger.LogInformation("Retrieved {Count} active employees from SQL Server", employees.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employees from SQL Server");
            throw;
        }

        return employees;
    }
}
