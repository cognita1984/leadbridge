using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

namespace LeadBridge.Storage;

public interface ITableClientFactory
{
    TableClient GetLeadsTableClient();
    TableClient GetCallEventsTableClient();
}

/// <summary>
/// Factory for creating Azure Table Storage clients
/// </summary>
public class TableClientFactory : ITableClientFactory
{
    private readonly string _connectionString;
    private readonly ILogger<TableClientFactory> _logger;

    private const string LeadsTableName = "leads";
    private const string CallEventsTableName = "callevents";

    public TableClientFactory(ILogger<TableClientFactory> logger)
    {
        _logger = logger;
        _connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING not configured");

        // Ensure tables exist
        EnsureTablesExist();
    }

    public TableClient GetLeadsTableClient()
    {
        return new TableClient(_connectionString, LeadsTableName);
    }

    public TableClient GetCallEventsTableClient()
    {
        return new TableClient(_connectionString, CallEventsTableName);
    }

    private void EnsureTablesExist()
    {
        try
        {
            var leadsClient = new TableClient(_connectionString, LeadsTableName);
            leadsClient.CreateIfNotExists();

            var callEventsClient = new TableClient(_connectionString, CallEventsTableName);
            callEventsClient.CreateIfNotExists();

            _logger.LogInformation("Azure Table Storage tables verified");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure tables exist");
            throw;
        }
    }
}

/// <summary>
/// Service for managing lead storage
/// </summary>
public class LeadStorageService
{
    private readonly TableClient _tableClient;
    private readonly ILogger<LeadStorageService> _logger;

    public LeadStorageService(ITableClientFactory factory, ILogger<LeadStorageService> logger)
    {
        _tableClient = factory.GetLeadsTableClient();
        _logger = logger;
    }

    public async Task<bool> SaveLeadAsync(Models.LeadEntity lead)
    {
        try
        {
            // Use date as partition key for efficient queries
            lead.PartitionKey = lead.ReceivedAt.ToString("yyyy-MM-dd");
            lead.RowKey = lead.LeadId;

            await _tableClient.UpsertEntityAsync(lead);
            _logger.LogInformation("Lead saved: {LeadId}", lead.LeadId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save lead: {LeadId}", lead.LeadId);
            return false;
        }
    }

    public async Task<Models.LeadEntity?> GetLeadAsync(string leadId, DateTime receivedDate)
    {
        try
        {
            var partitionKey = receivedDate.ToString("yyyy-MM-dd");
            var response = await _tableClient.GetEntityAsync<Models.LeadEntity>(partitionKey, leadId);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lead not found: {LeadId}", leadId);
            return null;
        }
    }

    public async Task<bool> UpdateLeadStatusAsync(string leadId, DateTime receivedDate, string status, string? callId = null)
    {
        try
        {
            var partitionKey = receivedDate.ToString("yyyy-MM-dd");
            var lead = await GetLeadAsync(leadId, receivedDate);

            if (lead == null)
            {
                _logger.LogWarning("Cannot update status - lead not found: {LeadId}", leadId);
                return false;
            }

            lead.Status = status;
            if (callId != null)
                lead.CallId = callId;

            await _tableClient.UpsertEntityAsync(lead);
            _logger.LogInformation("Lead status updated: {LeadId} -> {Status}", leadId, status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update lead status: {LeadId}", leadId);
            return false;
        }
    }
}

/// <summary>
/// Service for managing call event storage
/// </summary>
public class CallEventStorageService
{
    private readonly TableClient _tableClient;
    private readonly ILogger<CallEventStorageService> _logger;

    public CallEventStorageService(ITableClientFactory factory, ILogger<CallEventStorageService> logger)
    {
        _tableClient = factory.GetCallEventsTableClient();
        _logger = logger;
    }

    public async Task<bool> SaveCallEventAsync(Models.CallEventEntity callEvent)
    {
        try
        {
            callEvent.PartitionKey = callEvent.CreatedAt.ToString("yyyy-MM-dd");
            callEvent.RowKey = callEvent.CallId;

            await _tableClient.UpsertEntityAsync(callEvent);
            _logger.LogInformation("Call event saved: {CallId} - {Status}", callEvent.CallId, callEvent.Status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save call event: {CallId}", callEvent.CallId);
            return false;
        }
    }

    public async Task<Models.CallEventEntity?> GetCallEventAsync(string callId, DateTime createdDate)
    {
        try
        {
            var partitionKey = createdDate.ToString("yyyy-MM-dd");
            var response = await _tableClient.GetEntityAsync<Models.CallEventEntity>(partitionKey, callId);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call event not found: {CallId}", callId);
            return null;
        }
    }

    public async Task<bool> UpdateCallEventAsync(Models.CallEventEntity callEvent)
    {
        try
        {
            await _tableClient.UpsertEntityAsync(callEvent);
            _logger.LogInformation("Call event updated: {CallId} - {Status}", callEvent.CallId, callEvent.Status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update call event: {CallId}", callEvent.CallId);
            return false;
        }
    }

    public async Task<bool> UpdateCallStatusAsync(string callId, string status)
    {
        try
        {
            // Try to find the call event in recent partitions (last 7 days)
            for (int daysBack = 0; daysBack < 7; daysBack++)
            {
                var date = DateTime.UtcNow.AddDays(-daysBack);
                var callEvent = await GetCallEventAsync(callId, date);

                if (callEvent != null)
                {
                    callEvent.Status = status;
                    await _tableClient.UpsertEntityAsync(callEvent);
                    _logger.LogInformation("Call status updated: {CallId} -> {Status}", callId, status);
                    return true;
                }
            }

            _logger.LogWarning("Call event not found for status update: {CallId}", callId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update call status: {CallId}", callId);
            return false;
        }
    }

    public async Task<bool> UpdateCallDurationAsync(string callId, int durationSeconds, string status)
    {
        try
        {
            // Try to find the call event in recent partitions (last 7 days)
            for (int daysBack = 0; daysBack < 7; daysBack++)
            {
                var date = DateTime.UtcNow.AddDays(-daysBack);
                var callEvent = await GetCallEventAsync(callId, date);

                if (callEvent != null)
                {
                    callEvent.DurationSeconds = durationSeconds;
                    callEvent.Status = status;
                    await _tableClient.UpsertEntityAsync(callEvent);
                    _logger.LogInformation("Call duration updated: {CallId} - {Duration}s", callId, durationSeconds);
                    return true;
                }
            }

            _logger.LogWarning("Call event not found for duration update: {CallId}", callId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update call duration: {CallId}", callId);
            return false;
        }
    }
}
