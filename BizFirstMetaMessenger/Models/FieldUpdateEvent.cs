using System.Text.Json.Serialization;

namespace BizFirstMetaMessenger.Models;

/// <summary>
/// Model for Meta field update events (e.g., about, name, etc.)
/// </summary>
public class FieldUpdateEvent
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Entry containing field changes
/// </summary>
public class FieldChangeEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("changes")]
    public FieldUpdateEvent[]? Changes { get; set; }
}

/// <summary>
/// Complete webhook event for field updates
/// </summary>
public class FieldUpdateWebhookEvent
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("entry")]
    public FieldChangeEntry[]? Entry { get; set; }
}
