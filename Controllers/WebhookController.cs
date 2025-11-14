using System.Text;
using BizFirstMetaMessenger.Models;
using BizFirstMetaMessenger.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BizFirstMetaMessenger.Controllers;

/// <summary>
/// Controller for Meta Messenger webhook endpoints
/// </summary>
[ApiController]
[Route("webhook")]
public class WebhookController : ControllerBase
{
    private readonly IServiceProvider _services;
    private readonly MetaConfiguration _config;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IServiceProvider services,
        IOptions<MetaConfiguration> config,
        ILogger<WebhookController> logger)
    {
        _services = services;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Webhook verification endpoint - Meta uses this to verify your webhook URL
    /// </summary>
    [HttpGet]
    [Produces("text/plain")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string verifyToken,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        _logger.LogInformation("=== Webhook Verification Request ===");

        // Trim whitespace
        mode = mode?.Trim() ?? string.Empty;
        verifyToken = verifyToken?.Trim() ?? string.Empty;
        challenge = challenge?.Trim() ?? string.Empty;

        _logger.LogInformation("Mode: {Mode}, Token: {Token}, Challenge: {Challenge}", mode, verifyToken, challenge);

        // Validate
        if (mode == "subscribe" && verifyToken == _config.VerifyToken)
        {
            _logger.LogInformation("✓ Verification successful");
            return Content(challenge, "text/plain");
        }

        _logger.LogWarning("✗ Verification failed");
        return StatusCode(403);
    }

    /// <summary>
    /// Webhook event receiver - Receives events from Meta Messenger
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> ReceiveWebhook([FromBody] WebhookEventRequest webhookEvent)
    {
        _logger.LogInformation("=== Webhook Event Received ===");

        var signatureValidator = _services.GetRequiredService<ISignatureValidator>();
        var webhookService = _services.GetRequiredService<IWebhookService>();

        // Read raw body for signature validation
        Request.Body.Position = 0;
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();

        // Get signature headers
        Request.Headers.TryGetValue("X-Hub-Signature-256", out var sig256);
        Request.Headers.TryGetValue("X-Hub-Signature", out var sig);
        var signatureHeader = string.IsNullOrEmpty(sig256) ? sig.ToString() : sig256.ToString();

        _logger.LogInformation("Payload size: {Size} bytes", rawBody.Length);

        // Validate signature
        if (string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Missing signature header");
            return BadRequest(new { error = "Missing signature header" });
        }

        if (!signatureValidator.ValidateSignature(_config.AppSecret ?? string.Empty, rawBody, signatureHeader))
        {
            _logger.LogWarning("Invalid signature");
            return StatusCode(403, new { error = "Invalid signature" });
        }

        _logger.LogInformation("✓ Signature validated");

        // Process the webhook
        if (webhookEvent == null)
        {
            return BadRequest(new { error = "Invalid webhook data" });
        }

        webhookEvent.RawBody = rawBody;
        webhookEvent.SignatureHeader = signatureHeader;

        await webhookService.ProcessWebhookEventAsync(webhookEvent);

        _logger.LogInformation("✓ Webhook processed successfully");

        return Ok(new { status = "received" });
    }

    /// <summary>
    /// Test webhook endpoint - No signature validation (Development only)
    /// </summary>
    [HttpPost("test")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> TestWebhook([FromBody] WebhookEventRequest webhookEvent)
    {
        _logger.LogInformation("=== Test Webhook (Dev Mode) ===");

        var webhookService = _services.GetRequiredService<IWebhookService>();

        if (webhookEvent == null)
        {
            return BadRequest(new { error = "Invalid webhook data" });
        }

        await webhookService.ProcessWebhookEventAsync(webhookEvent);

        _logger.LogInformation("✓ Test webhook processed");

        return Ok(new
        {
            status = "received",
            message = "Test webhook processed successfully",
            eventObject = webhookEvent.Object,
            entryCount = webhookEvent.Entry?.Length ?? 0
        });
    }
}
