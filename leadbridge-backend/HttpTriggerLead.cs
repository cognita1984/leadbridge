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
    private readonly ITableClientFactory _tableFactory;
    private const string HARDCODED_CUSTOMER_PHONE = "+61432584824"; // For testing

    public HttpTriggerLead(
        ILogger<HttpTriggerLead> logger,
        ITwilioService twilioService,
        ITableClientFactory tableFactory)
    {
        _logger = logger;
        _twilioService = twilioService;
        _tableFactory = tableFactory;
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
            var leadStorage = new LeadStorageService(_tableFactory, new LoggerFactory().CreateLogger<LeadStorageService>());
            var leadEntity = new LeadEntity
            {
                LeadId = lead.LeadId,
                CustomerName = lead.CustomerName,
                CustomerPhone = HARDCODED_CUSTOMER_PHONE, // Hardcoded for testing
                TradiePhone = lead.TradiePhone,
                JobType = lead.JobType,
                Location = lead.Location,
                ReceivedAt = DateTime.UtcNow,
                Status = "Received"
            };

            await leadStorage.SaveLeadAsync(leadEntity);

            // Initiate call to tradie using Twilio
            string callSid;
            try
            {
                callSid = await _twilioService.InitiateCallToTradieAsync(
                    lead.TradiePhone,
                    lead.LeadId,
                    lead.CustomerName,
                    lead.JobType,
                    lead.Location,
                    lead.Description,
                    lead.Budget,
                    lead.Timing);

                // Update lead status
                await leadStorage.UpdateLeadStatusAsync(lead.LeadId, DateTime.UtcNow, "Calling", callSid);

                _logger.LogInformation("Twilio call initiated successfully. CallSid: {CallSid}", callSid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate Twilio call for lead: {LeadId}", lead.LeadId);

                // Update lead status to failed
                await leadStorage.UpdateLeadStatusAsync(lead.LeadId, DateTime.UtcNow, "Failed");

                return await CreateResponseAsync(req, HttpStatusCode.InternalServerError, new LeadResponse
                {
                    Success = false,
                    Message = $"Failed to initiate call: {ex.Message}",
                    LeadId = lead.LeadId
                });
            }

            // Log call event
            var callEventStorage = new CallEventStorageService(_tableFactory, new LoggerFactory().CreateLogger<CallEventStorageService>());
            var callEvent = new CallEventEntity
            {
                LeadId = lead.LeadId,
                CallId = callSid,
                CustomerPhone = HARDCODED_CUSTOMER_PHONE,
                TradiePhone = lead.TradiePhone,
                JobType = lead.JobType,
                Location = lead.Location,
                Status = "Initiated",
                CreatedAt = DateTime.UtcNow
            };

            await callEventStorage.SaveCallEventAsync(callEvent);

            // Return success response
            return await CreateResponseAsync(req, HttpStatusCode.OK, new LeadResponse
            {
                Success = true,
                Message = "Lead received and call initiated via Twilio",
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
    /// POST /api/twilio/tradie-greeting - TwiML response for tradie call
    /// </summary>
    [Function("TwilioTradieGreeting")]
    public async Task<HttpResponseData> TwilioTradieGreeting(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "get", Route = "twilio/tradie-greeting")] HttpRequestData req)
    {
        _logger.LogInformation("TwilioTradieGreeting endpoint invoked");

        try
        {
            var query = HttpUtility.ParseQueryString(req.Url.Query);
            var leadId = query["leadId"] ?? "unknown";
            var customerName = query["customerName"] ?? "Unknown Customer";
            var jobType = query["jobType"] ?? "General Service";
            var location = query["location"] ?? "Unknown Location";
            var description = query["description"];
            var budget = query["budget"];
            var timing = query["timing"];

            _logger.LogInformation("Generating TwiML for lead: {LeadId}", leadId);

            var twiml = _twilioService.GenerateTradieCallTwiML(leadId, customerName, jobType, location, description, budget, timing);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/xml");
            await response.WriteStringAsync(twiml);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating tradie greeting TwiML");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// POST /api/twilio/tradie-response - Handles keypress from tradie (1 = call customer, 2 = skip)
    /// </summary>
    [Function("TwilioTradieResponse")]
    public async Task<HttpResponseData> TwilioTradieResponse(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "twilio/tradie-response")] HttpRequestData req)
    {
        _logger.LogInformation("TwilioTradieResponse endpoint invoked");

        try
        {
            var query = HttpUtility.ParseQueryString(req.Url.Query);
            var leadId = query["leadId"] ?? "unknown";

            // Read form data
            var formData = await req.ReadFormAsync();
            var digits = formData["Digits"]?.ToString();

            _logger.LogInformation("Tradie pressed: {Digits} for lead: {LeadId}", digits, leadId);

            string twiml;

            if (digits == "1")
            {
                // Call customer
                _logger.LogInformation("Tradie chose to call customer for lead: {LeadId}", leadId);
                twiml = _twilioService.GenerateCustomerBridgeTwiML(HARDCODED_CUSTOMER_PHONE);

                // Update lead status
                var leadStorage = new LeadStorageService(_tableFactory, new LoggerFactory().CreateLogger<LeadStorageService>());
                await leadStorage.UpdateLeadStatusAsync(leadId, DateTime.UtcNow, "Calling Customer");
            }
            else if (digits == "2")
            {
                // Skip lead
                _logger.LogInformation("Tradie chose to skip lead: {LeadId}", leadId);
                twiml = ((TwilioService)_twilioService).GenerateSkipLeadTwiML();

                // Update lead status
                var leadStorage = new LeadStorageService(_tableFactory, new LoggerFactory().CreateLogger<LeadStorageService>());
                await leadStorage.UpdateLeadStatusAsync(leadId, DateTime.UtcNow, "Skipped");
            }
            else
            {
                // Invalid input
                _logger.LogWarning("Invalid keypress: {Digits} for lead: {LeadId}", digits, leadId);
                twiml = ((TwilioService)_twilioService).GenerateInvalidInputTwiML();
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/xml");
            await response.WriteStringAsync(twiml);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tradie response");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// POST /api/twilio/call-complete - Called when customer call ends
    /// </summary>
    [Function("TwilioCallComplete")]
    public async Task<HttpResponseData> TwilioCallComplete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "twilio/call-complete")] HttpRequestData req)
    {
        _logger.LogInformation("TwilioCallComplete endpoint invoked");

        try
        {
            var formData = await req.ReadFormAsync();
            var callSid = formData["CallSid"]?.ToString();
            var callDuration = formData["CallDuration"]?.ToString();
            var callStatus = formData["CallStatus"]?.ToString();

            _logger.LogInformation("Call completed. CallSid: {CallSid}, Duration: {Duration}s, Status: {Status}",
                callSid, callDuration, callStatus);

            // Update call event in storage
            var callEventStorage = new CallEventStorageService(_tableFactory, new LoggerFactory().CreateLogger<CallEventStorageService>());
            if (!string.IsNullOrEmpty(callSid) && int.TryParse(callDuration, out var duration))
            {
                await callEventStorage.UpdateCallDurationAsync(callSid, duration, callStatus ?? "completed");
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
            var formData = await req.ReadFormAsync();
            var callSid = formData["CallSid"]?.ToString();
            var callStatus = formData["CallStatus"]?.ToString();

            _logger.LogInformation("Twilio status update. CallSid: {CallSid}, Status: {Status}",
                callSid, callStatus);

            // Log status change
            var callEventStorage = new CallEventStorageService(_tableFactory, new LoggerFactory().CreateLogger<CallEventStorageService>());
            if (!string.IsNullOrEmpty(callSid) && !string.IsNullOrEmpty(callStatus))
            {
                await callEventStorage.UpdateCallStatusAsync(callSid, callStatus);
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
            version = "2.0.0-twilio",
            provider = "Twilio"
        });

        return response;
    }

    private async Task<HttpResponseData> CreateResponseAsync(
        HttpRequestData req,
        HttpStatusCode statusCode,
        LeadResponse responseBody)
    {
        var response = req.CreateResponse(statusCode);

        // Set CORS headers for Chrome extension
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        await response.WriteAsJsonAsync(responseBody);
        return response;
    }
}
