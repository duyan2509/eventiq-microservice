using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Domain.Repositories;

public interface ISubmissionRepository
{
    Task<PaginatedResult<SubmissionModel>> GetAllSubmissionsByEventIdAsync(Guid eventId);
    Task AddAsync(Guid eventId, Submission submission);
}

