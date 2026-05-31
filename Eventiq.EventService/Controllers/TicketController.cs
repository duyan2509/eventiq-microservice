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

    [HttpGet("events/{eventId:guid}/checkins")]
    [Authorize(Roles = "Staff,Organization")]
    public async Task<IActionResult> GetEventCheckIns(Guid eventId)
    {
        var items = await _ticketService.GetCheckedInByEventAsync(eventId);
        return Ok(items);
    }

    [HttpPost("checkin")]
    [Authorize(Roles = "Staff,Organization")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest req)
    {
        var staffId = Guid.Parse(User.FindFirst("sub")!.Value);
        var ticket = await _ticketService.CheckInAsync(req.Token, staffId);
        return Ok(TicketResponse.From(ticket));
    }
}

public record CheckInRequest(string Token);
