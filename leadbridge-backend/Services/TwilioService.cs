using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Twilio.TwiML;
using Twilio.TwiML.Voice;

namespace LeadBridge.Services;

public interface ITwilioService
{
    Task<string> InitiateCallToTradieAsync(string tradiePhone, string leadId, string customerName, string jobType, string location);
    string GenerateTradieCallTwiML(string leadId, string customerName, string jobType, string location);
    string GenerateCustomerBridgeTwiML(string customerPhone);
}

public class TwilioService : ITwilioService
{
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _twilioPhone;
    private readonly string _callbackBaseUrl;

    public TwilioService(string accountSid, string authToken, string twilioPhone, string callbackBaseUrl)
    {
        _accountSid = accountSid;
        _authToken = authToken;
        _twilioPhone = twilioPhone;
        _callbackBaseUrl = callbackBaseUrl;

        TwilioClient.Init(_accountSid, _authToken);
    }

    public async Task<string> InitiateCallToTradieAsync(
        string tradiePhone,
        string leadId,
        string customerName,
        string jobType,
        string location)
    {
        try
        {
            // Build TwiML URL for initial greeting and gather
            var twimlUrl = $"{_callbackBaseUrl}/api/twilio/tradie-greeting?leadId={leadId}&customerName={Uri.EscapeDataString(customerName)}&jobType={Uri.EscapeDataString(jobType)}&location={Uri.EscapeDataString(location)}";

            var call = await CallResource.CreateAsync(
                to: new PhoneNumber(tradiePhone),
                from: new PhoneNumber(_twilioPhone),
                url: new Uri(twimlUrl),
                statusCallback: new Uri($"{_callbackBaseUrl}/api/twilio/status"),
                statusCallbackEvent: new List<string> { "initiated", "ringing", "answered", "completed" },
                statusCallbackMethod: Twilio.Http.HttpMethod.Post
            );

            Console.WriteLine($"[Twilio] Call initiated to tradie. CallSid: {call.Sid}");
            return call.Sid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Twilio] Error initiating call: {ex.Message}");
            throw;
        }
    }

    public string GenerateTradieCallTwiML(string leadId, string customerName, string jobType, string location)
    {
        var response = new VoiceResponse();

        // Greeting message with lead details
        var message = $"You have a new ServiceSeeking lead. " +
                     $"Customer name: {customerName}. " +
                     $"Job type: {jobType}. " +
                     $"Location: {location}. " +
                     $"Press 1 to call the customer now, or press 2 to skip this lead.";

        var gather = new Gather(
            numDigits: 1,
            action: new Uri($"{_callbackBaseUrl}/api/twilio/tradie-response?leadId={leadId}", UriKind.Relative),
            method: Twilio.Http.HttpMethod.Post
        );

        gather.Say(message, voice: "Polly.Nicole", language: "en-AU");

        response.Append(gather);

        // If no input, repeat the message
        response.Say("We did not receive any input. Goodbye.", voice: "Polly.Nicole", language: "en-AU");
        response.Hangup();

        return response.ToString();
    }

    public string GenerateCustomerBridgeTwiML(string customerPhone)
    {
        var response = new VoiceResponse();

        response.Say("Connecting you to the customer now. Please wait.", voice: "Polly.Nicole", language: "en-AU");

        var dial = new Dial(
            action: new Uri($"{_callbackBaseUrl}/api/twilio/call-complete", UriKind.Relative),
            callerId: _twilioPhone
        );

        dial.Number(customerPhone);
        response.Append(dial);

        return response.ToString();
    }

    public string GenerateSkipLeadTwiML()
    {
        var response = new VoiceResponse();
        response.Say("Lead skipped. Goodbye.", voice: "Polly.Nicole", language: "en-AU");
        response.Hangup();
        return response.ToString();
    }

    public string GenerateInvalidInputTwiML()
    {
        var response = new VoiceResponse();
        response.Say("Invalid input. Please press 1 to call customer or 2 to skip. Goodbye.", voice: "Polly.Nicole", language: "en-AU");
        response.Hangup();
        return response.ToString();
    }
}
