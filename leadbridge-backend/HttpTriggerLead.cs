using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Web;
using LeadBridge.Models;
using LeadBridge.Services;
using LeadBridge.Storage;

namespace LeadBridge;

/// <summary>
/// HTTP-triggered Azure Function to receive leads from Chrome extension and handle Twilio webhooks
/// </summary>
public class HttpTriggerLead
{
    private readonly ILogger<HttpTriggerLead> _logger;
    private readonly ITwilioService _twilioService;
    private readonly LeadStorageService _leadStorage;
    private readonly CallEventStorageService _callEventStorage;

    public HttpTriggerLead(
        ILogger<HttpTriggerLead> logger,
        ITwilioService twilioService,
        LeadStorageService leadStorage,
        CallEventStorageService callEventStorage)
    {
        _logger = logger;
        _twilioService = twilioService;
        _leadStorage = leadStorage;
        _callEventStorage = callEventStorage;
    }

    /// <summary>
    /// POST /api/newlead - Receives lead data from Chrome extension
    /// </summary>
    [Function("NewLead")]
    public async Task<HttpResponseData> NewLead(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "newlead")] HttpRequestData req)
    {
        _logger.LogInformation("NewLead endpoint invoked");

        try
        {
            // Parse request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var lead = JsonSerializer.Deserialize<Lead>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (lead == null)
            {
                _logger.LogWarning("Invalid request body");
                return await CreateResponseAsync(req, HttpStatusCode.BadRequest, new LeadResponse
                {
                    Success = false,
                    Message = "Invalid request body"
                });
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(lead.LeadId) ||
                string.IsNullOrWhiteSpace(lead.TradiePhone))
            {
                _logger.LogWarning("Missing required fields");
                return await CreateResponseAsync(req, HttpStatusCode.BadRequest, new LeadResponse
                {
                    Success = false,
                    Message = "Missing required fields: leadId, tradiePhone"
                });
            }

            _logger.LogInformation("Processing lead: {LeadId} - Customer: {CustomerName}, Job: {JobType}, Location: {Location}",
                lead.LeadId, lead.CustomerName, lead.JobType, lead.Location);

            // Store lead in Table Storage
            var leadEntity = new LeadEntity
            {
                LeadId = lead.LeadId,
                CustomerName = lead.CustomerName,
                TradiePhone = lead.TradiePhone,
                JobType = lead.JobType,
                Location = lead.Location,
                ReceivedAt = DateTime.UtcNow,
                Status = "Received"
            };

            await _leadStorage.SaveLeadAsync(leadEntity);

            // Initiate notification call to tradie using Twilio
            var (canCall, reason, callSid) = await _twilioService.InitiateNotificationCallAsync(
                lead.TradiePhone,
                lead.LeadId,
                lead.CustomerName,
                lead.JobType,
                lead.Location,
                lead.DndStartHour,
                lead.DndEndHour,
                lead.Description,
                lead.Budget,
                lead.Timing);

            if (!canCall)
            {
                var status = reason == "DND_HOURS" ? "Skipped_DND" : "Failed";
                await _leadStorage.UpdateLeadStatusAsync(lead.LeadId, DateTime.UtcNow, status);

                _logger.LogWarning("Notification call skipped for lead: {LeadId}, Reason: {Reason}", lead.LeadId, reason);

                return await CreateResponseAsync(req, HttpStatusCode.OK, new LeadResponse
                {
                    Success = true,
                    Message = $"Lead received but call skipped: {reason}",
                    LeadId = lead.LeadId
                });
            }

            // Update lead status
            await _leadStorage.UpdateLeadStatusAsync(lead.LeadId, DateTime.UtcNow, "Notified", callSid);

            _logger.LogInformation("Notification call initiated successfully. CallSid: {CallSid}", callSid);

            // Log call event
            var callEvent = new CallEventEntity
            {
                LeadId = lead.LeadId,
                CallId = callSid!,
                TradiePhone = lead.TradiePhone,
                JobType = lead.JobType,
                Location = lead.Location,
                Status = "Initiated",
                CreatedAt = DateTime.UtcNow
            };

            await _callEventStorage.SaveCallEventAsync(callEvent);

            // Return success response
            return await CreateResponseAsync(req, HttpStatusCode.OK, new LeadResponse
            {
                Success = true,
                Message = "Lead received and notification call initiated",
                LeadId = lead.LeadId,
                CallId = callSid
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing lead");
            return await CreateResponseAsync(req, HttpStatusCode.InternalServerError, new LeadResponse
            {
                Success = false,
                Message = $"Internal server error: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// POST/GET /api/twilio/notification - TwiML response for tradie notification call
    /// </summary>
    [Function("TwilioNotification")]
    public async Task<HttpResponseData> TwilioNotification(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "get", Route = "twilio/notification")] HttpRequestData req)
    {
        _logger.LogInformation("TwilioNotification endpoint invoked");

        try
        {
            // Validate Twilio webhook signature
            if (!ValidateTwilioRequest(req))
            {
                _logger.LogWarning("Invalid Twilio webhook signature");
                return req.CreateResponse(HttpStatusCode.Forbidden);
            }

            var query = HttpUtility.ParseQueryString(req.Url.Query);
            var leadId = query["leadId"] ?? "unknown";
            var customerName = query["customerName"] ?? "Unknown Customer";
            var jobType = query["jobType"] ?? "General Service";
            var location = query["location"] ?? "Unknown Location";
            var description = query["description"];
            var budget = query["budget"];
            var timing = query["timing"];

            _logger.LogInformation("Generating notification TwiML for lead: {LeadId}", leadId);

            var twiml = _twilioService.GenerateNotificationTwiML(
                leadId, customerName, jobType, location, description, budget, timing);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/xml");
            await response.WriteStringAsync(twiml);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating notification TwiML");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// POST /api/twilio/call-complete - Called when notification call ends
    /// </summary>
    [Function("TwilioCallComplete")]
    public async Task<HttpResponseData> TwilioCallComplete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "twilio/call-complete")] HttpRequestData req)
    {
        _logger.LogInformation("TwilioCallComplete endpoint invoked");

        try
        {
            // Validate Twilio webhook signature
            if (!ValidateTwilioRequest(req))
            {
                _logger.LogWarning("Invalid Twilio webhook signature");
                return req.CreateResponse(HttpStatusCode.Forbidden);
            }

            var formData = await req.ReadFormAsync();
            var callSid = formData["CallSid"]?.ToString();
            var callDuration = formData["CallDuration"]?.ToString();
            var callStatus = formData["CallStatus"]?.ToString();

            _logger.LogInformation("Call completed. CallSid: {CallSid}, Duration: {Duration}s, Status: {Status}",
                callSid, callDuration, callStatus);

            // Update call event in storage
            if (!string.IsNullOrEmpty(callSid) && int.TryParse(callDuration, out var duration))
            {
                await _callEventStorage.UpdateCallDurationAsync(callSid, duration, callStatus ?? "completed");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("OK");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling call complete");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// POST /api/twilio/status - Receives status callbacks from Twilio
    /// </summary>
    [Function("TwilioStatus")]
    public async Task<HttpResponseData> TwilioStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "twilio/status")] HttpRequestData req)
    {
        _logger.LogInformation("TwilioStatus endpoint invoked");

        try
        {
            // Validate Twilio webhook signature
            if (!ValidateTwilioRequest(req))
            {
                _logger.LogWarning("Invalid Twilio webhook signature");
                return req.CreateResponse(HttpStatusCode.Forbidden);
            }

            var formData = await req.ReadFormAsync();
            var callSid = formData["CallSid"]?.ToString();
            var callStatus = formData["CallStatus"]?.ToString();

            _logger.LogInformation("Twilio status update. CallSid: {CallSid}, Status: {Status}",
                callSid, callStatus);

            // Log status change
            if (!string.IsNullOrEmpty(callSid) && !string.IsNullOrEmpty(callStatus))
            {
                await _callEventStorage.UpdateCallStatusAsync(callSid, callStatus);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("OK");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Twilio status");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// GET /api/health - Health check endpoint
    /// </summary>
    [Function("Health")]
    public async Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        _logger.LogInformation("Health check endpoint invoked");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "3.0.0-notification-only",
            provider = "Twilio"
        });

        return response;
    }

    /// <summary>
    /// Validate Twilio webhook request signature
    /// </summary>
    private bool ValidateTwilioRequest(HttpRequestData req)
    {
        try
        {
            // Get signature from header
            if (!req.Headers.TryGetValues("X-Twilio-Signature", out var signatures))
            {
                _logger.LogWarning("Missing X-Twilio-Signature header");
                return false;
            }

            var signature = signatures.FirstOrDefault();
            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("Empty X-Twilio-Signature header");
                return false;
            }

            // Get full URL
            var url = req.Url.ToString();

            // Get form parameters
            var parameters = new Dictionary<string, string>();
            if (req.Method.ToUpper() == "POST")
            {
                var formData = req.ReadFormAsync().Result;
                foreach (var key in formData.Keys)
                {
                    parameters[key] = formData[key]?.ToString() ?? string.Empty;
                }
            }

            // Validate signature
            return _twilioService.ValidateWebhookSignature(signature, url, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Twilio request");
            return false;
        }
    }

    private async Task<HttpResponseData> CreateResponseAsync(
        HttpRequestData req,
        HttpStatusCode statusCode,
        LeadResponse responseBody)
    {
        var response = req.CreateResponse(statusCode);

        // Set CORS headers for Chrome extension - RESTRICTED to extension origin
        // TODO: Replace with actual extension ID after publishing
        // Format: chrome-extension://YOUR_EXTENSION_ID
        var allowedOrigin = Environment.GetEnvironmentVariable("ALLOWED_ORIGIN") ?? "*";

        response.Headers.Add("Access-Control-Allow-Origin", allowedOrigin);
        response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        await response.WriteAsJsonAsync(responseBody);
        return response;
    }
}
