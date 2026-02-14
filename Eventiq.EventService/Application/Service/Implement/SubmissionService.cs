using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public class SubmissionService : ISubmissionService
{
    public Task<PaginatedResult<SubmissionResponse>> GetAllSubmissionByEventIdAsync(Guid userId, Guid eventId)
    {
        throw new NotImplementedException();
    }

    public Task<SubmissionResponse> SubmitEventAsync(Guid userId, Guid eventId, CreateSubmissionDto dto)
    {
        throw new NotImplementedException();
    }

    public Task<SubmissionResponse> AcceptEventAsync(Guid userId, Guid eventId, UpdateSubmissioDto dto)
    {
        throw new NotImplementedException();
    }

    public Task<SubmissionResponse> RejectEventAsync(Guid userId, Guid eventId, UpdateSubmissioDto dto)
    {
        throw new NotImplementedException();
    }
}
