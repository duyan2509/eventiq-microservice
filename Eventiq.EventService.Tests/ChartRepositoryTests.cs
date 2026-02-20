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

public class ChartRepositoryTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5434;Database=eventiq_event;Username=eventiq;Password=eventiq123;Ssl Mode=Disable";

    private async Task<(EvtEventDbContext Db, ChartRepository Repo, IDbConnection Conn, IDbTransaction Tran, Event Event)>
        CreateRepositoryWithDataAsync(int chartsForMainEvent = 5, int chartsForOtherEvent = 2)
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

        var chartsMain = Enumerable.Range(1, chartsForMainEvent)
            .Select(i => new Chart
            {
                Id = Guid.NewGuid(),
                EventId = mainEvent.Id,
                Name = $"Chart {i}"
            });

        var chartsOther = Enumerable.Range(1, chartsForOtherEvent)
            .Select(i => new Chart
            {
                Id = Guid.NewGuid(),
                EventId = otherEvent.Id,
                Name = $"Other Chart {i}"
            });

        db.Charts.AddRange(chartsMain);
        db.Charts.AddRange(chartsOther);

        await db.SaveChangesAsync();

        var repo = new ChartRepository(conn);
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
    public async Task GetAllChartsByEventIdAsync_Returns_Paginated_Data_And_Total()
    {
        var (db, repo, conn, tran, ev) = await CreateRepositoryWithDataAsync(chartsForMainEvent: 15, chartsForOtherEvent: 3);

        try
        {
            var page1 = await repo.GetAllChartsByEventIdAsync(ev.Id, page: 1, size: 10);
            var page2 = await repo.GetAllChartsByEventIdAsync(ev.Id, page: 2, size: 10);

            Assert.Equal(1, page1.Page);
            Assert.Equal(10, page1.Size);
            Assert.Equal(15, page1.Total);

            Assert.Equal(2, page2.Page);
            Assert.Equal(10, page2.Size);
            Assert.Equal(15, page2.Total);

            Assert.All(page1.Data.Concat(page2.Data), c => Assert.Equal(ev.Id, c.EventId));
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task AddAsync_Inserts_Chart_Within_Transaction_Then_Rollback()
    {
        var (db, repo, conn, tran, ev) = await CreateRepositoryWithDataAsync(chartsForMainEvent: 0, chartsForOtherEvent: 0);

        try
        {
            var chart = new Chart
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                Name = "New Chart"
            };

            var affected = await repo.AddAsync(ev.Id, chart);
            Assert.Equal(1, affected);

            var fromDb = await db.Charts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == chart.Id);
            Assert.NotNull(fromDb);
            Assert.Equal(chart.Name, fromDb!.Name);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }

        await using var verifyConn = new NpgsqlConnection(ConnectionString);
        await verifyConn.OpenAsync();

        var count = await verifyConn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM charts WHERE name = @Name",
            new { Name = "New Chart" });

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task UpdatePartialAsync_Updates_Name_Only()
    {
        var (db, repo, conn, tran, ev) = await CreateRepositoryWithDataAsync(chartsForMainEvent: 1, chartsForOtherEvent: 0);

        try
        {
            var chart = await db.Charts.FirstAsync(c => c.EventId == ev.Id);

            var dto = new UpdateChartDto
            {
                Name = "Updated Chart Name"
            };

            var updated = await repo.UpdatePartialAsync(chart.Id, ev.Id, dto);

            Assert.NotNull(updated);
            Assert.Equal("Updated Chart Name", updated!.Name);
            Assert.Equal(chart.Id, updated.Id);
            Assert.Equal(ev.Id, updated.EventId);

            await db.Entry(chart).ReloadAsync();
            Assert.Equal("Updated Chart Name", chart.Name);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task DeleteAsync_Deletes_Chart_When_Event_Is_Draft_And_Org_Matches()
    {
        var (db, repo, conn, tran, ev) = await CreateRepositoryWithDataAsync(chartsForMainEvent: 1, chartsForOtherEvent: 0);

        try
        {
            var chart = await db.Charts.FirstAsync(c => c.EventId == ev.Id);

            var affected = await repo.DeleteAsync(ev.Id, ev.OrganizationId, chart.Id);
            Assert.Equal(1, affected);

            var exists = await db.Charts.AsNoTracking().AnyAsync(c => c.Id == chart.Id);
            Assert.False(exists);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task GetAllChartsByEventIdAsync_Returns_Only_Charts_For_Specified_Event()
    {
        var (db, repo, conn, tran, ev) = await CreateRepositoryWithDataAsync(chartsForMainEvent: 3, chartsForOtherEvent: 5);

        try
        {
            var result = await repo.GetAllChartsByEventIdAsync(ev.Id, page: 1, size: 10);

            Assert.Equal(3, result.Total);
            Assert.Equal(3, result.Data.Count());
            Assert.All(result.Data, c => Assert.Equal(ev.Id, c.EventId));
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }
}
