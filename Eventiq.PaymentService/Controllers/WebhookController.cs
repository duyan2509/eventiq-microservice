using Eventiq.PaymentService.Application.Service.Interface;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Eventiq.PaymentService.Controllers;

[ApiController]
[Route("api/stripe")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookService _webhookService;

    public WebhookController(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Handle()
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();
        try
        {
            await _webhookService.HandleAsync(payload, signature);
            return Ok();
        }
        catch (StripeException ex)
        {
            // Signature/parse failure — already recorded for tracing. 400 (do not look
            // like a server crash). Processing errors are NOT caught here: they bubble
            // up to 500 so Stripe retries.
            return BadRequest(new { error = ex.Message });
        }
    }
}
