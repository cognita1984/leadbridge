using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Twilio.TwiML;
using Twilio.TwiML.Voice;
using Microsoft.Extensions.Logging;
using System.Security;

namespace LeadBridge.Services;

public interface ITwilioService
{
    Task<(bool canCall, string reason, string? callSid)> InitiateNotificationCallAsync(
        string tradiePhone,
        string leadId,
        string customerName,
        string jobType,
        string location,
        int? dndStartHour = null,
        int? dndEndHour = null,
        string? description = null,
        string? budget = null,
        string? timing = null);

    string GenerateNotificationTwiML(
        string leadId,
        string customerName,
        string jobType,
        string location,
        string? description = null,
        string? budget = null,
        string? timing = null);

    bool ValidateWebhookSignature(string signature, string url, Dictionary<string, string> parameters);
}

public class TwilioService : ITwilioService
{
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _twilioPhone;
    private readonly string _callbackBaseUrl;
    private readonly ILogger<TwilioService> _logger;

    // Australian timezone offset (AEDT/AEST)
    private static readonly TimeZoneInfo AustralianTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");

    public TwilioService(
        string accountSid,
        string authToken,
        string twilioPhone,
        string callbackBaseUrl,
        ILogger<TwilioService> logger)
    {
        _accountSid = accountSid;
        _authToken = authToken;
        _twilioPhone = twilioPhone;
        _callbackBaseUrl = callbackBaseUrl;
        _logger = logger;

        TwilioClient.Init(_accountSid, _authToken);
    }

    /// <summary>
    /// Sanitize input for TwiML to prevent XML injection
    /// </summary>
    private static string SanitizeForTwiML(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return SecurityElement.Escape(input);
    }

    /// <summary>
    /// Check if current time falls within DND hours
    /// </summary>
    private bool IsWithinDndHours(int? dndStartHour, int? dndEndHour)
    {
        if (!dndStartHour.HasValue || !dndEndHour.HasValue)
            return false;

        var australianNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AustralianTimeZone);
        var currentHour = australianNow.Hour;

        _logger.LogInformation("DND Check - Current hour (AU): {Hour}, DND window: {Start}-{End}",
            currentHour, dndStartHour.Value, dndEndHour.Value);

        // Handle overnight DND (e.g., 22:00 - 07:00)
        if (dndStartHour.Value > dndEndHour.Value)
        {
            return currentHour >= dndStartHour.Value || currentHour < dndEndHour.Value;
        }

        // Normal DND (e.g., 08:00 - 17:00)
        return currentHour >= dndStartHour.Value && currentHour < dndEndHour.Value;
    }

    public async Task<(bool canCall, string reason, string? callSid)> InitiateNotificationCallAsync(
        string tradiePhone,
        string leadId,
        string customerName,
        string jobType,
        string location,
        int? dndStartHour = null,
        int? dndEndHour = null,
        string? description = null,
        string? budget = null,
        string? timing = null)
    {
        try
        {
            // Check DND hours
            if (IsWithinDndHours(dndStartHour, dndEndHour))
            {
                var australianNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AustralianTimeZone);
                _logger.LogInformation("Call skipped - within DND hours. Current time (AU): {Time}",
                    australianNow.ToString("HH:mm"));
                return (false, "DND_HOURS", null);
            }

            // Build TwiML URL for notification
            var queryParams = $"leadId={Uri.EscapeDataString(leadId)}" +
                            $"&customerName={Uri.EscapeDataString(customerName)}" +
                            $"&jobType={Uri.EscapeDataString(jobType)}" +
                            $"&location={Uri.EscapeDataString(location)}";

            if (!string.IsNullOrWhiteSpace(description))
                queryParams += $"&description={Uri.EscapeDataString(description)}";
            if (!string.IsNullOrWhiteSpace(budget))
                queryParams += $"&budget={Uri.EscapeDataString(budget)}";
            if (!string.IsNullOrWhiteSpace(timing))
                queryParams += $"&timing={Uri.EscapeDataString(timing)}";

            var twimlUrl = $"{_callbackBaseUrl}/api/twilio/notification?{queryParams}";

            var call = await CallResource.CreateAsync(
                to: new PhoneNumber(tradiePhone),
                from: new PhoneNumber(_twilioPhone),
                url: new Uri(twimlUrl),
                statusCallback: new Uri($"{_callbackBaseUrl}/api/twilio/status"),
                statusCallbackEvent: new List<string> { "initiated", "ringing", "answered", "completed" },
                statusCallbackMethod: Twilio.Http.HttpMethod.Post
            );

            _logger.LogInformation("Notification call initiated to tradie. CallSid: {CallSid}", call.Sid);
            return (true, "SUCCESS", call.Sid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating notification call");
            return (false, ex.Message, null);
        }
    }

    public string GenerateNotificationTwiML(
        string leadId,
        string customerName,
        string jobType,
        string location,
        string? description = null,
        string? budget = null,
        string? timing = null)
    {
        var response = new VoiceResponse();

        // Sanitize all inputs
        var safeCustomerName = SanitizeForTwiML(customerName);
        var safeJobType = SanitizeForTwiML(jobType);
        var safeLocation = SanitizeForTwiML(location);
        var safeDescription = SanitizeForTwiML(description);
        var safeBudget = SanitizeForTwiML(budget);
        var safeTiming = SanitizeForTwiML(timing);

        // Build concise notification message
        var message = $"New ServiceSeeking lead. ";

        if (!string.IsNullOrWhiteSpace(safeJobType))
            message += $"{safeJobType}. ";

        if (!string.IsNullOrWhiteSpace(safeLocation))
            message += $"Location: {safeLocation}. ";

        if (!string.IsNullOrWhiteSpace(safeBudget))
            message += $"Budget: {safeBudget}. ";

        if (!string.IsNullOrWhiteSpace(safeTiming))
            message += $"Timing: {safeTiming}. ";

        if (!string.IsNullOrWhiteSpace(safeDescription))
        {
            // Limit description length for speech
            var shortDesc = safeDescription.Length > 80 ? safeDescription.Substring(0, 80) + "..." : safeDescription;
            message += $"Description: {shortDesc}. ";
        }

        message += "Check ServiceSeeking to respond. Goodbye.";

        response.Say(message, voice: "Polly.Nicole", language: "en-AU");
        response.Hangup();

        return response.ToString();
    }

    public bool ValidateWebhookSignature(string signature, string url, Dictionary<string, string> parameters)
    {
        try
        {
            var validator = new Twilio.Security.RequestValidator(_authToken);
            return validator.Validate(url, parameters, signature);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Twilio webhook signature");
            return false;
        }
    }
}
