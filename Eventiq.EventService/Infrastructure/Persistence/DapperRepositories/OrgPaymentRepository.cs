using System.Data;
using Dapper;
using Eventiq.EventService.Domain.Repositories;

namespace Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;

public class OrgPaymentRepository : BaseRepository, IOrgPaymentRepository
{
    public OrgPaymentRepository(IDbConnection connection) : base(connection)
    {
    }

    public async Task<bool> HasActivePaymentAsync(Guid orgId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT is_active
            FROM org_payment_info
            WHERE organization_id = @OrgId
            LIMIT 1;
        ";

        var result = await _connection.QueryFirstOrDefaultAsync<bool?>(
            sql,
            new { OrgId = orgId },
            transaction: _transaction);

        return result == true;
    }

    public async Task UpsertAsync(Guid orgId, string stripeAccountId, bool isActive, DateTime updatedAt, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO org_payment_info (organization_id, stripe_account_id, is_active, updated_at)
            VALUES (@OrgId, @StripeAccountId, @IsActive, @UpdatedAt)
            ON CONFLICT (organization_id)
            DO UPDATE SET
                stripe_account_id = EXCLUDED.stripe_account_id,
                is_active         = EXCLUDED.is_active,
                updated_at        = EXCLUDED.updated_at;
        ";

        await _connection.ExecuteAsync(
            sql,
            new { OrgId = orgId, StripeAccountId = stripeAccountId, IsActive = isActive, UpdatedAt = updatedAt },
            transaction: _transaction);
    }
}
