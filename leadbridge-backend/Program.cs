using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LeadBridge.Services;
using LeadBridge.Storage;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Azure Table Storage Factory
        services.AddSingleton<ITableClientFactory, TableClientFactory>();

        // Storage Services (Scoped for per-request instances)
        services.AddScoped<LeadStorageService>();
        services.AddScoped<CallEventStorageService>();

        // Twilio Service with ILogger injection
        services.AddSingleton<ITwilioService>(provider =>
        {
            var accountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID")
                ?? throw new InvalidOperationException("TWILIO_ACCOUNT_SID not configured");

            var authToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN")
                ?? throw new InvalidOperationException("TWILIO_AUTH_TOKEN not configured");

            var twilioPhone = Environment.GetEnvironmentVariable("TWILIO_PHONE_NUMBER")
                ?? throw new InvalidOperationException("TWILIO_PHONE_NUMBER not configured");

            var callbackBaseUrl = Environment.GetEnvironmentVariable("TWILIO_CALLBACK_BASE_URL")
                ?? throw new InvalidOperationException("TWILIO_CALLBACK_BASE_URL not configured");

            var logger = provider.GetRequiredService<ILogger<TwilioService>>();

            return new TwilioService(accountSid, authToken, twilioPhone, callbackBaseUrl, logger);
        });

        // Logging
        services.AddLogging();
    })
    .Build();

host.Run();
