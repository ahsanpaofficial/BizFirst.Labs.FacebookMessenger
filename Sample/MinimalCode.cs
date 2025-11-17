using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MyFbWebhookDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacebookWebhookController : ControllerBase
    {
        private readonly ILogger<FacebookWebhookController> _logger;
        private const string VERIFY_TOKEN = "MySecretVerifyToken123";  // set this to match your FB App dashboard

        public FacebookWebhookController(ILogger<FacebookWebhookController> logger)
        {
            _logger = logger;
        }

        // GET: api/FacebookWebhook?hub.mode=subscribe&hub.challenge=123456&hub.verify_token=MySecretVerifyToken123
        [HttpGet]
        public IActionResult Get([FromQuery] string hub_mode, [FromQuery] string hub_challenge, [FromQuery] string hub_verify_token)
        {
            _logger.LogInformation("Received verification request: mode={mode}, challenge={challenge}, verify_token={token}",
                hub_mode, hub_challenge, hub_verify_token);

            if (hub_mode == "subscribe" && hub_verify_token == VERIFY_TOKEN)
            {
                // When FB verifies, we respond with the challenge
                return Content(hub_challenge, "text/plain");
            }
            else
            {
                return Forbid();
            }
        }

        // POST: api/FacebookWebhook
        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            _logger.LogInformation("Received webhook POST from Facebook: {body}", body);

            // You could deserialize the JSON and act on it
            // Example: var data = JsonSerializer.Deserialize<FacebookWebhookPayload>(body);

            // For demo, just log and return 200 OK
            return Ok();
        }
    }

    // Example payload class (you’d expand based on the actual fields)
    public class FacebookWebhookPayload
    {
        public string @object { get; set; }
        public Entry[] entry { get; set; }
    }

    public class Entry
    {
        public string id { get; set; }
        public long time { get; set; }
        public Change[] changes { get; set; }
    }

    public class Change
    {
        public string field { get; set; }
        public Value value { get; set; }
    }

    public class Value
    {
        public string verb { get; set; }
        // other fields depending on subscription
    }
}