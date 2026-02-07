using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Eventiq.OrganizationService.Extensions;

public static class DbUpdateExceptionExtensions
{
    public static bool IsUniqueConstraintViolation(this DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx
               && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
