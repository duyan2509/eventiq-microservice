using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Domain.Repositories;

public interface IMemberRepository
{
    Task AddAsync(Member? member, CancellationToken cancellationToken=default);
    Task UpdateAsync(Member? member, CancellationToken cancellationToken=default);

    Task RemoveAsync(Member? member, CancellationToken cancellationToken=default);
    Task<Member?> GetAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<PaginatedResult<MemberReponse>> GetOrgMembersAsync(Guid orgId, int page = 1, int size =10, CancellationToken cancellationToken = default);
}