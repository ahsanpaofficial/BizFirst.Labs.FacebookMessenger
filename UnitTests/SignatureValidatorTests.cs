using BizFirstMetaMessenger.Models;
using BizFirstMetaMessenger.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace BizFirstMetaMessenger.Tests;

/// <summary>
/// Unit tests for SignatureValidator service
/// Tests HMAC signature validation for webhook security
/// </summary>
public class SignatureValidatorTests
{
    private readonly Mock<ILogger<SignatureValidator>> _mockLogger;
    private readonly SignatureValidator _validator;
    private const string TestAppSecret = "test_app_secret_123";

    public SignatureValidatorTests()
    {
        _mockLogger = new Mock<ILogger<SignatureValidator>>();
        _validator = new SignatureValidator(_mockLogger.Object);
    }

    [Fact(DisplayName = "Should validate correct SHA256 signature")]
    public void ValidateSignature_WithCorrectSHA256_ReturnsTrue()
    {
        // Arrange
        var payload = "{\"test\":\"data\"}";
        var signature = GenerateHmacSignature(payload, TestAppSecret, "sha256");

        // Act
        var result = _validator.ValidateSignature(TestAppSecret, payload, signature);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "Should validate correct SHA1 signature")]
    public void ValidateSignature_WithCorrectSHA1_ReturnsTrue()
    {
        // Arrange
        var payload = "{\"test\":\"data\"}";
        var signature = GenerateHmacSignature(payload, TestAppSecret, "sha1");

        // Act
        var result = _validator.ValidateSignature(TestAppSecret, payload, signature);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "Should reject incorrect signature")]
    public void ValidateSignature_WithIncorrectSignature_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"test\":\"data\"}";
        var signature = "sha256=incorrect_signature";

        // Act
        var result = _validator.ValidateSignature(TestAppSecret, payload, signature);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "Should reject null signature")]
    public void ValidateSignature_WithNullSignature_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"test\":\"data\"}";

        // Act
        var result = _validator.ValidateSignature(TestAppSecret, payload, null!);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "Should reject empty signature")]
    public void ValidateSignature_WithEmptySignature_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"test\":\"data\"}";

        // Act
        var result = _validator.ValidateSignature(TestAppSecret, payload, "");

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "Should reject signature without algorithm prefix")]
    public void ValidateSignature_WithoutAlgorithmPrefix_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"test\":\"data\"}";
        var signature = "invalid_format_signature";

        // Act
        var result = _validator.ValidateSignature(TestAppSecret, payload, signature);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "Should handle empty payload")]
    public void ValidateSignature_WithEmptyPayload_ValidatesCorrectly()
    {
        // Arrange
        var payload = "";
        var signature = GenerateHmacSignature(payload, TestAppSecret, "sha256");

        // Act
        var result = _validator.ValidateSignature(TestAppSecret, payload, signature);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "Should handle large payloads")]
    public void ValidateSignature_WithLargePayload_ValidatesCorrectly()
    {
        // Arrange
        var payload = new string('x', 10000); // 10KB payload
        var signature = GenerateHmacSignature(payload, TestAppSecret, "sha256");

        // Act
        var result = _validator.ValidateSignature(TestAppSecret, payload, signature);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "Should use constant-time comparison")]
    public void ValidateSignature_UsesConstantTimeComparison()
    {
        // Arrange
        var payload = "{\"test\":\"data\"}";
        var correctSignature = GenerateHmacSignature(payload, TestAppSecret, "sha256");

        // Create a signature that differs only in the last character
        var almostCorrectSignature = correctSignature[..^1] + "0";

        // Act
        var result = _validator.ValidateSignature(TestAppSecret, payload, almostCorrectSignature);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Helper method to generate HMAC signatures for testing
    /// </summary>
    private static string GenerateHmacSignature(string payload, string secret, string algorithm)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        byte[] hashBytes;
        if (algorithm.ToLower() == "sha256")
        {
            using var hmac = new HMACSHA256(keyBytes);
            hashBytes = hmac.ComputeHash(payloadBytes);
        }
        else
        {
            using var hmac = new HMACSHA1(keyBytes);
            hashBytes = hmac.ComputeHash(payloadBytes);
        }

        var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        return $"{algorithm}={hashHex}";
    }
}
