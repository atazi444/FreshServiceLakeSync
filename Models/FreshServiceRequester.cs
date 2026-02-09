using System.Text.Json.Serialization;

namespace FreshServiceLakeSync.Models;

/// <summary>
/// Represents a FreshService requester
/// </summary>
public class FreshServiceRequester
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("primary_email")]
    public string PrimaryEmail { get; set; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("job_title")]
    public string? JobTitle { get; set; }

    [JsonPropertyName("department_names")]
    public List<string>? DepartmentNames { get; set; }

    [JsonPropertyName("custom_fields")]
    public Dictionary<string, object?>? CustomFields { get; set; }
}

/// <summary>
/// API response wrapper for FreshService requesters
/// </summary>
public class FreshServiceRequestersResponse
{
    [JsonPropertyName("requesters")]
    public List<FreshServiceRequester> Requesters { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// API request wrapper for updating a FreshService requester
/// </summary>
public class FreshServiceRequesterUpdateRequest
{
    [JsonPropertyName("custom_fields")]
    public Dictionary<string, object?> CustomFields { get; set; } = new();
}
