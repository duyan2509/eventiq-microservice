namespace Eventiq.EventService.Dtos;

public class UpdateLegendDto
{
    public string? Name { get; set; }
    public string? Color { get; set; }
    public int? Price { get; set; }
}

public class CreateLegendDto
{
    public string Name { get; set; }
    public string? Color { get; set; }
    public int Price { get; set; }
}

public class LegendResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Color { get; set; }
    public int Price { get; set; }
}
