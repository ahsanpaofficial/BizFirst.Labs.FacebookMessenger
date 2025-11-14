namespace BizFirstMetaMessenger.Models;

/// <summary>
/// Configuration settings for Meta Messenger integration
/// </summary>
public class MetaConfiguration
{
    /// <summary>
    /// Verify token used for webhook verification handshake
    /// </summary>
    public string? VerifyToken { get; set; }

    /// <summary>
    /// App secret used for validating webhook signatures
    /// </summary>
    public string? AppSecret { get; set; }

    /// <summary>
    /// Optional page access token for sending messages
    /// </summary>
    public string? PageAccessToken { get; set; }
}
