namespace Eventiq.EventService.Dtos;

public class PaginatedResult<T>
{
    public IEnumerable<T> Data { get; set; } = null!;
    public int Total { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
}
