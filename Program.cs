using FreshServiceLakeSync.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register services for dependency injection
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register HttpClient for FreshService API
builder.Services.AddHttpClient<FreshServiceClient>();

// Register application services
builder.Services.AddSingleton<SqlService>();
builder.Services.AddSingleton<SyncService>();

builder.Build().Run();
