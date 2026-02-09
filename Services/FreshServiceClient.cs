using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FreshServiceLakeSync.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FreshServiceLakeSync.Services;

/// <summary>
/// Service for interacting with FreshService API
/// </summary>
public class FreshServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FreshServiceClient> _logger;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public FreshServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<FreshServiceClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _baseUrl = _configuration["FreshService:BaseUrl"] ?? throw new InvalidOperationException("FreshService:BaseUrl is not configured");
        _apiKey = _configuration["FreshService:ApiKey"] ?? throw new InvalidOperationException("FreshService:ApiKey is not configured");

        // Set up authentication (Basic Auth with API key)
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_apiKey}:X"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Retrieves all requesters from FreshService (with pagination)
    /// </summary>
    public async Task<List<FreshServiceRequester>> GetAllRequestersAsync()
    {
        var allRequesters = new List<FreshServiceRequester>();
        var page = 1;
        const int perPage = 100; // FreshService max per page

        try
        {
            while (true)
            {
                var url = $"{_baseUrl}/api/v2/requesters?page={page}&per_page={perPage}";
                _logger.LogDebug("Fetching requesters page {Page}", page);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<FreshServiceRequestersResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Requesters == null || result.Requesters.Count == 0)
                {
                    break;
                }

                allRequesters.AddRange(result.Requesters);

                // Check if we've retrieved all requesters
                if (allRequesters.Count >= result.Total)
                {
                    break;
                }

                page++;

                // Respect rate limiting - FreshService has rate limits
                await Task.Delay(200); // Small delay between requests
            }

            _logger.LogInformation("Retrieved {Count} requesters from FreshService", allRequesters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving requesters from FreshService");
            throw;
        }

        return allRequesters;
    }

    /// <summary>
    /// Updates a requester's custom fields in FreshService
    /// </summary>
    public async Task<bool> UpdateRequesterCustomFieldsAsync(long requesterId, Dictionary<string, object?> customFields)
    {
        try
        {
            var url = $"{_baseUrl}/api/v2/requesters/{requesterId}";
            var request = new FreshServiceRequesterUpdateRequest
            {
                CustomFields = customFields
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, httpContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully updated requester {RequesterId}", requesterId);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to update requester {RequesterId}: {StatusCode} - {Error}", 
                    requesterId, response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating requester {RequesterId}", requesterId);
            return false;
        }
    }
}
