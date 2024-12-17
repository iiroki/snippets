using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Snippets.Outbox;

public class OutboxServiceOptions
{
    public Func<DateTime>? TimestampProvider { get; init; }
}

/// <summary>
/// Outbox job service implemented with EF Core.
/// </summary>
/// <seealso href="https://microservices.io/patterns/data/transactional-outbox.html">
/// Pattern: Transactional outbox
/// </seealso>
public class OutboxService(IDbContextFactory<OutboxDbContext> dbFactory, OutboxServiceOptions? options = null)
    : IOutboxService
{
    private readonly IDbContextFactory<OutboxDbContext> _dbFactory = dbFactory;
    private readonly OutboxServiceOptions _options = options ?? new OutboxServiceOptions();

    public async Task<List<IOutboxJobWrapper>> GetNext(OutboxGetParams @params, CancellationToken ct = default)
    {
        var now = GetTimestamp();

        FormattableString sql = $"""
            WITH next AS (
                SELECT *
                FROM outbox
                WHERE
                   key = {@params.Key} AND
                   (next_visible_ts IS NULL OR next_visible_ts <= {now}) AND
                   status IS NULL
                ORDER BY id
                LIMIT {@params.Count}
            )
            UPDATE outbox
            SET
                attempt_count = attempt_count + {(@params.ShouldPeek ? 0 : 1)},
                next_visible_ts = {now.Add(@params.Timeout ?? TimeSpan.Zero)},
                receipt = {(@params.ShouldPeek ? null : Guid.NewGuid())},
                updated_ts = {now}
            WHERE id IN (SELECT id FROM next)
            RETURNING *;
            """;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var jobs = await db.Outbox.FromSql(sql).AsNoTracking().ToListAsync(ct);
        return jobs.Cast<IOutboxJobWrapper>().ToList();
    }

    public async Task<IOutboxJobWrapper> Resolve(long id, OutboxResolveParams @params, CancellationToken ct = default)
    {
        var now = GetTimestamp();
        if (@params.Receipt.HasValue)
        {
            var status = @params.Result.Action switch
            {
                OutboxAction.Complete => OutboxStatus.Success,
                OutboxAction.Error => OutboxStatus.Error,
                // JobStatus.Update => null,
                _ => OutboxStatus.Unknown,
            };

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await Update(db, id, @params.Receipt.Value, status, now, null, ct);
        }

        throw new OutboxException($"Could not update outbox job without a receipt - ID: {id}");
    }

    public async Task<IOutboxJobWrapper> Activate(
        long id,
        OutboxActiveParams? @params = null,
        CancellationToken ct = default
    )
    {
        var now = GetTimestamp();

        FormattableString sql = $"""
            UPDATE outbox
            SET
                attempt_count = attempt_count + 1,
                next_visible_ts = {now.Add(@params?.Timeout ?? TimeSpan.Zero)},
                receipt = {Guid.NewGuid()},
                updated_ts = {now}
            WHERE
                id = {id} AND
                receipt IS NULL
            RETURNING *;
            """;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var activated = await db.Outbox.FromSql(sql).AsNoTracking().ToListAsync(ct);
        if (activated.Count == 0)
        {
            throw new OutboxException($"Could not active outbox job - ID: {id}");
        }

        return activated.First();
    }

    private DateTime GetTimestamp() => _options.TimestampProvider?.Invoke() ?? DateTime.UtcNow;

    private static async Task<OutboxJobEntity> Update(
        OutboxDbContext db,
        long id,
        Guid receipt,
        OutboxStatus status,
        DateTime now,
        JsonElement? metadata,
        CancellationToken ct = default
    )
    {
        var isCompleted = status != OutboxStatus.Unknown;

        FormattableString sql = $"""
            UPDATE outbox
            SET
                status = {status},
                metadata = COALESCE({metadata}, metadata),
                completed_ts = {(isCompleted ? now : null)},
                updated_ts = {now}
            WHERE
                id = {id} AND
                receipt = {receipt} AND
                status IS NULL
            RETURNING *;
            """;

        var updated = await db.Outbox.FromSql(sql).AsNoTracking().ToListAsync(ct);
        if (updated.Count == 0)
        {
            throw new OutboxException($"Could not update outbox job - ID: {id}, Receipt: ${receipt}");
        }

        return updated.First();
    }
}

public class OutboxException(string msg) : Exception(msg);
