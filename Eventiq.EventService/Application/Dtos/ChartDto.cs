namespace Eventiq.EventService.Dtos;

public class UpdateLChartDto
{
    public string? Name { get; set; }

}

public class CreateChartDto
{
    public string Name { get; set; }
}

public class ChartResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}
