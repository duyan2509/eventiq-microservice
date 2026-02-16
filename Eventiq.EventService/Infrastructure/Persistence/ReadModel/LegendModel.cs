namespace Eventiq.EventService.Infrastructure.Persistence.ReadModel;

public class LegendModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Color { get; set; }
    public int Price { get; set; }
    public Guid EventId { get; set; }
}