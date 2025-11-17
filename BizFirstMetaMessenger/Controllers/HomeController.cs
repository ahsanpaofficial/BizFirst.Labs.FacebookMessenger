using BizFirstMetaMessenger.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BizFirstMetaMessenger.Controllers;

/// <summary>
/// Controller for frontend pages and status
/// </summary>
[ApiController]
[Route("")]
public class HomeController : ControllerBase
{
    private readonly MetaConfiguration _config;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IOptions<MetaConfiguration> config, ILogger<HomeController> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Root endpoint that displays server status
    /// </summary>
    /// <returns>HTML page with server status</returns>
    [HttpGet]
    [Produces("text/html")]
    public IActionResult Index()
    {
        _logger.LogInformation("Home page accessed");

        var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Meta Messenger Webhook Server</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 50px; background-color: #f0f0f0; }
        .container { background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); max-width: 600px; margin: 0 auto; }
        .status { color: #28a745; font-size: 24px; font-weight: bold; margin-bottom: 20px; }
        .info { margin-top: 20px; padding: 15px; background-color: #e7f3ff; border-left: 4px solid #2196F3; }
        h1 { color: #333; }
        ul { line-height: 1.8; }
        .swagger-link { margin-top: 20px; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; }
        a { color: #007bff; text-decoration: none; font-weight: bold; }
        a:hover { text-decoration: underline; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Meta Messenger Webhook Server</h1>
        <div class='status'>✓ The server is running properly</div>
        <div class='info'>
            <strong>Available Endpoints:</strong>
            <ul>
                <li><strong>GET /webhook</strong> - Webhook verification endpoint</li>
                <li><strong>POST /webhook</strong> - Webhook event receiver</li>
            </ul>
            <strong>Configuration Status:</strong>
            <ul>
                <li>VerifyToken: " + (string.IsNullOrEmpty(_config.VerifyToken) ? "❌ NOT SET" : "✓ Configured") + @"</li>
                <li>AppSecret: " + (string.IsNullOrEmpty(_config.AppSecret) ? "❌ NOT SET" : "✓ Configured") + @"</li>
            </ul>
        </div>
        <div class='swagger-link'>
            <strong>📚 API Documentation:</strong><br/>
            <a href='/swagger' target='_blank'>Open Swagger UI</a>
        </div>
    </div>
</body>
</html>";

        return Content(html, "text/html");
    }
}
