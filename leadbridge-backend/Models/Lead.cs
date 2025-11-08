using System.Text.Json.Serialization;

namespace LeadBridge.Models;

/// <summary>
/// Represents a lead received from ServiceSeeking
/// </summary>
public class Lead
{
    [JsonPropertyName("leadId")]
    public string LeadId { get; set; } = string.Empty;

    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("jobType")]
    public string JobType { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("tradiePhone")]
    public string TradiePhone { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("budget")]
    public string? Budget { get; set; }

    [JsonPropertyName("timing")]
    public string? Timing { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("dndStartHour")]
    public int? DndStartHour { get; set; }

    [JsonPropertyName("dndEndHour")]
    public int? DndEndHour { get; set; }
}

/// <summary>
/// Response returned to the Chrome extension
/// </summary>
public class LeadResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("leadId")]
    public string LeadId { get; set; } = string.Empty;

    [JsonPropertyName("callId")]
    public string? CallId { get; set; }
}
