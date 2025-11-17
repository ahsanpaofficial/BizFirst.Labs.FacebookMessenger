# Facebook Graph API Webhooks — PoC Guide

Great — implementing a proof of concept (PoC) for Facebook Graph API Webhooks is a very good learning exercise. I’ll walk you through:

1. A super‑simple explanation of how it works
2. A minimal C# sample
3. What you need to do to make a working PoC

---

## 1. What are Webhooks in Facebook’s Graph API

**Here’s the gist:**

Facebook provides Webhooks so your server can be notified when certain events happen in the Facebook ecosystem (e.g., a Page gets a new post, comments, etc.).

**The sequence is roughly:**

- You set up a callback URL (an HTTPS endpoint) in your app’s dashboard.
- Facebook will verify your endpoint by sending a GET request with a “challenge” parameter (`hub.challenge`) and expect your endpoint to echo back that challenge.
- Once verified & configured (with which page/object + what fields you subscribe to), Facebook will send `POST` requests to your callback URL when the subscribed event occurs (with JSON payloads).

**Advantages:**

- Your server doesn’t have to constantly poll Facebook. Instead, it “listens” for push notifications.

**Important:**

- The callback URL must be publicly accessible via HTTPS (not just `localhost`), and must respond correctly to the verification.

---

## 2. Minimal C# Sample

Here’s a very minimal ASP.NET Core Web API controller example in C# to handle Facebook Webhook verification and event notifications. (You can adapt it for ASP.NET MVC or older Web API versions.)

```csharp
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class FacebookWebhookController : ControllerBase
{
    private const string VERIFY_TOKEN = "your_verify_token_here";

    // Verification endpoint that Facebook calls with the challenge
    [HttpGet]
    public IActionResult Get([
        FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
    {
        if (mode == "subscribe" && verifyToken == VERIFY_TOKEN)
        {
            // Must return the challenge as plain text
            return Content(challenge, "text/plain");
        }

        return Forbid();
    }

    // Event notifications will be POSTed here
    [HttpPost]
    public async Task<IActionResult> Post()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        // TODO: log and/or parse the JSON body and act on it

        // Return quickly with 200 OK so Facebook considers it delivered
        return Ok();
    }
}
```

### Explanation of the sample

- The `GET` method handles the verification challenge from Facebook. Facebook will send something like:

```
GET /api/FacebookWebhook?hub.mode=subscribe&hub.challenge=XYZ&hub.verify_token=foobar
```

You check the `verify_token` against your configured token, and if good, echo back the challenge.

- The `POST` method is where Facebook will send event notifications. Here we just read the body (in real life you’d parse and act on it).
- You must set the `VERIFY_TOKEN` constant to the same token you configured in the Facebook App Webhooks setup.

---

## 3. PoC Steps

1. **Create a Facebook App**
   - Go to the Meta Platforms Developer dashboard, add a new app.
   - Under **Products**, add **Webhooks** (or choose the Webhooks product if that’s the current UI).

2. **Deploy your webhook endpoint with HTTPS**
   - Facebook requires a publicly accessible HTTPS URL (localhost typically won’t work unless you expose it via a tunnel like `ngrok`).
   - Example deployment options: Azure App Service, any public hosting provider, or local dev using `ngrok` pointing to your local port.
   - Ensure your endpoint is reachable, e.g. `https://mydomain.com/api/FacebookWebhook`.

3. **Configure the Webhook in the App Settings**
   - In your Facebook App dashboard → **Webhooks** → **Add Subscription**.
   - Provide the callback URL (from step 2).
   - Set the **Verify Token** to exactly the `VERIFY_TOKEN` you used in your code.
   - Select the object(s) to subscribe to (Page, User, etc.) and which fields to watch.
   - Facebook will ping the `GET` endpoint and expect the correct response (your echo logic). If you fail, you’ll get the error: `The URL couldn't be validated. Response does not match challenge.`

4. **Subscribe a Page (if needed)**
   - If you want Page events (posts/comments) to be sent, you may need to subscribe the Page to your app — for example via Graph API: `POST /{page-id}/subscribed_apps`.
   - Ensure you have the required permissions.

5. **Trigger an event and observe the POST**
   - Once setup is verified, perform an action on the Page (e.g., post a new status).
   - Facebook should send a `POST` request to your webhook URL with a JSON body — your code logs or processes it.
   - In your code you can deserialize the JSON, inspect which field changed, and take action (store in DB, send email, etc.).

6. **Test & inspect**
   - Use a tool like `ngrok` to view inbound requests and payloads during local development.
   - Log full request body & headers so you can debug what Facebook sends.
   - Make sure your webhook returns `200 OK` quickly (within Facebook’s delivery time limits).

---

## 4. Key Pitfalls / Tips

- Make sure the response to the verification `GET` is just the challenge string, and it is `text/plain`. If there’s extra whitespace, BOM (Byte Order Mark), or an HTML wrapper, it will fail. Many folks get the **Response does not match challenge** because of BOM.
- Use a valid HTTPS endpoint — Facebook requires a trusted SSL certificate.
- For production, validate incoming `POST` payloads (e.g., verify a signature header, check clientState/token) to ensure the request is genuinely from Facebook.
- Keep your `verify_token` secret — it helps validate the `GET` request.
- Logging is invaluable during PoC.
- Ensure your webhook endpoint handles `POST` requests quickly — don’t perform long-running work synchronously; queue it and respond with `200 OK` promptly.
- Facebook’s documentation states your app may need to go through App Review for certain permissions/fields.

---

If you like, I can create a full sample project (a Visual Studio solution) with a ready‑made endpoint, a simple parse of the payload, and instructions to test it (including using `ngrok`).

