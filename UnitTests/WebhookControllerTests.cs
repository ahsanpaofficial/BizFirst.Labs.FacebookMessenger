using BizFirstMetaMessenger.Controllers;
using BizFirstMetaMessenger.Models;
using BizFirstMetaMessenger.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using System.Text.Json;

namespace BizFirstMetaMessenger.Tests;

/// <summary>
/// Unit tests for WebhookController
/// Tests webhook verification and event processing endpoints
/// </summary>
public class WebhookControllerTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<WebhookController>> _mockLogger;
    private readonly Mock<IOptions<MetaConfiguration>> _mockConfig;
    private readonly Mock<ISignatureValidator> _mockSignatureValidator;
    private readonly Mock<IWebhookService> _mockWebhookService;
    private readonly WebhookController _controller;
    private const string TestVerifyToken = "my_verify_token_123";
    private const string TestAppSecret = "test_app_secret";

    public WebhookControllerTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<ILogger<WebhookController>>();
        _mockConfig = new Mock<IOptions<MetaConfiguration>>();
        _mockSignatureValidator = new Mock<ISignatureValidator>();
        _mockWebhookService = new Mock<IWebhookService>();

        _mockConfig.Setup(c => c.Value).Returns(new MetaConfiguration
        {
            VerifyToken = TestVerifyToken,
            AppSecret = TestAppSecret,
            PageAccessToken = "test_token"
        });

        _mockServiceProvider.Setup(sp => sp.GetService(typeof(ISignatureValidator)))
            .Returns(_mockSignatureValidator.Object);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IWebhookService)))
            .Returns(_mockWebhookService.Object);

        _controller = new WebhookController(_mockServiceProvider.Object, _mockConfig.Object, _mockLogger.Object);
    }

    [Fact(DisplayName = "GET: Should return challenge on valid verification")]
    public void VerifyWebhook_WithValidToken_ReturnsChallenge()
    {
        // Arrange
        var mode = "subscribe";
        var token = TestVerifyToken;
        var challenge = "test_challenge_123";

        // Act
        var result = _controller.VerifyWebhook(mode, token, challenge);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal(challenge, contentResult.Content);
        Assert.Equal("text/plain", contentResult.ContentType);
    }

    [Fact(DisplayName = "GET: Should return 403 on invalid token")]
    public void VerifyWebhook_WithInvalidToken_Returns403()
    {
        // Arrange
        var mode = "subscribe";
        var token = "wrong_token";
        var challenge = "test_challenge_123";

        // Act
        var result = _controller.VerifyWebhook(mode, token, challenge);

        // Assert
        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact(DisplayName = "GET: Should return 403 on invalid mode")]
    public void VerifyWebhook_WithInvalidMode_Returns403()
    {
        // Arrange
        var mode = "invalid_mode";
        var token = TestVerifyToken;
        var challenge = "test_challenge_123";

        // Act
        var result = _controller.VerifyWebhook(mode, token, challenge);

        // Assert
        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact(DisplayName = "GET: Should handle whitespace in parameters")]
    public void VerifyWebhook_WithWhitespace_TrimsAndValidates()
    {
        // Arrange
        var mode = "  subscribe  ";
        var token = $"  {TestVerifyToken}  ";
        var challenge = "  test_challenge_123  ";

        // Act
        var result = _controller.VerifyWebhook(mode, token, challenge);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("test_challenge_123", contentResult.Content);
    }

    [Fact(DisplayName = "POST: Should accept valid webhook with correct signature")]
    public async Task ReceiveWebhook_WithValidSignature_Returns200()
    {
        // Arrange
        var payload = "{\"object\":\"page\",\"entry\":[]}";
        var signature = "sha256=test_signature";

        var webhookEvent = new WebhookEventRequest
        {
            Object = "page",
            Entry = Array.Empty<JsonElement>()
        };

        _mockSignatureValidator.Setup(v => v.ValidateSignature(TestAppSecret, payload, signature))
            .Returns(true);

        _mockWebhookService.Setup(s => s.ProcessWebhookEventAsync(It.IsAny<WebhookEventRequest>()))
            .Returns(Task.CompletedTask);

        var context = new DefaultHttpContext();
        var bodyBytes = Encoding.UTF8.GetBytes(payload);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.Headers["X-Hub-Signature-256"] = signature;
        context.Request.ContentLength = bodyBytes.Length;
        _controller.ControllerContext = new ControllerContext { HttpContext = context };

        // Act
        var result = await _controller.ReceiveWebhook(webhookEvent);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact(DisplayName = "POST: Should reject webhook with invalid signature")]
    public async Task ReceiveWebhook_WithInvalidSignature_Returns403()
    {
        // Arrange
        var payload = "{\"object\":\"page\",\"entry\":[]}";
        var signature = "sha256=invalid_signature";

        var webhookEvent = new WebhookEventRequest
        {
            Object = "page",
            Entry = Array.Empty<JsonElement>()
        };

        _mockSignatureValidator.Setup(v => v.ValidateSignature(TestAppSecret, payload, signature))
            .Returns(false);

        var context = new DefaultHttpContext();
        var bodyBytes = Encoding.UTF8.GetBytes(payload);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.Headers["X-Hub-Signature-256"] = signature;
        context.Request.ContentLength = bodyBytes.Length;
        _controller.ControllerContext = new ControllerContext { HttpContext = context };

        // Act
        var result = await _controller.ReceiveWebhook(webhookEvent);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact(DisplayName = "POST: Should reject webhook without signature")]
    public async Task ReceiveWebhook_WithoutSignature_Returns400()
    {
        // Arrange
        var payload = "{\"object\":\"page\",\"entry\":[]}";

        var webhookEvent = new WebhookEventRequest
        {
            Object = "page",
            Entry = Array.Empty<JsonElement>()
        };

        var context = new DefaultHttpContext();
        var bodyBytes = Encoding.UTF8.GetBytes(payload);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;
        _controller.ControllerContext = new ControllerContext { HttpContext = context };

        // Act
        var result = await _controller.ReceiveWebhook(webhookEvent);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact(DisplayName = "POST/Test: Should accept webhook without signature verification")]
    public async Task TestWebhook_WithoutSignature_Returns200()
    {
        // Arrange
        var webhookEvent = new WebhookEventRequest
        {
            Object = "page",
            Entry = Array.Empty<JsonElement>()
        };

        _mockWebhookService.Setup(s => s.ProcessWebhookEventAsync(It.IsAny<WebhookEventRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.TestWebhook(webhookEvent);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }
}
