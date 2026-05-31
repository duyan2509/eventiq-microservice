using Eventiq.PaymentService.Application.Dto;
using Eventiq.PaymentService.Application.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.PaymentService.Controllers;

[ApiController]
[Route("api/payments/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var orders = await _orderService.GetMyOrdersAsync(userId);
        return Ok(orders.Select(OrderResponse.From));
    }

    [HttpGet("{orderId:guid}/tickets")]
    public async Task<IActionResult> GetTickets(Guid orderId)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var tickets = await _orderService.GetTicketsByOrderAsync(orderId, userId);
        return Ok(tickets);
    }
}
