using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence;
using Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Eventiq.EventService.Tests;

public class LegendRepositoryTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5434;Database=eventiq_event;Username=eventiq;Password=eventiq123;Ssl Mode=Disable";

    private async Task<(EvtEventDbContext Db, LegendRepository Repo, IDbConnection Conn, IDbTransaction Tran, Event Event)>
        CreateRepositoryWithDataAsync(int legendsForMainEvent = 5, int legendsForOtherEvent = 2)
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var tran = await conn.BeginTransactionAsync();

        var options = new DbContextOptionsBuilder<EvtEventDbContext>()
            .UseNpgsql(conn)
            .UseSnakeCaseNamingConvention()
            .Options;

        var db = new EvtEventDbContext(options);
        db.Database.UseTransaction((DbTransaction)tran);

        var mainEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            OrganizationName = "Org Main",
            Name = "Main Event",
            Status = EventStatus.Draft,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2)
        };

        var otherEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            OrganizationName = "Org Other",
            Name = "Other Event",
            Status = EventStatus.Draft,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2)
        };

        db.Events.Add(mainEvent);
        db.Events.Add(otherEvent);

        var legendsMain = Enumerable.Range(1, legendsForMainEvent)
            .Select(i => new Legend
            {
                Id = Guid.NewGuid(),
                EventId = mainEvent.Id,
                Name = $"Legend {i}",
                Color = $"#{i:D2}{i:D2}{i:D2}",
                Price = 1000 + i
            });

        var legendsOther = Enumerable.Range(1, legendsForOtherEvent)
            .Select(i => new Legend
            {
                Id = Guid.NewGuid(),
                EventId = otherEvent.Id,
                Name = $"Other Legend {i}",
                Color = null,
                Price = 2000 + i
            });

        db.Legends.AddRange(legendsMain);
        db.Legends.AddRange(legendsOther);

        await db.SaveChangesAsync();

        var repo = new LegendRepository(conn);
        repo.SetTransaction(tran);

        return (db, repo, conn, tran, mainEvent);
    }

    private static async Task CleanupAsync(EvtEventDbContext db, IDbConnection conn, IDbTransaction tran)
    {
        try
        {
            await ((DbTransaction)tran).RollbackAsync();
        }
        finally
        {
            await db.DisposeAsync();
            await ((DbConnection)conn).DisposeAsync();
        }
    }

    [Fact]
    public async Task GetAllLegendsByEventIdAsync_Returns_Paginated_Data_And_Total()
    {
        var (db, repo, conn, tran, ev) = await CreateRepositoryWithDataAsync(legendsForMainEvent: 15, legendsForOtherEvent: 3);

        try
        {
            var page1 = await repo.GetAllLegendsByEventIdAsync(ev.Id, page: 1, size: 10);
            var page2 = await repo.GetAllLegendsByEventIdAsync(ev.Id, page: 2, size: 10);

            Assert.Equal(1, page1.Page);
            Assert.Equal(10, page1.Size);
            Assert.Equal(15, page1.Total);

            Assert.Equal(2, page2.Page);
            Assert.Equal(10, page2.Size);
            Assert.Equal(15, page2.Total);

            Assert.All(page1.Data.Concat(page2.Data), l => Assert.Equal(ev.Id, l.EventId));
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task AddAsync_Inserts_Legend_Within_Transaction_Then_Rollback()
    {
        var (db, repo, conn, tran, ev) = await CreateRepositoryWithDataAsync(legendsForMainEvent: 0, legendsForOtherEvent: 0);

        try
        {
            var legend = new Legend
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                Name = "New Legend",
                Color = "#FFFFFF",
                Price = 999
            };

            var affected = await repo.AddAsync(legend);
            Assert.Equal(1, affected);

            var fromDb = await db.Legends.AsNoTracking().FirstOrDefaultAsync(x => x.Id == legend.Id);
            Assert.NotNull(fromDb);
            Assert.Equal(legend.Name, fromDb!.Name);
            Assert.Equal(legend.Color, fromDb.Color);
            Assert.Equal(legend.Price, fromDb.Price);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }

        await using var verifyConn = new NpgsqlConnection(ConnectionString);
        await verifyConn.OpenAsync();

        var count = await verifyConn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM legends WHERE name = @Name",
            new { Name = "New Legend" });

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetLegendByIdEventIdAsync_Returns_Correct_Legend()
    {
        var (db, repo, conn, tran, ev) = await CreateRepositoryWithDataAsync(legendsForMainEvent: 3, legendsForOtherEvent: 0);

        try
        {
            var anyLegend = await db.Legends.AsNoTracking()
                .Where(l => l.EventId == ev.Id)
                .FirstAsync();

            LegendModel result = await repo.GetLegendByIdEventIdAsync(anyLegend.Id, ev.Id);

            Assert.NotNull(result);
            Assert.Equal(anyLegend.Id, result.Id);
            Assert.Equal(ev.Id, result.EventId);
            Assert.Equal(anyLegend.Name, result.Name);
            Assert.Equal(anyLegend.Color, result.Color);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task UpdatePartialAsync_Updates_Name_And_Color_Only()
    {
        var (db, repo, conn, tran, ev) = await CreateRepositoryWithDataAsync(legendsForMainEvent: 1, legendsForOtherEvent: 0);

        try
        {
            var legend = await db.Legends.FirstAsync(l => l.EventId == ev.Id);
            var originalPrice = legend.Price;

            var dto = new UpdateLegendDto
            {
                Name = "Updated Name",
                Color = "#ABCDEF",
                Price = 999999
            };

            var updated = await repo.UpdatePartialAsync(legend.Id, ev.Id, dto);

            Assert.NotNull(updated);
            Assert.Equal("Updated Name", updated!.Name);
            Assert.Equal("#ABCDEF", updated.Color);
            Assert.Equal(originalPrice, updated.Price);

            await db.Entry(legend).ReloadAsync();
            Assert.Equal("Updated Name", legend.Name);
            Assert.Equal("#ABCDEF", legend.Color);
            Assert.Equal(originalPrice, legend.Price);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task DeleteAsync_Deletes_Legend_When_Event_Is_Draft_And_Org_Matches()
    {
        var (db, repo, conn, tran, ev) = await CreateRepositoryWithDataAsync(legendsForMainEvent: 1, legendsForOtherEvent: 0);

        try
        {
            var legend = await db.Legends.FirstAsync(l => l.EventId == ev.Id);

            var affected = await repo.DeleteAsync(ev.Id, ev.OrganizationId, legend.Id);
            Assert.Equal(1, affected);

            var exists = await db.Legends.AsNoTracking().AnyAsync(l => l.Id == legend.Id);
            Assert.False(exists);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }
}

