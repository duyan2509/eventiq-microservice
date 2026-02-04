using Eventiq.OrganizationService.Application.Service;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.OrganizationService.Controllers;

[ApiController]
[Route("api/organizations")]
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

    public OrganizationController(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await _organizationService.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _organizationService.GetByIdAsync(id, cancellationToken);
        if (item == null)
            return NotFound();
        return Ok(item);
    }
}
