using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Infrastructure.Persistence;
using Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Eventiq.EventService.Tests;

public class SessionRepositoryTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5434;Database=eventiq_event;Username=eventiq;Password=eventiq123;Ssl Mode=Disable";

    private async Task<(EvtEventDbContext Db, SessionRepository Repo, IDbConnection Conn, IDbTransaction Tran, Event Event, Chart Chart)>
        CreateRepositoryWithDataAsync(int sessionsForMainEvent = 5, int sessionsForOtherEvent = 2)
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

        var mainChart = new Chart
        {
            Id = Guid.NewGuid(),
            EventId = mainEvent.Id,
            Name = "Main Chart"
        };

        var otherChart = new Chart
        {
            Id = Guid.NewGuid(),
            EventId = otherEvent.Id,
            Name = "Other Chart"
        };

        db.Charts.Add(mainChart);
        db.Charts.Add(otherChart);

        var sessionsMain = Enumerable.Range(1, sessionsForMainEvent)
            .Select(i => new Session
            {
                Id = Guid.NewGuid(),
                EventId = mainEvent.Id,
                ChartId = mainChart.Id,
                Name = $"Session {i}",
                StartTime = DateTime.UtcNow.AddHours(i),
                EndTime = DateTime.UtcNow.AddHours(i + 1)
            });

        var sessionsOther = Enumerable.Range(1, sessionsForOtherEvent)
            .Select(i => new Session
            {
                Id = Guid.NewGuid(),
                EventId = otherEvent.Id,
                ChartId = otherChart.Id,
                Name = $"Other Session {i}",
                StartTime = DateTime.UtcNow.AddHours(i + 10),
                EndTime = DateTime.UtcNow.AddHours(i + 11)
            });

        db.Sessions.AddRange(sessionsMain);
        db.Sessions.AddRange(sessionsOther);

        await db.SaveChangesAsync();

        var repo = new SessionRepository(conn);
        repo.SetTransaction(tran);

        return (db, repo, conn, tran, mainEvent, mainChart);
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
    public async Task GetAllSessionsByEventIdAsync_Returns_Paginated_Data_And_Total()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 15, sessionsForOtherEvent: 3);

        try
        {
            var page1 = await repo.GetAllSessionsByEventIdAsync(ev.Id, page: 1, size: 10);
            var page2 = await repo.GetAllSessionsByEventIdAsync(ev.Id, page: 2, size: 10);

            Assert.Equal(1, page1.Page);
            Assert.Equal(10, page1.Size);
            Assert.Equal(15, page1.Total);

            Assert.Equal(2, page2.Page);
            Assert.Equal(10, page2.Size);
            Assert.Equal(15, page2.Total);

            Assert.All(page1.Data.Concat(page2.Data), s => Assert.Equal(ev.Id, s.EventId));
            Assert.All(page1.Data.Concat(page2.Data), s => Assert.NotNull(s.ChartName));
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task GetAllSessionsByEventIdAsync_Returns_Only_Sessions_For_Specified_Event()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 3, sessionsForOtherEvent: 5);

        try
        {
            var result = await repo.GetAllSessionsByEventIdAsync(ev.Id, page: 1, size: 10);

            Assert.Equal(3, result.Total);
            Assert.Equal(3, result.Data.Count());
            Assert.All(result.Data, s => Assert.Equal(ev.Id, s.EventId));
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task GetAllSessionsByEventIdAsync_Includes_Chart_Name()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 2, sessionsForOtherEvent: 0);

        try
        {
            var result = await repo.GetAllSessionsByEventIdAsync(ev.Id, page: 1, size: 10);

            Assert.Equal(2, result.Data.Count());
            Assert.All(result.Data, s =>
            {
                Assert.Equal(chart.Id, s.ChartId);
                Assert.Equal(chart.Name, s.ChartName);
            });
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task DeleteAsync_Deletes_Session_When_Event_Is_Draft_And_Org_Matches()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 1, sessionsForOtherEvent: 0);

        try
        {
            var session = await db.Sessions.FirstAsync(s => s.EventId == ev.Id);

            var affected = await repo.DeleteAsync(ev.Id, ev.OrganizationId, session.Id);
            Assert.Equal(1, affected);

            var exists = await db.Sessions.AsNoTracking().AnyAsync(s => s.Id == session.Id);
            Assert.False(exists);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task DeleteAsync_Returns_Zero_When_Session_Not_Found()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 0, sessionsForOtherEvent: 0);

        try
        {
            var nonExistentSessionId = Guid.NewGuid();
            var affected = await repo.DeleteAsync(ev.Id, ev.OrganizationId, nonExistentSessionId);
            Assert.Equal(0, affected);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Correct_Session()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 1, sessionsForOtherEvent: 0);

        try
        {
            var session = await db.Sessions.FirstAsync(s => s.EventId == ev.Id);

            var result = await repo.GetByIdAsync(session.Id);

            Assert.NotNull(result);
            Assert.Equal(session.Id, result!.Id);
            Assert.Equal(session.Name, result.Name);
            Assert.Equal(session.EventId, result.EventId);
            Assert.Equal(session.ChartId, result.ChartId);
            Assert.True(
                Math.Abs((session.StartTime - result.StartTime).TotalMilliseconds) < 1);

            Assert.True(
                Math.Abs((session.EndTime - result.EndTime).TotalMilliseconds) < 1);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Null_When_Session_Not_Found()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 0, sessionsForOtherEvent: 0);

        try
        {
            var nonExistentSessionId = Guid.NewGuid();
            var result = await repo.GetByIdAsync(nonExistentSessionId);
            Assert.Null(result);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task AddAsync_Inserts_New_Session_For_Event()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 0, sessionsForOtherEvent: 0);

        try
        {
            var newSession = new Session
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                ChartId = chart.Id,
                Name = "New Session",
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2)
            };

            var affected = await repo.AddAsync(ev.Id, newSession);
            Assert.Equal(1, affected);

            var inserted = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == newSession.Id);
            Assert.NotNull(inserted);
            Assert.Equal(newSession.Name, inserted!.Name);
            Assert.Equal(ev.Id, inserted.EventId);
            Assert.Equal(chart.Id, inserted.ChartId);
            Assert.True(Math.Abs((newSession.StartTime - inserted.StartTime).TotalMilliseconds) < 1);
            Assert.True(Math.Abs((newSession.EndTime - inserted.EndTime).TotalMilliseconds) < 1);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task UpdateAsync_Updates_Session_Fields()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 1, sessionsForOtherEvent: 0);

        try
        {
            var session = await db.Sessions.FirstAsync(s => s.EventId == ev.Id);

            var newChart = new Chart
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                Name = "Updated Chart"
            };
            db.Charts.Add(newChart);
            await db.SaveChangesAsync();

            session.Name = "Updated Name";
            session.StartTime = DateTime.UtcNow.AddHours(5);
            session.EndTime = DateTime.UtcNow.AddHours(6);
            session.ChartId = newChart.Id;

            var affected = await repo.UpdateAsync(session);
            Assert.Equal(1, affected);

            var updated = await db.Sessions.AsNoTracking().FirstAsync(s => s.Id == session.Id);
            Assert.Equal("Updated Name", updated.Name);
            Assert.Equal(newChart.Id, updated.ChartId);
            Assert.True(Math.Abs((session.StartTime - updated.StartTime).TotalMilliseconds) < 1);
            Assert.True(Math.Abs((session.EndTime - updated.EndTime).TotalMilliseconds) < 1);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task CheckOverlappedAsync_Returns_False_When_No_Overlap()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 2, sessionsForOtherEvent: 0);

        try
        {
            var latestEnd = await db.Sessions
                .Where(s => s.EventId == ev.Id)
                .MaxAsync(s => s.EndTime);

            var candidateStart = latestEnd.AddHours(1);
            var candidateEnd = candidateStart.AddHours(1);

            var hasOverlap = await repo.CheckOverlappedAsync(ev.Id, Guid.NewGuid(), candidateStart, candidateEnd);
            Assert.False(hasOverlap);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task CheckOverlappedAsync_Returns_True_When_Overlap_With_Other_Session()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 2, sessionsForOtherEvent: 0);

        try
        {
            var existing = await db.Sessions.Where(s => s.EventId == ev.Id).OrderBy(s => s.StartTime).FirstAsync();

            var candidateStart = existing.StartTime.AddMinutes(15);
            var candidateEnd = existing.EndTime.AddMinutes(15);

            var hasOverlap = await repo.CheckOverlappedAsync(ev.Id, Guid.NewGuid(), candidateStart, candidateEnd);
            Assert.True(hasOverlap);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }

    [Fact]
    public async Task CheckOverlappedAsync_Ignores_Same_Session_When_Updating()
    {
        var (db, repo, conn, tran, ev, chart) = await CreateRepositoryWithDataAsync(sessionsForMainEvent: 2, sessionsForOtherEvent: 0);

        try
        {
            var existing = await db.Sessions.Where(s => s.EventId == ev.Id).OrderBy(s => s.StartTime).FirstAsync();

            var hasOverlap = await repo.CheckOverlappedAsync(ev.Id, existing.Id, existing.StartTime, existing.EndTime);
            Assert.False(hasOverlap);
        }
        finally
        {
            await CleanupAsync(db, conn, tran);
        }
    }
}
