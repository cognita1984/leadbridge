using Azure.Communication.CallAutomation;
using Azure.Messaging;
using Microsoft.Extensions.Logging;

namespace LeadBridge.Services;

public interface IAcsService
{
    Task<string> InitiateCallToTradieAsync(string tradiePhone, string leadId, string jobType, string location);
    Task HandleCallbackEventAsync(CloudEvent cloudEvent);
}

/// <summary>
/// Service for managing Azure Communication Services call automation
/// </summary>
public class AcsService : IAcsService
{
    private readonly CallAutomationClient _client;
    private readonly string _callbackUri;
    private readonly ILogger<AcsService> _logger;

    // Store active call contexts (in production, use Redis or Cosmos DB)
    private static readonly Dictionary<string, CallContext> ActiveCalls = new();

    public AcsService(string connectionString, string callbackUri, ILogger<AcsService>? logger = null)
    {
        _client = new CallAutomationClient(connectionString);
        _callbackUri = callbackUri;
        _logger = logger ?? new LoggerFactory().CreateLogger<AcsService>();
    }

    /// <summary>
    /// Initiate outbound call to tradie
    /// </summary>
    public async Task<string> InitiateCallToTradieAsync(
        string tradiePhone,
        string leadId,
        string jobType,
        string location)
    {
        try
        {
            _logger.LogInformation("Initiating call to tradie: {TradiePhone} for lead {LeadId}", tradiePhone, leadId);

            // Get the ACS phone number (caller ID)
            var sourcePhoneNumber = Environment.GetEnvironmentVariable("ACS_PHONE_NUMBER")
                ?? throw new InvalidOperationException("ACS_PHONE_NUMBER not configured");

            // Create phone number identifiers
            var source = new PhoneNumberIdentifier(sourcePhoneNumber);
            var target = new PhoneNumberIdentifier(tradiePhone);

            // Prepare TTS message
            var greeting = $"You have a new ServiceSeeking lead for {jobType} in {location}. Press 1 to call the customer now, or press 2 to skip.";

            // Create call options
            var createCallOptions = new CreateCallOptions(source, target, new Uri(_callbackUri))
            {
                AzureCognitiveServicesEndpointUrl = new Uri(
                    Environment.GetEnvironmentVariable("COGNITIVE_SERVICES_ENDPOINT")
                    ?? "https://australiaeast.api.cognitive.microsoft.com/"),
            };

            // Create the call
            var callResult = await _client.CreateCallAsync(createCallOptions);
            var callConnectionId = callResult.Value.CallConnectionProperties.CallConnectionId;

            _logger.LogInformation("Call created: {CallConnectionId}", callConnectionId);

            // Store call context
            var context = new CallContext
            {
                CallConnectionId = callConnectionId,
                LeadId = leadId,
                TradiePhone = tradiePhone,
                JobType = jobType,
                Location = location,
                Status = "TradieRinging",
                CreatedAt = DateTime.UtcNow
            };

            ActiveCalls[callConnectionId] = context;

            // Get call connection to control the call
            var callConnection = _client.GetCallConnection(callConnectionId);

            // Play greeting and recognize DTMF (keypress)
            var recognizeOptions = new CallMediaRecognizeDtmfOptions(
                targetParticipant: target,
                maxTonesToCollect: 1)
            {
                InitialSilenceTimeout = TimeSpan.FromSeconds(10),
                Prompt = new TextSource(greeting)
                {
                    VoiceName = "en-AU-NatashaNeural" // Australian English voice
                },
                InterruptPrompt = true,
                InterToneTimeout = TimeSpan.FromSeconds(5)
            };

            await callConnection.GetCallMedia().StartRecognizingAsync(recognizeOptions);

            return callConnectionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate call to tradie");
            throw;
        }
    }

    /// <summary>
    /// Handle callback events from ACS
    /// </summary>
    public async Task HandleCallbackEventAsync(CloudEvent cloudEvent)
    {
        try
        {
            _logger.LogInformation("Received callback event: {EventType}", cloudEvent.Type);

            // Parse the event
            var callEvent = CallAutomationEventParser.Parse(cloudEvent);

            if (callEvent == null)
            {
                _logger.LogWarning("Failed to parse callback event");
                return;
            }

            var callConnectionId = callEvent.CallConnectionId;

            if (!ActiveCalls.TryGetValue(callConnectionId, out var context))
            {
                _logger.LogWarning("No context found for call: {CallConnectionId}", callConnectionId);
                return;
            }

            // Handle specific events
            switch (callEvent)
            {
                case CallConnected connectedEvent:
                    await HandleCallConnectedAsync(connectedEvent, context);
                    break;

                case RecognizeCompleted recognizeEvent:
                    await HandleRecognizeCompletedAsync(recognizeEvent, context);
                    break;

                case RecognizeFailed recognizeFailedEvent:
                    await HandleRecognizeFailedAsync(recognizeFailedEvent, context);
                    break;

                case CallDisconnected disconnectedEvent:
                    await HandleCallDisconnectedAsync(disconnectedEvent, context);
                    break;

                case PlayCompleted playCompletedEvent:
                    _logger.LogInformation("Play completed for call: {CallConnectionId}", callConnectionId);
                    break;

                default:
                    _logger.LogInformation("Unhandled event type: {EventType}", callEvent.GetType().Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling callback event");
        }
    }

    private async Task HandleCallConnectedAsync(CallConnected connectedEvent, CallContext context)
    {
        _logger.LogInformation("Tradie answered call: {CallConnectionId}", connectedEvent.CallConnectionId);
        context.Status = "TradieAnswered";
        context.TradieAnsweredAt = DateTime.UtcNow;
    }

    private async Task HandleRecognizeCompletedAsync(RecognizeCompleted recognizeEvent, CallContext context)
    {
        _logger.LogInformation("DTMF recognition completed for call: {CallConnectionId}", recognizeEvent.CallConnectionId);

        // Check what key was pressed
        if (recognizeEvent.RecognizeResult is DtmfResult dtmfResult)
        {
            var tones = string.Join("", dtmfResult.Tones);
            _logger.LogInformation("Tradie pressed: {Tones}", tones);

            if (tones == "1")
            {
                // Tradie wants to connect - bridge to customer
                await BridgeToCustomerAsync(recognizeEvent.CallConnectionId, context);
            }
            else if (tones == "2")
            {
                // Tradie declined - hang up
                _logger.LogInformation("Tradie declined lead: {LeadId}", context.LeadId);
                context.Status = "TradieDeclined";
                await HangUpCallAsync(recognizeEvent.CallConnectionId);
            }
        }
    }

    private async Task HandleRecognizeFailedAsync(RecognizeFailed recognizeFailedEvent, CallContext context)
    {
        _logger.LogWarning("DTMF recognition failed: {CallConnectionId} - {Reason}",
            recognizeFailedEvent.CallConnectionId,
            recognizeFailedEvent.ResultInformation?.Message ?? "Unknown");

        context.Status = "RecognizeFailed";

        // Optionally replay the prompt or hang up
        await HangUpCallAsync(recognizeFailedEvent.CallConnectionId);
    }

    private async Task HandleCallDisconnectedAsync(CallDisconnected disconnectedEvent, CallContext context)
    {
        _logger.LogInformation("Call disconnected: {CallConnectionId}", disconnectedEvent.CallConnectionId);
        context.Status = "Completed";
        context.CompletedAt = DateTime.UtcNow;

        // Calculate duration
        if (context.TradieAnsweredAt.HasValue)
        {
            context.DurationSeconds = (int)(context.CompletedAt.Value - context.TradieAnsweredAt.Value).TotalSeconds;
        }

        // Remove from active calls
        ActiveCalls.Remove(disconnectedEvent.CallConnectionId);
    }

    private async Task BridgeToCustomerAsync(string callConnectionId, CallContext context)
    {
        try
        {
            _logger.LogInformation("Bridging to customer for lead: {LeadId}", context.LeadId);

            // For demo purposes, we'll just log this
            // In production, you'd:
            // 1. Add the customer to the call using AddParticipantAsync
            // 2. Play hold music to tradie while customer is being called
            // 3. When customer answers, bridge both participants

            var callConnection = _client.GetCallConnection(callConnectionId);

            // Placeholder: Add customer to the call
            // var customerPhone = new PhoneNumberIdentifier(context.CustomerPhone);
            // await callConnection.AddParticipantAsync(customerPhone);

            _logger.LogInformation("Customer would be called here: {CustomerPhone}", context.CustomerPhone);
            context.Status = "CustomerBridged";

            // Play confirmation to tradie
            var message = "Connecting you to the customer now. Please wait.";
            var playSource = new TextSource(message)
            {
                VoiceName = "en-AU-NatashaNeural"
            };

            await callConnection.GetCallMedia().PlayToAllAsync(playSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bridge to customer");
            context.Status = "BridgeFailed";
            await HangUpCallAsync(callConnectionId);
        }
    }

    private async Task HangUpCallAsync(string callConnectionId)
    {
        try
        {
            var callConnection = _client.GetCallConnection(callConnectionId);
            await callConnection.HangUpAsync(forEveryone: true);
            _logger.LogInformation("Call hung up: {CallConnectionId}", callConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hang up call: {CallConnectionId}", callConnectionId);
        }
    }
}

/// <summary>
/// Context for an active call
/// </summary>
public class CallContext
{
    public string CallConnectionId { get; set; } = string.Empty;
    public string LeadId { get; set; } = string.Empty;
    public string TradiePhone { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? TradieAnsweredAt { get; set; }
    public DateTime? CustomerAnsweredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int DurationSeconds { get; set; }
}
