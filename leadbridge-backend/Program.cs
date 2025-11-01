using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        // Twilio Service
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

            return new TwilioService(accountSid, authToken, twilioPhone, callbackBaseUrl);
        });

        // Logging
        services.AddLogging();
    })
    .Build();

host.Run();
