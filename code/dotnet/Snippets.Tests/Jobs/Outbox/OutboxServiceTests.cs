using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Snippets.Outbox;

namespace Snippets.Tests.Jobs.Outbox;

public sealed class OutboxServiceTests : IAsyncDisposable
{
    private const string TestKey = "test";

    private readonly DbContextOptions<OutboxDbContext> _dbOpt = new DbContextOptionsBuilder<OutboxDbContext>()
        .UseSqlite($"Data Source={Guid.NewGuid()}.db")
        .Options;

    private readonly OutboxService _service;

    private DateTime? _timestamp;

    public OutboxServiceTests()
    {
        var dbFactory = new OutboxDbFactory(_dbOpt);
        using var db = dbFactory.CreateDbContext();
        db.Database.EnsureCreatedAsync().Wait();

        var options = new OutboxServiceOptions { TimestampProvider = () => _timestamp ?? DateTime.UtcNow };

        _service = new OutboxService(dbFactory, options);
    }

    public async ValueTask DisposeAsync()
    {
        var dbFactory = new OutboxDbFactory(_dbOpt);
        await using var db = dbFactory.CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task GetNext_Ok()
    {
        // Arrange
        const int count = 5;
        var now = DateTime.UtcNow;
        await SeedJobs(10, now);

        // Act
        var @params = new OutboxGetParams
        {
            Key = TestKey,
            Count = count,
            Timeout = TimeSpan.FromMinutes(1),
        };

        var result = await _service.GetNext(@params);

        // Assert
        await using var db = new OutboxDbContext(_dbOpt);
        var actual = result.Cast<OutboxJobEntity>().ToList();
        var jobs = await db.Outbox.ToListAsync();

        Assert.Equal(count, result.Count);
        Assert.All(actual, j => Assert.True(j.NextVisibleTs >= now));

        Assert.Equal(jobs.Count - result.Count, jobs.Where(j => j.NextVisibleTs.HasValue).ToList().Count);
        Assert.Equal(jobs.Count - result.Count, jobs.Where(j => !j.NextVisibleTs.HasValue).ToList().Count);
    }

    [Fact]
    public async Task GetNext_Peek_Ok()
    {
        // Arrange
        await SeedJobs(20);

        // Act
        var @params = new OutboxGetParams
        {
            Key = TestKey,
            Count = 1,
            ShouldPeek = true,
        };

        var jobs = await _service.GetNext(@params);

        // Assert
        Assert.Single(jobs);

        var job = jobs.First();
        Assert.Null(job.Receipt);
    }

    [Fact]
    public async Task GetNext_Empty_Ok()
    {
        // Arrange
        await using var db = new OutboxDbContext(_dbOpt);

        // Act
        var @params = new OutboxGetParams
        {
            Key = TestKey,
            Count = 5,
            Timeout = TimeSpan.FromMinutes(1),
        };

        var result = await _service.GetNext(@params);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Resolve_Complete_Ok()
    {
        // Arrange
        var now = DateTime.UtcNow;
        await SeedJobs(10, now);
        var nextParams = new OutboxGetParams
        {
            Key = TestKey,
            Count = 1,
            Timeout = TimeSpan.FromMinutes(1),
        };

        var job = (await _service.GetNext(nextParams)).First();

        // Act
        _timestamp = now.AddMinutes(5);
        var resolveParams = new OutboxResolveParams
        {
            Result = new OutboxResult { Action = OutboxAction.Complete },
            Receipt = job.Receipt,
        };

        await _service.Resolve(job.Id, resolveParams);

        // Assert
        await using var db = new OutboxDbContext(_dbOpt);
        var actual = await db.Outbox.FindAsync(job.Id);
        Assert.NotNull(actual);
        Assert.Equal(OutboxStatus.Success, actual.Status);
        Assert.Equal(_timestamp, actual.CompletedTs);
        Assert.Equal(_timestamp, actual.UpdatedTs);
    }

    [Fact]
    public async Task Resolve_Update_Ok()
    {
        // Arrange
        var now = DateTime.UtcNow;
        await SeedJobs(10, now);
        var nextParams = new OutboxGetParams
        {
            Key = TestKey,
            Count = 1,
            Timeout = TimeSpan.FromMinutes(1),
        };

        var job = (await _service.GetNext(nextParams)).First();

        // Act
        _timestamp = now.AddMinutes(5);
        var resolveParams = new OutboxResolveParams
        {
            Result = new OutboxResult
            {
                Action = OutboxAction.Update,
                Metadata = JsonSerializer.SerializeToElement(new { Updated = true }),
            },
            Receipt = job.Receipt,
        };

        await _service.Resolve(job.Id, resolveParams);

        // Assert
        await using var db = new OutboxDbContext(_dbOpt);
        var actual = await db.Outbox.FindAsync(job.Id);
        Assert.NotNull(actual);
        Assert.Null(actual.Status);
        Assert.Null(actual.CompletedTs);
        Assert.Equal(_timestamp, actual.UpdatedTs);
        Assert.Equivalent(resolveParams.Result.Metadata, actual.Metadata);
    }

    [Fact]
    public async Task Activate_Ok()
    {
        // Arrange
        _timestamp = DateTime.UtcNow;
        await SeedJobs(20);

        var initParams = new OutboxGetParams
        {
            Key = TestKey,
            Count = 5,
            ShouldPeek = true,
        };

        var initJobs = await _service.GetNext(initParams);

        // Act
        var timeout = TimeSpan.FromSeconds(5);
        var @params = new OutboxActiveParams { Timeout = timeout };
        var results = await Task.WhenAll(initJobs.Select(j => _service.Activate(j.Id, @params)));
        var activatedJobs = results.Cast<OutboxJobEntity>().ToList();

        // Assert
        Assert.All(activatedJobs, j => Assert.Equal((uint)1, j.AttemptCount));
        Assert.All(activatedJobs, j => Assert.True(j.Receipt.HasValue));
        Assert.All(activatedJobs, j => Assert.True(j.NextVisibleTs >= _timestamp?.Add(timeout)));
    }

    private async Task SeedJobs(int count, DateTime? timestamp = null)
    {
        var now = timestamp ?? DateTime.UtcNow;
        await using var db = new OutboxDbContext(_dbOpt);

        db.Outbox.AddRange(
            Enumerable
                .Range(0, count)
                .Select(i => new OutboxJobEntity
                {
                    Key = TestKey,
                    Data = JsonSerializer.SerializeToElement(new { Test = true, Counter = i }),
                    CreatedTs = now,
                    UpdatedTs = now,
                })
        );

        await db.SaveChangesAsync();
    }

    internal class TestOutboxHandler : IOutboxHandler
    {
        public string Key => TestKey;
        public uint? MaxAttemptCount => null;
        public TimeSpan? Timeout => null;
        public uint? Concurrency => null;
        public bool ShouldPeek => false;

        public Task<OutboxResult> Handle(IOutboxContext ctx, CancellationToken ct = default)
        {
            var result = new OutboxResult { Action = OutboxAction.Unknown };
            return Task.FromResult(result);
        }
    }

    internal class TestTypedOutboxHandler : TypedOutboxHandler<TestJob>
    {
        public override string Key => TestKey;
        public override uint? MaxAttemptCount => null;
        public override TimeSpan? Timeout => null;
        public override uint? Concurrency => null;
        public override bool ShouldPeek => false;

        protected override Task<OutboxResult> Handle(
            IOutboxContext<IOutboxJob<TestJob, JsonElement>> ctx,
            CancellationToken ct = default
        )
        {
            throw new NotImplementedException();
        }
    }
}

internal record TestJob
{
    public required string Name { get; init; }

    public required float Value { get; init; }
}
