namespace BizFirstMetaMessenger.Services;

/// <summary>
/// Interface for validating webhook signatures
/// </summary>
public interface ISignatureValidator
{
    /// <summary>
    /// Validates the HMAC signature from Meta webhook
    /// </summary>
    /// <param name="appSecret">The app secret for HMAC calculation</param>
    /// <param name="payload">The raw request body</param>
    /// <param name="signatureHeader">The signature header (X-Hub-Signature-256 or X-Hub-Signature)</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    bool ValidateSignature(string appSecret, string payload, string signatureHeader);
}
