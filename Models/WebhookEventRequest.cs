using System.Text.Json;

namespace BizFirstMetaMessenger.Models;

/// <summary>
/// Request model for webhook events from Meta
/// </summary>
public class WebhookEventRequest
{
    /// <summary>
    /// The type of object (e.g., "page")
    /// </summary>
    public string Object { get; set; } = string.Empty;

    /// <summary>
    /// Array of entry objects containing the events
    /// </summary>
    public JsonElement[]? Entry { get; set; }

    /// <summary>
    /// Raw body content for signature validation
    /// </summary>
    public string? RawBody { get; set; }

    /// <summary>
    /// Signature header from Meta
    /// </summary>
    public string? SignatureHeader { get; set; }
}
