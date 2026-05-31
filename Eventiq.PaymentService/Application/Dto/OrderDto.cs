using Eventiq.PaymentService.Domain.Entity;
using Eventiq.PaymentService.Domain.Enums;

namespace Eventiq.PaymentService.Application.Dto;

public record OrderResponse(
    Guid Id,
    Guid SessionId,
    string EventName,
    string SessionName,
    DateTime SessionDate,
    string Status,
    decimal TotalAmount,
    decimal PlatformFee,
    DateTime CreatedAt,
    DateTime? PaidAt,
    List<OrderItemResponse> Items)
{
    public static OrderResponse From(Order o) => new(
        o.Id, o.SessionId, o.EventName, o.SessionName, o.SessionDate,
        o.Status.ToString(), o.TotalAmount, o.PlatformFee,
        o.CreatedAt, o.PaidAt,
        o.Items.Select(OrderItemResponse.From).ToList());
}

public record OrderItemResponse(Guid SeatId, string SeatLabel, string LegendName, decimal Price)
{
    public static OrderItemResponse From(OrderItem i) => new(i.SeatId, i.SeatLabel, i.LegendName, i.Price);
}

public record TicketDetailDto(
    string Id,
    string SeatLabel,
    string LegendName,
    decimal Price,
    string QrCode,
    bool IsCheckedIn,
    DateTime IssuedAt,
    DateTime? CheckedInAt);
