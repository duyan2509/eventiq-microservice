using Eventiq.PaymentService.Application.Dto;
using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.PaymentService.Controllers;

[ApiController]
[Route("api/payments/admin/webhooks")]
[Authorize(Roles = "Admin")]
public class WebhookAdminController : ControllerBase
{
    private readonly IWebhookAdminService _service;

    public WebhookAdminController(IWebhookAdminService service) => _service = service;

    // GET /api/payments/admin/webhooks?status=Failed&page=1&size=20
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] WebhookEventStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var items = await _service.GetAsync(status, page, size);
        return Ok(items.Select(WebhookEventSummary.From));
    }

    // GET /api/payments/admin/webhooks/{id} — full payload for tracing
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var ev = await _service.GetByIdAsync(id);
        return ev == null ? NotFound() : Ok(WebhookEventDetail.From(ev));
    }
}
