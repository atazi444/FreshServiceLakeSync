using FreshServiceLakeSync.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FreshServiceLakeSync.Functions;

/// <summary>
/// Timer-triggered function that runs the sync process on a schedule
/// </summary>
public class TimerSyncFunction
{
    private readonly SyncService _syncService;
    private readonly ILogger<TimerSyncFunction> _logger;

    public TimerSyncFunction(SyncService syncService, ILogger<TimerSyncFunction> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    [Function("TimerSyncFunction")]
    public async Task Run(
        [TimerTrigger("%SyncSchedule%")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Timer trigger function started at: {Time}", DateTime.UtcNow);
        
        if (timerInfo.ScheduleStatus != null)
        {
            _logger.LogInformation("Next timer schedule at: {NextSchedule}", timerInfo.ScheduleStatus.Next);
        }

        try
        {
            var result = await _syncService.ExecuteSyncAsync();
            _logger.LogInformation("Timer sync completed: {Summary}", result.Summary);
            
            if (result.Errors.Any())
            {
                _logger.LogWarning("Sync completed with {ErrorCount} errors", result.Errors.Count);
                foreach (var error in result.Errors.Take(10)) // Log first 10 errors
                {
                    _logger.LogWarning("Error: {Error}", error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Timer sync function failed");
            throw;
        }
    }
}
