using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Application.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.SeatService.Controllers;

[ApiController]
[Route("api/seat-maps/{seatMapId:guid}/versions")]
[Authorize]
public class SeatMapVersionController : ControllerBase
{
    private readonly ISeatDesignService _designService;
    private readonly IUnitOfWork _uow;
    private readonly AutoMapper.IMapper _mapper;

    public SeatMapVersionController(
        ISeatDesignService designService,
        IUnitOfWork uow,
        AutoMapper.IMapper mapper)
    {
        _designService = designService;
        _uow = uow;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetVersions(Guid seatMapId)
    {
        var versions = await _uow.Versions.GetBySeatMapIdAsync(seatMapId);
        var result = _mapper.Map<List<SeatMapVersionResponse>>(versions);
        return Ok(result);
    }

    [HttpGet("{versionId:guid}")]
    public async Task<IActionResult> GetVersion(Guid seatMapId, Guid versionId)
    {
        var version = await _uow.Versions.GetByIdAsync(versionId);
        if (version == null || version.SeatMapId != seatMapId)
            throw new NotFoundException("Version not found.");

        var result = _mapper.Map<SeatMapVersionDetailResponse>(version);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SaveVersion(Guid seatMapId, [FromBody] CreateVersionDto dto)
    {
        var userId = GetUserId();
        var result = await _designService.AutoSaveSnapshotAsync(seatMapId, userId, dto.ChangeDescription);
        return CreatedAtAction(nameof(GetVersion), new { seatMapId, versionId = result.Id }, result);
    }

    [HttpPost("{versionId:guid}/restore")]
    public async Task<IActionResult> RestoreVersion(Guid seatMapId, Guid versionId)
    {
        var version = await _uow.Versions.GetByIdAsync(versionId);
        if (version == null || version.SeatMapId != seatMapId)
            throw new NotFoundException("Version not found.");

        // Return the snapshot for client-side restore
        // The client will apply the snapshot and sync via SignalR
        var result = _mapper.Map<SeatMapVersionDetailResponse>(version);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : throw new UnauthorizedException("Invalid user.");
    }
}
