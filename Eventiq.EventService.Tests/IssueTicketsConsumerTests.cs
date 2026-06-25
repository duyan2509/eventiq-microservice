using Eventiq.Contracts;
using Eventiq.EventService.Application.Service.Interface;
using Eventiq.EventService.Consumers;
using Eventiq.EventService.Domain.Entity;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Eventiq.EventService.Tests;

public class IssueTicketsConsumerTests
{
    private readonly ITicketService _ticketService;
    private readonly IssueTicketsConsumer _consumer;

    private static readonly Guid OrderId   = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId    = Guid.NewGuid();

    public IssueTicketsConsumerTests()
    {
        _ticketService = Substitute.For<ITicketService>();
        _ticketService
            .IssueAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<List<(Guid, string, string, decimal)>>())
            .Returns([]);

        _consumer = new IssueTicketsConsumer(_ticketService, NullLogger<IssueTicketsConsumer>.Instance);
    }

    private static IssueTicketsCommand MakeCommand(int seatCount = 2) => new()
    {
        OrderId   = OrderId,
        UserId    = UserId,
        SessionId = SessionId,
        Seats = Enumerable.Range(1, seatCount).Select(i => new IssueTicketSeat
        {
            SeatId     = Guid.NewGuid(),
            SeatLabel  = $"A{i}",
            LegendName = "Standard",
            Price      = 50m * i
        }).ToList()
    };

    private static ConsumeContext<IssueTicketsCommand> MakeContext(IssueTicketsCommand cmd)
    {
        var ctx = Substitute.For<ConsumeContext<IssueTicketsCommand>>();
        ctx.Message.Returns(cmd);
        return ctx;
    }

    // ── 1. ITicketService.IssueAsync is called with correct params ───────────
    [Fact]
    public async Task Consume_CallsTicketService_WithCorrectOrderAndSession()
    {
        var cmd = MakeCommand();
        await _consumer.Consume(MakeContext(cmd));

        await _ticketService.Received(1).IssueAsync(
            OrderId,
            SessionId,
            Arg.Is<List<(Guid SeatId, string SeatLabel, string LegendName, decimal Price)>>(
                list => list.Count == cmd.Seats.Count));
    }

    // ── 2. Seat details (label, legend, price) are forwarded correctly ───────
    [Fact]
    public async Task Consume_ForwardsSeatDetailsToTicketService()
    {
        var cmd = MakeCommand(seatCount: 1);
        var seat = cmd.Seats[0];

        await _consumer.Consume(MakeContext(cmd));

        await _ticketService.Received(1).IssueAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Is<List<(Guid SeatId, string SeatLabel, string LegendName, decimal Price)>>(
                list => list[0].SeatId     == seat.SeatId     &&
                        list[0].SeatLabel  == seat.SeatLabel  &&
                        list[0].LegendName == seat.LegendName &&
                        list[0].Price      == seat.Price));
    }

    // ── 3. TicketsIssued is published after service call ─────────────────────
    [Fact]
    public async Task Consume_PublishesTicketsIssued_WithCorrectOrderId()
    {
        var cmd = MakeCommand();
        var ctx = MakeContext(cmd);

        await _consumer.Consume(ctx);

        await ctx.Received(1).Publish(Arg.Is<TicketsIssued>(m => m.OrderId == OrderId));
    }

    // ── 4. TicketsIssued is NOT published if IssueAsync throws ───────────────
    [Fact]
    public async Task Consume_WhenTicketServiceThrows_DoesNotPublishTicketsIssued()
    {
        _ticketService
            .IssueAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<List<(Guid, string, string, decimal)>>())
            .Returns(Task.FromException<List<Ticket>>(new InvalidOperationException("DB error")));

        var ctx = MakeContext(MakeCommand());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _consumer.Consume(ctx));

        await ctx.DidNotReceive().Publish(Arg.Any<TicketsIssued>());
    }

    // ── 5. Multiple seats in one command are all forwarded ────────────────────
    [Fact]
    public async Task Consume_MultipleSeats_AllForwardedToTicketService()
    {
        var cmd = MakeCommand(seatCount: 5);
        await _consumer.Consume(MakeContext(cmd));

        await _ticketService.Received(1).IssueAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Is<List<(Guid, string, string, decimal)>>(list => list.Count == 5));
    }
}
