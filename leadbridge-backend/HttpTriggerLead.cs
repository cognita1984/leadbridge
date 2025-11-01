using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using LeadBridge.Models;
using LeadBridge.Services;
using LeadBridge.Storage;

namespace LeadBridge;

/// <summary>
/// HTTP-triggered Azure Function to receive leads from Chrome extension
/// </summary>
public class HttpTriggerLead
{
    private readonly ILogger<HttpTriggerLead> _logger;
    private readonly IAcsService _acsService;
    private readonly ITableClientFactory _tableFactory;

    public HttpTriggerLead(
        ILogger<HttpTriggerLead> logger,
        IAcsService acsService,
        ITableClientFactory tableFactory)
    {
        _logger = logger;
        _acsService = acsService;
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
                string.IsNullOrWhiteSpace(lead.TradiePhone) ||
                string.IsNullOrWhiteSpace(lead.CustomerPhone))
            {
                _logger.LogWarning("Missing required fields");
                return await CreateResponseAsync(req, HttpStatusCode.BadRequest, new LeadResponse
                {
                    Success = false,
                    Message = "Missing required fields: leadId, tradiePhone, customerPhone"
                });
            }

            _logger.LogInformation("Processing lead: {LeadId} for tradie {TradiePhone}",
                lead.LeadId, lead.TradiePhone);

            // Store lead in Table Storage
            var leadStorage = new LeadStorageService(_tableFactory, new LoggerFactory().CreateLogger<LeadStorageService>());
            var leadEntity = new LeadEntity
            {
                LeadId = lead.LeadId,
                CustomerName = lead.CustomerName,
                CustomerPhone = lead.CustomerPhone,
                TradiePhone = lead.TradiePhone,
                JobType = lead.JobType,
                Location = lead.Location,
                ReceivedAt = DateTime.UtcNow,
                Status = "Received"
            };

            await leadStorage.SaveLeadAsync(leadEntity);

            // Initiate call to tradie
            string callId;
            try
            {
                callId = await _acsService.InitiateCallToTradieAsync(
                    lead.TradiePhone,
                    lead.LeadId,
                    lead.JobType,
                    lead.Location);

                // Update lead status
                await leadStorage.UpdateLeadStatusAsync(lead.LeadId, DateTime.UtcNow, "Calling", callId);

                _logger.LogInformation("Call initiated successfully: {CallId}", callId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate call for lead: {LeadId}", lead.LeadId);

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
                CallId = callId,
                CustomerPhone = lead.CustomerPhone,
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
                Message = "Lead received and call initiated",
                LeadId = lead.LeadId,
                CallId = callId
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
    /// POST /api/callback - Receives callbacks from Azure Communication Services
    /// </summary>
    [Function("AcsCallback")]
    public async Task<HttpResponseData> AcsCallback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "callback")] HttpRequestData req)
    {
        _logger.LogInformation("ACS callback endpoint invoked");

        try
        {
            // Read the request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // Parse CloudEvent(s)
            var cloudEvents = CloudEventParser.ParseMany(requestBody);

            foreach (var cloudEvent in cloudEvents)
            {
                _logger.LogInformation("Processing CloudEvent: {EventType}", cloudEvent.Type);

                // Handle the event
                await _acsService.HandleCallbackEventAsync(cloudEvent);
            }

            // Return 200 OK
            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ACS callback");
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
            version = "1.0.0"
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

/// <summary>
/// Simple CloudEvent parser (minimal implementation)
/// In production, use Azure.Messaging.CloudEvent package
/// </summary>
public static class CloudEventParser
{
    public static IEnumerable<Azure.Messaging.CloudEvent> ParseMany(string json)
    {
        var events = new List<Azure.Messaging.CloudEvent>();

        try
        {
            // Try to parse as array
            if (json.TrimStart().StartsWith("["))
            {
                var jsonArray = JsonSerializer.Deserialize<JsonElement[]>(json);
                if (jsonArray != null)
                {
                    foreach (var element in jsonArray)
                    {
                        events.Add(ParseCloudEvent(element));
                    }
                }
            }
            else
            {
                // Parse as single event
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
                events.Add(ParseCloudEvent(jsonElement));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse CloudEvent", ex);
        }

        return events;
    }

    private static Azure.Messaging.CloudEvent ParseCloudEvent(JsonElement element)
    {
        var eventType = element.GetProperty("type").GetString() ?? "unknown";
        var source = element.GetProperty("source").GetString() ?? "unknown";
        var data = element.GetProperty("data");

        return new Azure.Messaging.CloudEvent(source, eventType, data);
    }
}
