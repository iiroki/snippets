namespace Snippets.Outbox;

using ActivateFn = Func<Task<IOutboxJob>>;

public class OutboxContext(IOutboxJob job, IOutboxJobProperties metadata, ActivateFn activateFn)
    : OutboxContext<IOutboxJob>(job, metadata, activateFn),
        IOutboxContext
{
    public OutboxContext(IOutboxJobWrapper job, ActivateFn activateFn)
        : this(job, job, activateFn)
    {
        // NOP
    }
}

public class OutboxContext<TJob>(TJob job, IOutboxJobProperties metadata, Func<Task<TJob>> activateFn)
    : IOutboxContext<TJob>
{
    private readonly Func<Task<TJob>> _activateFn = activateFn;

    public TJob Job { get; private set; } = job;

    public IOutboxJobProperties Properties { get; } = metadata;

    public Func<Task> Activate => async () => Job = await _activateFn();

    public bool IsPeeked => Properties.Receipt == null;
}
