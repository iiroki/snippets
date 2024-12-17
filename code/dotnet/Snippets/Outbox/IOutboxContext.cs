namespace Snippets.Outbox;

public interface IOutboxContext : IOutboxContext<IOutboxJob>;

public interface IOutboxContext<out TJob> : IOutboxContextBase
{
    TJob Job { get; }
}

public interface IOutboxContextBase
{
    IOutboxJobProperties Properties { get; }

    bool IsPeeked { get; }

    Func<Task> Activate { get; }
}
