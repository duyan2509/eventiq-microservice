using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Domain.Repositories;

public interface IInvitationRepository
{
    Task<PaginatedResult<InviationResponse>> GetOrgInvitationsAsync(Guid orgId, int page, int size, CancellationToken cancellationToken = default);
    Task<PaginatedResult<InviationResponse>> GetUserInvitationsAsync(Guid userId, int page, int size, CancellationToken cancellationToken = default);
    Task<Invitation?> GetInvitatioByIDAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Invitation? invitation, CancellationToken cancellationToken = default);
    Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default);

}