using Eventiq.Contracts;
using Eventiq.Contracts.Grpc;
using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Domain.Entity;
using Eventiq.PaymentService.Domain.Enums;
using Eventiq.PaymentService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Eventiq.PaymentService.Application.Service.Implement;

public class CheckoutService : ICheckoutService
{
    // Stripe requires expires_at >= 30 min from session creation; the extra 2 min
    // covers gRPC latency and clock drift between PaymentService and SeatService.
    private static readonly TimeSpan CheckoutHoldExtension = TimeSpan.FromMinutes(32);

    private readonly PaymentDbContext _dbContext;
    private readonly EventInternal.EventInternalClient _eventClient;
    private readonly SeatInternal.SeatInternalClient _seatClient;
    private readonly OrgInternal.OrgInternalClient _orgClient;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfiguration _config;

    public CheckoutService(
        PaymentDbContext dbContext,
        EventInternal.EventInternalClient eventClient,
        SeatInternal.SeatInternalClient seatClient,
        OrgInternal.OrgInternalClient orgClient,
        IPublishEndpoint publishEndpoint,
        IConfiguration config)
    {
        _dbContext = dbContext;
        _eventClient = eventClient;
        _seatClient = seatClient;
        _orgClient = orgClient;
        _publishEndpoint = publishEndpoint;
        _config = config;
    }

    public async Task<string> CreateAsync(Guid userId, Guid sessionId, List<Guid> seatIds)
    {
        // 1. Validate seats are still Holding by this user
        var seatRequest = new GetSeatsRequest();
        seatRequest.SeatIds.AddRange(seatIds.Select(id => id.ToString()));
        var seatsResponse = await _seatClient.GetSeatsAsync(seatRequest);

        foreach (var seat in seatsResponse.Seats)
        {
            if (seat.Status != "Holding" || seat.HeldBy != userId.ToString())
                throw new BusinessException($"Seat {seat.Label} is no longer held by current user");
        }

        // 2. Get legend prices from EventService
        var legendIds = seatsResponse.Seats
            .Where(s => !string.IsNullOrEmpty(s.LegendId))
            .Select(s => s.LegendId)
            .Distinct()
            .ToList();

        var legendRequest = new GetLegendsRequest();
        legendRequest.LegendIds.AddRange(legendIds);
        var legendResponse = await _eventClient.GetLegendsAsync(legendRequest);
        var legendMap = legendResponse.Legends.ToDictionary(l => l.LegendId);

        // 3. Get session + event info
        var sessionInfo = await _eventClient.GetSessionAsync(
            new GetSessionRequest { SessionId = sessionId.ToString() });

        // 4. Get org Stripe account + platform fee rate
        var paymentStatus = await _orgClient.GetPaymentStatusAsync(
            new GetPaymentStatusRequest { OrgId = sessionInfo.OrgId });
        if (!paymentStatus.IsActive)
            throw new BusinessException("Organization Stripe account is not configured");

        var platformConfig = await _orgClient.GetPlatformConfigAsync(new());

        // 5. Idempotency: reuse the existing open Stripe session ONLY when it covers the
        //    exact same seats. A stale Pending order (e.g. user backed out then picked
        //    different seats) must be abandoned, otherwise the old session's seats/total
        //    would be shown instead of the new selection.
        var existingOrder = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == userId && o.SessionId == sessionId
                                      && o.Status == OrderStatus.Pending);
        if (existingOrder != null)
        {
            StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];

            var existingSeatIds = existingOrder.Items.Select(i => i.SeatId).OrderBy(x => x).ToList();
            var requestedSeatIds = seatIds.OrderBy(x => x).ToList();
            var sameSeats = existingSeatIds.SequenceEqual(requestedSeatIds);

            var existing = await new SessionService().GetAsync(existingOrder.StripeSessionId);
            if (sameSeats && existing.Status == "open")
                return existing.Url;

            // Different seats (or no longer open): abandon the stale checkout so it can't be paid.
            if (existing.Status == "open")
                await new SessionService().ExpireAsync(existingOrder.StripeSessionId);
            existingOrder.Status = OrderStatus.Failed;
            await _dbContext.SaveChangesAsync();
        }

        // 6. Build seat snapshot + totals
        var items = seatsResponse.Seats.Select(seat =>
        {
            var legend = legendMap.GetValueOrDefault(seat.LegendId);
            return new OrderItem
            {
                SeatId = Guid.Parse(seat.SeatId),
                SeatLabel = seat.Label,
                LegendName = legend?.Name ?? string.Empty,
                Price = legend != null ? (decimal)legend.Price : 0m
            };
        }).ToList();

        var total = items.Sum(i => i.Price);
        var platformFee = Math.Round(total * (decimal)platformConfig.CurrentFeeRate, 2);

        // 7. Save Order (before Stripe call to get orderId for metadata)
        var order = new Order
        {
            UserId = userId,
            OrgId = Guid.Parse(sessionInfo.OrgId),
            SessionId = sessionId,
            Status = OrderStatus.Pending,
            TotalAmount = total,
            PlatformFee = platformFee,
            EventName = sessionInfo.EventName,
            SessionName = sessionInfo.SessionName,
            SessionDate = DateTime.Parse(sessionInfo.StartTime, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
            Items = items
        };

        // 8. Extend the seat hold to cover the Stripe checkout window before creating
        //    a new session, so the hold never expires while the session is still open.
        var extendRequest = new ExtendHoldRequest
        {
            UserId = userId.ToString(),
            DurationSeconds = (int)CheckoutHoldExtension.TotalSeconds
        };
        extendRequest.SeatIds.AddRange(seatIds.Select(id => id.ToString()));
        var extendResponse = await _seatClient.ExtendHoldAsync(extendRequest);
        if (!extendResponse.Success)
            throw new BusinessException(extendResponse.Message);

        var expiresAt = DateTime.Parse(extendResponse.HeldUntil, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

        // 9. Create Stripe Checkout Session
        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var frontendBase = _config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        // Per-Order idempotency: same Order retry returns same Stripe session;
        // a new Order (e.g. after the previous one expired) gets a fresh key.
        var idempotencyKey = order.Id.ToString();

        var options = new SessionCreateOptions
        {
            ExpiresAt = expiresAt,
            PaymentMethodTypes = ["card"],
            LineItems = items.Select(item => new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "usd",
                    UnitAmount = (long)(item.Price * 100), // cents
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"{item.SeatLabel} - {item.LegendName}"
                    }
                },
                Quantity = 1
            }).ToList(),
            Mode = "payment",
            SuccessUrl = $"{frontendBase}/payment/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{frontendBase}/payment/cancel",
            ClientReferenceId = userId.ToString(),
            Metadata = new Dictionary<string, string> { ["order_id"] = order.Id.ToString() },
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                ApplicationFeeAmount = (long)(platformFee * 100), // cents
                TransferData = new SessionPaymentIntentDataTransferDataOptions
                {
                    Destination = paymentStatus.StripeAccountId
                }
            }
        };

        var session = await new SessionService().CreateAsync(options,
            new RequestOptions { IdempotencyKey = idempotencyKey });

        order.StripeSessionId = session.Id;
        _dbContext.Orders.Add(order);

        // Publish before SaveChangesAsync so the outbox message is saved atomically with the Order.
        // Calling SaveChangesAsync first and Publish second loses the message (EF outbox never persisted).
        await _publishEndpoint.Publish(new BookingInitiated
        {
            OrderId = order.Id,
            UserId = userId,
            SessionId = sessionId,
            SeatIds = seatIds
        });
        await _dbContext.SaveChangesAsync();

        return session.Url;
    }
}
