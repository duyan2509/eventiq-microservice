using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Application.Service;

public interface IMemberService
{
    Task<PaginatedResult<MemberReponse>> GetMembersAsync(Guid orgId, int page =1, int size =10,  CancellationToken cancellationToken = default);
    Task<MemberReponse> ChangeMemberPermissionsAsync(Guid ownerId, Guid memberId, Guid orgId, ChangePermission dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteMemberAsync(Guid ownerId, Guid memberId, Guid orgId, CancellationToken cancellationToken = default);
}


    