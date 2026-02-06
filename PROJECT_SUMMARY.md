# Project Summary

## Overview

This Azure Functions v4 application (.NET 8 isolated) synchronizes employee organizational data from a SQL Server Lake database to FreshService requester custom fields.

## Implementation Details

### Architecture

**Technology Stack:**
- Azure Functions v4
- .NET 8 (isolated worker process)
- C# with nullable reference types
- Microsoft.Data.SqlClient for SQL connectivity
- System.Text.Json for JSON serialization
- Application Insights for monitoring

**Triggers:**
1. **Timer Trigger** - Runs on configurable CRON schedule (default: every 6 hours)
2. **HTTP Trigger** - On-demand sync via POST endpoint

### Project Structure

```
FreshServiceLakeSync/
├── Models/
│   ├── Employee.cs                    # SQL employee data model
│   ├── FreshServiceRequester.cs       # FreshService API models
│   └── SyncResult.cs                  # Sync result tracking
├── Services/
│   ├── SqlService.cs                  # SQL Server data access
│   ├── FreshServiceClient.cs          # FreshService API client
│   └── SyncService.cs                 # Core sync business logic
├── Functions/
│   ├── TimerSyncFunction.cs           # Scheduled sync
│   └── HttpSyncFunction.cs            # On-demand sync
├── Program.cs                          # DI configuration
├── host.json                           # Function host config
├── local.settings.json.template        # Config template
├── README.md                           # Main documentation
├── DEPLOYMENT.md                       # Azure deployment guide
└── FreshServiceLakeSync.csproj        # Project file
```

### Sync Process Flow

1. **Retrieve Data**: Query SQL Server for all active employees
2. **Fetch Requesters**: Get all FreshService requesters (with pagination)
3. **Match**: Create email-based lookup (case-insensitive)
4. **Compare**: Check if custom fields need updating
5. **Update**: PUT request only when data has changed
6. **Report**: Return summary with counts and errors

### Configuration

Required application settings:

| Setting | Description |
|---------|-------------|
| `SqlConnectionString` | SQL Server connection string |
| `FreshService:BaseUrl` | FreshService domain URL |
| `FreshService:ApiKey` | FreshService API key |
| `SyncSchedule` | CRON expression for timer (e.g., `0 0 */6 * * *`) |

### Custom Fields Synced

The following fields are synced from SQL to FreshService requester custom fields:

- `employee_id` → Employee code
- `division` → Division name
- `team` → Team name
- `region` → Region name
- `location` → Office name

**Note:** These custom fields must be created in FreshService before running the sync.

### API Endpoint

**HTTP Trigger:**
- **URL:** `POST /api/sync-freshservice-requesters`
- **Auth:** Function key required
- **Response:** JSON with sync summary and details

Example response:
```json
{
  "success": true,
  "timestamp": "2026-02-06T23:00:00Z",
  "summary": "Processed: 150 employees, 200 requesters | Matched: 145 | Updated: 23 | Skipped: 122 | Failed: 0",
  "details": {
    "totalEmployees": 150,
    "totalRequesters": 200,
    "matched": 145,
    "updated": 23,
    "skipped": 122,
    "failed": 0
  },
  "errors": []
}
```

### Key Features

✅ **Dual Triggers**: Timer (scheduled) and HTTP (on-demand)  
✅ **Shared Logic**: Both triggers use the same sync service  
✅ **Smart Updates**: Only updates when data has changed  
✅ **Safe Operations**: Never creates or deletes requesters  
✅ **Email Matching**: Case-insensitive email-based matching  
✅ **Rate Limiting**: Respects FreshService API rate limits  
✅ **Pagination**: Handles large datasets from both sources  
✅ **Error Handling**: Comprehensive logging and error tracking  
✅ **Monitoring Ready**: Application Insights integration  
✅ **Secure**: No hardcoded secrets, uses configuration

### Security

- ✅ All dependencies scanned - no vulnerabilities found
- ✅ CodeQL security analysis passed - no alerts
- ✅ Code review completed - no issues
- ✅ Secrets stored in configuration (Azure Key Vault recommended)
- ✅ SQL parameterized queries (no SQL injection)
- ✅ HTTPS API calls with authentication

### Deployment

The application can be deployed to Azure using:
1. Azure CLI commands (see DEPLOYMENT.md)
2. Azure DevOps pipelines
3. GitHub Actions
4. Visual Studio publish

**Recommended Production Setup:**
- Use Azure Key Vault for secrets
- Enable Managed Identity for SQL authentication
- Configure Application Insights for monitoring
- Set up alerts for failures
- Use consumption or premium plan based on load

### Testing

**Local Testing:**
```bash
# Start functions locally
func start

# Trigger HTTP endpoint
curl -X POST http://localhost:7071/api/sync-freshservice-requesters
```

**Azure Testing:**
```bash
# Get function key and trigger
curl -X POST "https://<function-app>.azurewebsites.net/api/sync-freshservice-requesters?code=<key>"
```

### Monitoring

**Key Metrics:**
- Sync execution count
- Duration per sync
- Number of records processed
- Update/skip/fail counts
- API call latency
- Error rates

**Application Insights Queries:**
```kusto
// View sync results
traces
| where message contains "sync completed"
| project timestamp, message

// Track errors
traces
| where severityLevel >= 3
| project timestamp, message, severityLevel
```

### Maintenance

**Regular Tasks:**
1. Monitor sync execution logs
2. Review error rates and patterns
3. Update dependencies quarterly
4. Verify FreshService custom fields exist
5. Check SQL query performance
6. Review and adjust sync schedule

**Troubleshooting:**
- Check Application Insights for errors
- Verify connection strings and API keys
- Ensure SQL firewall allows Azure services
- Confirm FreshService custom fields exist
- Review rate limiting and quotas

## Files Created

1. **FreshServiceLakeSync.csproj** - Project configuration with all dependencies
2. **Program.cs** - Application startup and DI configuration
3. **Models/** - Data models for SQL, FreshService, and results
4. **Services/** - Business logic and API clients
5. **Functions/** - Timer and HTTP trigger implementations
6. **README.md** - Comprehensive user documentation
7. **DEPLOYMENT.md** - Azure deployment instructions
8. **local.settings.json.template** - Configuration template
9. **.gitignore** - Proper .NET gitignore (pre-existing)

## Next Steps

1. **Local Setup**: Copy `local.settings.json.template` to `local.settings.json` and configure
2. **Test Locally**: Run `func start` and test both triggers
3. **Deploy**: Follow DEPLOYMENT.md to deploy to Azure
4. **Configure**: Set up custom fields in FreshService
5. **Monitor**: Watch first sync execution and verify results

## Support

For questions or issues, refer to:
- README.md for usage and configuration
- DEPLOYMENT.md for Azure setup
- Application Insights logs for troubleshooting
