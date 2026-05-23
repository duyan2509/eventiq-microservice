using Eventiq.PaymentService.Application.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.PaymentService.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _checkoutService;

    public CheckoutController(ICheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var url = await _checkoutService.CreateAsync(userId, request.SessionId, request.SeatIds);
        return Ok(new { checkoutUrl = url });
    }
}

public record CreateCheckoutRequest(Guid SessionId, List<Guid> SeatIds);
