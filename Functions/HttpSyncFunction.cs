using System.Net;
using FreshServiceLakeSync.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FreshServiceLakeSync.Functions;

/// <summary>
/// HTTP-triggered function that runs the sync process on demand
/// </summary>
public class HttpSyncFunction
{
    private readonly SyncService _syncService;
    private readonly ILogger<HttpSyncFunction> _logger;

    public HttpSyncFunction(SyncService syncService, ILogger<HttpSyncFunction> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    [Function("HttpSyncFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sync-freshservice-requesters")] HttpRequestData req)
    {
        _logger.LogInformation("HTTP trigger function received request at: {Time}", DateTime.UtcNow);

        try
        {
            var result = await _syncService.ExecuteSyncAsync();
            _logger.LogInformation("HTTP sync completed: {Summary}", result.Summary);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                timestamp = DateTime.UtcNow,
                summary = result.Summary,
                details = new
                {
                    totalEmployees = result.TotalEmployees,
                    totalRequesters = result.TotalRequesters,
                    matched = result.Matched,
                    updated = result.Updated,
                    skipped = result.Skipped,
                    failed = result.Failed
                },
                errors = result.Errors
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP sync function failed");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                success = false,
                timestamp = DateTime.UtcNow,
                error = ex.Message
            });

            return errorResponse;
        }
    }
}
