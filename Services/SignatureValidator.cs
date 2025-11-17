using System.Security.Cryptography;
using System.Text;

namespace BizFirstMetaMessenger.Services;

/// <summary>
/// Service for validating webhook signatures using HMAC
/// </summary>
public class SignatureValidator : ISignatureValidator
{
    private readonly ILogger<SignatureValidator> _logger;

    public SignatureValidator(ILogger<SignatureValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates the HMAC signature from Meta webhook (supports sha256 and sha1)
    /// </summary>
    public bool ValidateSignature(string appSecret, string payload, string signatureHeader)
    {
        if (string.IsNullOrEmpty(appSecret))
        {
            _logger.LogError("App secret not configured - cannot validate signature.");
            return false;
        }

        if (string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Signature header is null or empty");
            return false;
        }

        // signatureHeader looks like "sha256=abcdef..." or "sha1=..." depending on header
        var parts = signatureHeader.Split('=');
        if (parts.Length != 2) return false;

        var method = parts[0].ToLowerInvariant(); // "sha256" or "sha1"
        var sigHex = parts[1];

        byte[] computed;
        if (method == "sha256")
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
            computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        }
        else if (method == "sha1")
        {
            using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(appSecret));
            computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        }
        else
        {
            _logger.LogWarning("Unsupported signature method '{Method}'", method);
            return false;
        }

        var computedHex = BitConverter.ToString(computed).Replace("-", "").ToLowerInvariant();

        // Use constant-time comparison
        return ConstantTimeEquals(computedHex, sigHex.ToLowerInvariant());
    }

    /// <summary>
    /// Constant-time string comparison to avoid timing attacks
    /// </summary>
    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }
}
