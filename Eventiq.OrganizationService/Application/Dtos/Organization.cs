namespace Eventiq.OrganizationService.Dtos;
public class OrganizationDetail
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public int Size { get; set; } = 1;
    public bool isOwner { get; set; } = false;
    public Guid Id { get; set; }
}
public class OrganizationDto
{
    public string Name { get; set; }
    public string? Description { get; set; }
}

public class UpdateOrganizationDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public class OrganizationResponse: OrganizationDto
{
    public Guid Id { get; set; }
}
