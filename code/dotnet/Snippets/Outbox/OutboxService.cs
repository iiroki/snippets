using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Snippets.Database;

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
            OutboxStatus? status = @params.Result.Action switch
            {
                OutboxAction.Complete => OutboxStatus.Success,
                OutboxAction.Error => OutboxStatus.Error,
                OutboxAction.Update => null,
                _ => OutboxStatus.Unknown,
            };

            return await Update(id, @params.Receipt.Value, status, now, @params.Result.Metadata, ct);
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

    private async Task<OutboxJobEntity> Update(
        long id,
        Guid receipt,
        OutboxStatus? status,
        DateTime now,
        JsonElement? metadata,
        CancellationToken ct = default
    )
    {
        var isCompleted = status is not (null or OutboxStatus.Unknown);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var converter = db.Model.FindValueConverter<OutboxJobEntity>(nameof(OutboxJobEntity.Metadata));

        FormattableString sql = $"""
            UPDATE @@table
            SET
                status = {status},
                metadata = COALESCE({converter?.ConvertToProvider(metadata)}, metadata),
                completed_ts = {(isCompleted ? now : null)},
                updated_ts = {now}
            WHERE
                id = {id} AND
                receipt = {receipt} AND
                status IS NULL
            RETURNING *;
            """;

        var updated = await db.Outbox.FromSqlWithTable(sql).AsNoTracking().ToListAsync(ct);
        if (updated.Count == 0)
        {
            throw new OutboxException($"Could not update outbox job - ID: {id}, Receipt: ${receipt}");
        }

        return updated.First();
    }
}

public class OutboxException(string msg) : Exception(msg);
