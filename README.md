# FreshServiceLakeSync

Azure Functions application that synchronizes employee organizational data from SQL Server to FreshService requester custom fields.

## Overview

This application provides automated and on-demand synchronization of employee data from a SQL Server Lake database to FreshService. It reads active employees and their organizational details (division, team, region, location) and updates the corresponding FreshService requester custom fields.

**Key Features:**
- ✅ Scheduled sync via Timer trigger (configurable CRON schedule)
- ✅ On-demand sync via HTTP trigger endpoint
- ✅ Updates only when data has actually changed (no unnecessary API calls)
- ✅ Never creates or deletes FreshService requesters - only updates existing ones
- ✅ Email-based matching between employees and requesters
- ✅ Comprehensive logging and error handling
- ✅ Rate limiting and pagination support

## Technology Stack

- **Azure Functions v4** with **.NET 8 (isolated worker process)**
- **C#** with nullable reference types enabled
- **Microsoft.Data.SqlClient** for SQL Server connectivity
- **System.Text.Json** for JSON serialization
- **Application Insights** for monitoring and telemetry

## Architecture

### Components

1. **Models** (`/Models`)
   - `Employee.cs` - SQL Server employee data model
   - `FreshServiceRequester.cs` - FreshService API models
   - `SyncResult.cs` - Sync operation result tracking

2. **Services** (`/Services`)
   - `SqlService.cs` - Queries SQL Server for active employees
   - `FreshServiceClient.cs` - FreshService API client with authentication and pagination
   - `SyncService.cs` - Core business logic for matching and syncing data

3. **Functions** (`/Functions`)
   - `TimerSyncFunction.cs` - Scheduled sync execution
   - `HttpSyncFunction.cs` - On-demand HTTP-triggered sync

### Sync Process Flow

1. **Retrieve Employees**: Query SQL Server for all active employees with org/location details
2. **Retrieve Requesters**: Fetch all FreshService requesters via API (with pagination)
3. **Match by Email**: Create case-insensitive email lookup to match employees with requesters
4. **Compare Data**: Check if custom fields need updating (skip if unchanged)
5. **Update FreshService**: PUT request to update requester custom fields
6. **Report Results**: Return summary with counts and any errors

## Configuration

### Application Settings

Configure these in `local.settings.json` (local development) or Azure Function App Settings (production):

| Setting | Description | Example |
|---------|-------------|---------|
| `SqlConnectionString` | SQL Server connection string | `Server=myserver.database.windows.net;Database=mydb;User Id=myuser;Password=mypass;Encrypt=true;` |
| `FreshService:BaseUrl` | FreshService domain URL | `https://yourcompany.freshservice.com` |
| `FreshService:ApiKey` | FreshService API key | `your-api-key-here` |
| `SyncSchedule` | CRON expression for timer trigger | `0 0 */6 * * *` (every 6 hours) |

### CRON Schedule Examples

- `0 0 */6 * * *` - Every 6 hours
- `0 0 * * * *` - Every hour
- `0 0 0 * * *` - Daily at midnight UTC
- `0 0 9 * * 1-5` - Weekdays at 9 AM UTC

### SQL Server Query

The application uses the following query to retrieve employee data:

```sql
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
order by emps.firstname
```

### FreshService Custom Fields

The following custom fields are synced to FreshService requester profiles:

- `employee_id` - Employee code from SQL
- `division` - Division name
- `team` - Team name  
- `region` - Region name
- `location` - Office/location name

**Note:** These custom field keys must exist in your FreshService instance. Create them in FreshService admin settings before running the sync.

## Local Development

### Prerequisites

- .NET 8 SDK
- Azure Functions Core Tools v4
- Access to SQL Server database
- FreshService API key

### Setup

1. Clone the repository:
```bash
git clone https://github.com/atazi444/FreshServiceLakeSync.git
cd FreshServiceLakeSync
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Update `local.settings.json` with your configuration values

4. Run locally:
```bash
func start
```

### Testing Locally

**Timer Function:**
The timer function will execute based on the schedule. You can trigger it immediately using:
```bash
# Timer functions run automatically on schedule when func start is running
```

**HTTP Function:**
```bash
# Trigger sync on demand
curl -X POST http://localhost:7071/api/sync-freshservice-requesters
```

## Deployment

### Deploy to Azure

1. Create an Azure Function App:
```bash
az functionapp create --resource-group <resource-group> \
  --consumption-plan-location <location> \
  --runtime dotnet-isolated --functions-version 4 \
  --name <function-app-name> --storage-account <storage-account>
```

2. Configure application settings:
```bash
az functionapp config appsettings set --name <function-app-name> \
  --resource-group <resource-group> \
  --settings "SqlConnectionString=<connection-string>" \
  "FreshService:BaseUrl=<base-url>" \
  "FreshService:ApiKey=<api-key>" \
  "SyncSchedule=0 0 */6 * * *"
```

3. Deploy the function:
```bash
func azure functionapp publish <function-app-name>
```

### Azure Configuration Best Practices

- Store sensitive values (connection strings, API keys) in **Azure Key Vault**
- Use **Managed Identity** for SQL Server authentication where possible
- Enable **Application Insights** for monitoring
- Configure **alerts** for failed sync operations
- Set appropriate **timeout values** for long-running queries

## API Reference

### HTTP Sync Endpoint

**Endpoint:** `POST /api/sync-freshservice-requesters`  
**Authorization:** Function key required  
**Content-Type:** `application/json`

**Response (Success):**
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

**Response (Error):**
```json
{
  "success": false,
  "timestamp": "2026-02-06T23:00:00Z",
  "error": "Error message details"
}
```

## Monitoring

### Application Insights Queries

**Track sync executions:**
```kusto
traces
| where message contains "sync completed"
| project timestamp, message
| order by timestamp desc
```

**Identify errors:**
```kusto
traces
| where severityLevel >= 3
| project timestamp, message, severityLevel
| order by timestamp desc
```

### Key Metrics to Monitor

- Sync execution duration
- Number of employees processed
- Number of requesters updated
- API call failures
- SQL query performance

## Troubleshooting

### Common Issues

**1. SQL Connection Fails**
- Verify connection string
- Check firewall rules allow Azure Functions IP
- Ensure SQL credentials are correct

**2. FreshService API Errors**
- Verify API key is valid
- Check FreshService rate limits
- Ensure custom fields exist in FreshService

**3. No Requesters Updated**
- Verify email addresses match between systems
- Check custom field keys match FreshService configuration
- Review Application Insights logs for matching details

## Security Considerations

- API keys and connection strings should be stored in Azure Key Vault
- Use Managed Identity for Azure resource authentication where possible
- Implement IP restrictions on Azure Function App if needed
- Enable HTTPS only
- Regular security audits and dependency updates

## Contributing

Contributions are welcome! Please follow these guidelines:
1. Create a feature branch
2. Make your changes
3. Test thoroughly
4. Submit a pull request

## License

This project is licensed under the MIT License.

## Support

For issues, questions, or suggestions, please open an issue in the GitHub repository.