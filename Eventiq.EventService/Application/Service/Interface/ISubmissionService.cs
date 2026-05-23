using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public interface ISubmissionService
{
    Task<PaginatedResult<SubmissionResponse>> GetAllSubmissionByEventIdAsync(Guid userId, Guid eventId, int page = 1, int size = 20);
    Task<SubmissionResponse> SubmitEventAsync(Guid userId, Guid orgId, Guid eventId);
    Task<SubmissionResponse> AcceptEventAsync(Guid userId, string adminEmail, Guid eventId, UpdateSubmissioDto dto);
    Task<SubmissionResponse> RejectEventAsync(Guid userId, string adminEmail, Guid eventId, UpdateSubmissioDto dto);
    Task<SubmissionResponse> CancelEventAsync(Guid userId, string userEmail, Guid orgId, Guid eventId, UpdateSubmissioDto dto);
}


