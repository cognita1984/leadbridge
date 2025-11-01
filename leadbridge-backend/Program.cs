using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Data.Tables;
using Azure.Communication.CallAutomation;
using LeadBridge.Services;
using LeadBridge.Storage;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Azure Table Storage
        services.AddSingleton<ITableClientFactory, TableClientFactory>();

        // Azure Communication Services
        services.AddSingleton<IAcsService>(provider =>
        {
            var connectionString = Environment.GetEnvironmentVariable("ACS_CONNECTION_STRING")
                ?? throw new InvalidOperationException("ACS_CONNECTION_STRING not configured");

            var callbackUri = Environment.GetEnvironmentVariable("ACS_CALLBACK_URI")
                ?? throw new InvalidOperationException("ACS_CALLBACK_URI not configured");

            return new AcsService(connectionString, callbackUri);
        });

        // Logging
        services.AddLogging();
    })
    .Build();

host.Run();
