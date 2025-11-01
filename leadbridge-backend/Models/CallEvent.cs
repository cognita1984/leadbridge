using Azure;
using Azure.Data.Tables;

namespace LeadBridge.Models;

/// <summary>
/// Represents a call event logged in Azure Table Storage
/// </summary>
public class CallEventEntity : ITableEntity
{
    // ITableEntity properties
    public string PartitionKey { get; set; } = string.Empty; // Date: yyyy-MM-dd
    public string RowKey { get; set; } = string.Empty; // CallId or unique identifier
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Custom properties
    public string LeadId { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string TradiePhone { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string CallId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Initiated, TradieAnswered, CustomerBridged, Failed, Completed
    public int DurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents a lead entity in Azure Table Storage
/// </summary>
public class LeadEntity : ITableEntity
{
    // ITableEntity properties
    public string PartitionKey { get; set; } = string.Empty; // Date: yyyy-MM-dd
    public string RowKey { get; set; } = string.Empty; // LeadId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Custom properties
    public string LeadId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string TradiePhone { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string Status { get; set; } = string.Empty; // Received, Calling, Completed, Failed
    public string? CallId { get; set; }
}

/// <summary>
/// Call status enumeration
/// </summary>
public enum CallStatus
{
    Initiated,
    TradieRinging,
    TradieAnswered,
    CustomerRinging,
    CustomerBridged,
    Completed,
    Failed,
    TradieDeclined,
    CustomerNoAnswer
}
