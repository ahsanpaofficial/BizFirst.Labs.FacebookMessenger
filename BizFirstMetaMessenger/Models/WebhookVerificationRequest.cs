using System.ComponentModel.DataAnnotations;

namespace BizFirstMetaMessenger.Models;

/// <summary>
/// Request model for webhook verification
/// </summary>
public class WebhookVerificationRequest
{
    /// <summary>
    /// The mode of the verification request (should be "subscribe")
    /// </summary>
    [Required]
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// The verify token sent by Meta
    /// </summary>
    [Required]
    public string VerifyToken { get; set; } = string.Empty;

    /// <summary>
    /// The challenge string to echo back
    /// </summary>
    [Required]
    public string Challenge { get; set; } = string.Empty;
}
