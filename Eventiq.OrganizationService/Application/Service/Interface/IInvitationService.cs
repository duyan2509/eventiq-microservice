using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Application.Service;

public interface IInvitationService
{
    Task<InviationResponse> AddInvitationAsync(string userEmail, Guid userId, Guid orgId, InvitationDto dto, CancellationToken cancellationToken = default);
    Task<PaginatedResult<InviationResponse>> GetOrgInvitationsAsync(Guid ownerId, Guid orgId, int page=1, int size =10 ,CancellationToken cancellationToken = default);
    Task<PaginatedResult<InviationResponse>> GetUserInvitationsAsync(Guid userId, int page=1, int size =10,CancellationToken cancellationToken = default);
    Task<InviationResponse> AcceptInvitationAsync(Guid userId, Guid orgId, Guid invitationId, CancellationToken cancellationToken = default);
    Task<InviationResponse> RejectInvitationAsync(Guid userId, Guid orgId, Guid invitationId, CancellationToken cancellationToken = default);
    Task<InviationResponse> CancelInvitationAsync(Guid userId, Guid orgId, Guid invitationId, CancellationToken cancellationToken = default);
}
