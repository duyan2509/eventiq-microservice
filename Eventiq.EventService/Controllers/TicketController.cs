using Eventiq.EventService.Application.Dto;
using Eventiq.EventService.Application.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.EventService.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId)
    {
        var tickets = await _ticketService.GetByOrderAsync(orderId);
        return Ok(tickets.Select(TicketResponse.From));
    }

    [HttpPost("{ticketId:guid}/checkin")]
    [Authorize(Roles = "Staff,OrgOwner")]
    public async Task<IActionResult> CheckIn(Guid ticketId)
    {
        var staffId = Guid.Parse(User.FindFirst("sub")!.Value);
        await _ticketService.CheckInAsync(ticketId, staffId);
        return Ok();
    }
}
