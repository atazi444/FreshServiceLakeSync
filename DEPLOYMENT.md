# Deployment Guide

## Prerequisites

- Azure subscription
- Azure CLI installed
- .NET 8 SDK installed
- Azure Functions Core Tools v4

## Step 1: Create Azure Resources

### 1.1 Create Resource Group

```bash
az group create \
  --name rg-freshservice-sync \
  --location eastus
```

### 1.2 Create Storage Account

```bash
az storage account create \
  --name stfreshservicesync \
  --resource-group rg-freshservice-sync \
  --location eastus \
  --sku Standard_LRS
```

### 1.3 Create Function App

```bash
az functionapp create \
  --resource-group rg-freshservice-sync \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --functions-version 4 \
  --name func-freshservice-sync \
  --storage-account stfreshservicesync \
  --os-type Linux
```

## Step 2: Configure Application Settings

### 2.1 Set SQL Connection String

```bash
az functionapp config appsettings set \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync \
  --settings "SqlConnectionString=Server=your-server.database.windows.net;Database=your-database;User Id=your-user;Password=your-password;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"
```

### 2.2 Set FreshService Configuration

```bash
az functionapp config appsettings set \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync \
  --settings "FreshService:BaseUrl=https://your-domain.freshservice.com" \
             "FreshService:ApiKey=your-api-key-here"
```

### 2.3 Set Sync Schedule

```bash
az functionapp config appsettings set \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync \
  --settings "SyncSchedule=0 0 */6 * * *"
```

## Step 3: Deploy the Function App

### 3.1 Build the Project

```bash
cd FreshServiceLakeSync
dotnet build --configuration Release
```

### 3.2 Publish to Azure

```bash
func azure functionapp publish func-freshservice-sync
```

## Step 4: Enable Application Insights

```bash
az monitor app-insights component create \
  --app func-freshservice-sync-insights \
  --location eastus \
  --resource-group rg-freshservice-sync \
  --application-type web

# Get the instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app func-freshservice-sync-insights \
  --resource-group rg-freshservice-sync \
  --query instrumentationKey -o tsv)

# Set it in the function app
az functionapp config appsettings set \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync \
  --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=$INSTRUMENTATION_KEY"
```

## Step 5: Configure SQL Server Firewall

### Option A: Allow Azure Services

```bash
az sql server firewall-rule create \
  --resource-group <your-sql-resource-group> \
  --server <your-sql-server> \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

### Option B: Use Managed Identity (Recommended)

1. Enable system-assigned managed identity:

```bash
az functionapp identity assign \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync
```

2. Grant SQL permissions to the managed identity (run in SQL Server):

```sql
CREATE USER [func-freshservice-sync] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [func-freshservice-sync];
```

3. Update connection string to use managed identity:

```bash
az functionapp config appsettings set \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync \
  --settings "SqlConnectionString=Server=your-server.database.windows.net;Database=your-database;Authentication=Active Directory Default;Encrypt=true;"
```

## Step 6: Configure FreshService Custom Fields

Before running the sync, ensure these custom fields exist in FreshService:

1. Log in to FreshService admin portal
2. Go to **Admin** > **Requester Fields**
3. Create the following custom fields:
   - `employee_id` (Text field)
   - `division` (Text field)
   - `team` (Text field)
   - `region` (Text field)
   - `location` (Text field)

## Step 7: Test the Deployment

### 7.1 Test HTTP Trigger

```bash
# Get the function key
FUNCTION_KEY=$(az functionapp keys list \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync \
  --query functionKeys.default -o tsv)

# Trigger the sync
curl -X POST "https://func-freshservice-sync.azurewebsites.net/api/sync-freshservice-requesters?code=$FUNCTION_KEY"
```

### 7.2 Monitor Execution

```bash
# Stream logs
func azure functionapp logstream func-freshservice-sync
```

Or view logs in Azure Portal:
- Go to Function App > Functions > HttpSyncFunction or TimerSyncFunction
- Click on "Monitor" tab
- View execution history and logs

## Step 8: Set Up Alerts (Optional)

Create an alert for failed executions:

```bash
az monitor metrics alert create \
  --name alert-sync-failures \
  --resource-group rg-freshservice-sync \
  --scopes $(az functionapp show --name func-freshservice-sync --resource-group rg-freshservice-sync --query id -o tsv) \
  --condition "count exceptions/count > 0" \
  --description "Alert when sync function fails"
```

## Using Azure Key Vault (Recommended for Production)

### 1. Create Key Vault

```bash
az keyvault create \
  --name kv-freshservice-sync \
  --resource-group rg-freshservice-sync \
  --location eastus
```

### 2. Store Secrets

```bash
az keyvault secret set \
  --vault-name kv-freshservice-sync \
  --name SqlConnectionString \
  --value "Server=your-server.database.windows.net;..."

az keyvault secret set \
  --vault-name kv-freshservice-sync \
  --name FreshServiceApiKey \
  --value "your-api-key-here"
```

### 3. Grant Function App Access

```bash
az keyvault set-policy \
  --name kv-freshservice-sync \
  --object-id $(az functionapp identity show --name func-freshservice-sync --resource-group rg-freshservice-sync --query principalId -o tsv) \
  --secret-permissions get list
```

### 4. Reference Secrets in App Settings

```bash
az functionapp config appsettings set \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync \
  --settings "SqlConnectionString=@Microsoft.KeyVault(SecretUri=https://kv-freshservice-sync.vault.azure.net/secrets/SqlConnectionString/)" \
             "FreshService:ApiKey=@Microsoft.KeyVault(SecretUri=https://kv-freshservice-sync.vault.azure.net/secrets/FreshServiceApiKey/)"
```

## Troubleshooting

### Check Function App Logs

```bash
az functionapp log tail \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync
```

### Verify Configuration

```bash
az functionapp config appsettings list \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync
```

### Restart Function App

```bash
az functionapp restart \
  --name func-freshservice-sync \
  --resource-group rg-freshservice-sync
```

## Clean Up (if needed)

```bash
az group delete \
  --name rg-freshservice-sync \
  --yes
```
