namespace Eventiq.OrganizationService.Dtos;
public class UpdatePermissionDto
{
    public string? Name { get; set; }
    public bool? IsDesigner { get; set; } = false;
}

public class PermissionDto
{
    public string Name { get; set; }
    public bool IsDesigner { get; set; } = false;
}

public class PermissionResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public bool IsDesigner { get; set; } = false;
}