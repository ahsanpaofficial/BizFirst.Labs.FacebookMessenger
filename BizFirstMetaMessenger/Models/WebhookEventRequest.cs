using System.Text.Json;
using System.Text.Json.Serialization;

namespace BizFirstMetaMessenger.Models;

/// <summary>
/// Request model for webhook events from Meta
/// </summary>
public class WebhookEventRequest
{
    /// <summary>
    /// The type of object (e.g., "page")
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    /// <summary>
    /// Array of entry objects containing the events
    /// </summary>
    [JsonPropertyName("entry")]
    public JsonElement[]? Entry { get; set; }

    /// <summary>
    /// Raw body content for signature validation
    /// </summary>
    [JsonIgnore]
    public string? RawBody { get; set; }

    /// <summary>
    /// Signature header from Meta
    /// </summary>
    [JsonIgnore]
    public string? SignatureHeader { get; set; }
}
