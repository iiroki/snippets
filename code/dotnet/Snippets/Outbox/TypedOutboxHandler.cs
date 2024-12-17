using System.Text.Json;

namespace Snippets.Outbox;

/// <inheritdoc cref="TypedOutboxHandler{TData,TMetadata}" />
public abstract class TypedOutboxHandler<TData> : TypedOutboxHandler<TData, JsonElement>;

/// <summary>
/// Outbox handler that deserializes the incoming jobs to strong types.
/// </summary>
public abstract class TypedOutboxHandler<TData, TMetadata>(JsonSerializerOptions? jsonOpt = null) : IOutboxHandler
{
    private readonly JsonSerializerOptions? _jsonOpt = jsonOpt;

    public abstract string Key { get; }
    public abstract uint? MaxAttemptCount { get; }
    public abstract TimeSpan? Timeout { get; }
    public abstract uint? Concurrency { get; }
    public abstract bool ShouldPeek { get; }

    public async Task<OutboxResult> Handle(IOutboxContext ctx, CancellationToken ct = default)
    {
        var typedJob = new TypedOutboxJob(ctx.Job, _jsonOpt);
        var typedCtx = new TypedOutboxContext(typedJob, ctx);
        return await Handle(typedCtx, ct);
    }

    protected abstract Task<OutboxResult> Handle(
        IOutboxContext<IOutboxJob<TData, TMetadata>> ctx,
        CancellationToken ct = default
    );

    private class TypedOutboxJob(IOutboxJob job, JsonSerializerOptions? jsonOpt = null) : IOutboxJob<TData, TMetadata>
    {
        public string Key { get; } = job.Key;
        public TData Data { get; } = job.Data.Deserialize<TData>(jsonOpt)!;
        public TMetadata Metadata { get; } = job.Metadata.Deserialize<TMetadata>(jsonOpt)!;
    }

    private class TypedOutboxContext(TypedOutboxJob job, IOutboxContext ctx) : IOutboxContext<TypedOutboxJob>
    {
        public TypedOutboxJob Job { get; } = job;

        public IOutboxJobProperties Properties => ctx.Properties;

        public bool IsPeeked => ctx.IsPeeked;

        public Func<Task> Activate => ctx.Activate;
    }
}
