using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BizFirstMetaMessenger.Data;

/// <summary>
/// Database entity for storing parsed message details
/// </summary>
public class Message
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to WebhookEvent
    /// </summary>
    [Required]
    public int WebhookEventId { get; set; }

    /// <summary>
    /// Navigation property to parent webhook event
    /// </summary>
    [ForeignKey(nameof(WebhookEventId))]
    public WebhookEvent WebhookEvent { get; set; } = null!;

    /// <summary>
    /// Meta's message ID
    /// </summary>
    [MaxLength(255)]
    public string? MessageId { get; set; }

    /// <summary>
    /// Sender's Facebook/Messenger ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// Recipient's Facebook/Messenger ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>
    /// Message text content
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Timestamp from Meta (when message was sent)
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Message type (message, postback, delivery, read)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is an echo of a message sent by the page
    /// </summary>
    public bool IsEcho { get; set; } = false;

    /// <summary>
    /// App ID if this is an echo message
    /// </summary>
    [MaxLength(100)]
    public string? AppId { get; set; }

    /// <summary>
    /// Postback payload (for button clicks)
    /// </summary>
    public string? PostbackPayload { get; set; }

    /// <summary>
    /// Delivery watermark (for delivery confirmations)
    /// </summary>
    public long? DeliveryWatermark { get; set; }

    /// <summary>
    /// When this message was stored in our database
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Flag to indicate if this message has been responded to
    /// </summary>
    public bool IsResponded { get; set; } = false;
}
