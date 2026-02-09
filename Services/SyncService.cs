using FreshServiceLakeSync.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FreshServiceLakeSync.Services;

/// <summary>
/// Core business logic service for syncing employee data to FreshService
/// </summary>
public class SyncService
{
    private readonly SqlService _sqlService;
    private readonly FreshServiceClient _freshServiceClient;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        SqlService sqlService,
        FreshServiceClient freshServiceClient,
        ILogger<SyncService> logger)
    {
        _sqlService = sqlService;
        _freshServiceClient = freshServiceClient;
        _logger = logger;
    }

    /// <summary>
    /// Executes the full sync process: retrieve employees, retrieve requesters, match and update
    /// </summary>
    public async Task<SyncResult> ExecuteSyncAsync()
    {
        var result = new SyncResult();

        try
        {
            _logger.LogInformation("Starting FreshService sync process");

            // Step 1: Get all active employees from SQL Server
            _logger.LogInformation("Retrieving active employees from SQL Server");
            var employees = await _sqlService.GetActiveEmployeesAsync();
            result.TotalEmployees = employees.Count;

            if (employees.Count == 0)
            {
                _logger.LogWarning("No active employees found in SQL Server");
                return result;
            }

            // Step 2: Get all requesters from FreshService
            _logger.LogInformation("Retrieving requesters from FreshService");
            var requesters = await _freshServiceClient.GetAllRequestersAsync();
            result.TotalRequesters = requesters.Count;

            if (requesters.Count == 0)
            {
                _logger.LogWarning("No requesters found in FreshService");
                return result;
            }

            // Step 3: Create a lookup dictionary for employees by email (case-insensitive)
            var employeeLookup = employees
                .Where(e => !string.IsNullOrWhiteSpace(e.Email))
                .ToDictionary(e => e.Email.Trim().ToLowerInvariant(), e => e, StringComparer.OrdinalIgnoreCase);

            // Step 4: Match requesters with employees and sync data
            foreach (var requester in requesters)
            {
                if (string.IsNullOrWhiteSpace(requester.PrimaryEmail))
                {
                    continue;
                }

                var email = requester.PrimaryEmail.Trim().ToLowerInvariant();
                if (!employeeLookup.TryGetValue(email, out var employee))
                {
                    // No matching employee found - skip this requester
                    continue;
                }

                result.Matched++;

                // Build custom fields from employee data
                var newCustomFields = BuildCustomFields(employee);

                // Check if update is needed by comparing with existing custom fields
                if (IsUpdateNeeded(requester.CustomFields, newCustomFields))
                {
                    _logger.LogDebug("Updating requester {Email} (ID: {Id})", requester.PrimaryEmail, requester.Id);
                    
                    var success = await _freshServiceClient.UpdateRequesterCustomFieldsAsync(requester.Id, newCustomFields);
                    
                    if (success)
                    {
                        result.Updated++;
                    }
                    else
                    {
                        result.Failed++;
                        result.Errors.Add($"Failed to update requester {requester.PrimaryEmail} (ID: {requester.Id})");
                    }

                    // Small delay to respect rate limits
                    await Task.Delay(100);
                }
                else
                {
                    result.Skipped++;
                    _logger.LogDebug("Skipping requester {Email} (ID: {Id}) - no changes detected", 
                        requester.PrimaryEmail, requester.Id);
                }
            }

            _logger.LogInformation("Sync completed: {Summary}", result.Summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync process");
            result.Errors.Add($"Sync process error: {ex.Message}");
            throw;
        }

        return result;
    }

    /// <summary>
    /// Builds custom fields dictionary from employee data
    /// </summary>
    private Dictionary<string, object?> BuildCustomFields(Employee employee)
    {
        return new Dictionary<string, object?>
        {
            ["employee_id"] = employee.EmployeeCode,
            ["division"] = employee.DivisionName,
            ["team"] = employee.TeamName,
            ["region"] = employee.RegionName,
            ["location"] = employee.OfficeName
        };
    }

    /// <summary>
    /// Determines if an update is needed by comparing existing and new custom fields
    /// </summary>
    private bool IsUpdateNeeded(Dictionary<string, object?>? existingFields, Dictionary<string, object?> newFields)
    {
        if (existingFields == null)
        {
            // No existing custom fields - update needed if we have any new fields
            return newFields.Any(kvp => kvp.Value != null);
        }

        // Check each field to see if it has changed
        foreach (var (key, newValue) in newFields)
        {
            var newValueStr = newValue?.ToString() ?? string.Empty;
            
            if (!existingFields.TryGetValue(key, out var existingValue))
            {
                // Field doesn't exist and we have a non-empty value - update needed
                if (!string.IsNullOrWhiteSpace(newValueStr))
                {
                    return true;
                }
                continue;
            }

            var existingValueStr = existingValue?.ToString() ?? string.Empty;
            
            // Compare values (case-insensitive string comparison)
            if (!string.Equals(existingValueStr, newValueStr, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
