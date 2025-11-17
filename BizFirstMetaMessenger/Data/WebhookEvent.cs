using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BizFirstMetaMessenger.Data;

/// <summary>
/// Database entity for storing raw webhook events
/// </summary>
public class WebhookEvent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Type of webhook event (e.g., "webhook", "messaging", "field_about")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// When the webhook was received by our server
    /// </summary>
    [Required]
    public DateTime ReceivedAt { get; set; }

    /// <summary>
    /// Raw JSON payload from Meta
    /// </summary>
    [Required]
    public string RawPayload { get; set; } = string.Empty;

    /// <summary>
    /// Object type from Meta (e.g., "page")
    /// </summary>
    [MaxLength(50)]
    public string? ObjectType { get; set; }

    /// <summary>
    /// Flag to indicate if this event has been processed
    /// </summary>
    public bool IsProcessed { get; set; } = false;

    /// <summary>
    /// Navigation property to related messages
    /// </summary>
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
