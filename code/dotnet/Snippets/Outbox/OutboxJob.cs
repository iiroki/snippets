using System.Text.Json;

namespace Snippets.Outbox;

internal class OutboxJob(OutboxJobEntity entity) : IOutboxJobWrapper
{
    public long Id => entity.Id;
    public string Key => entity.Key;
    public JsonElement Data => entity.Data;
    public JsonElement Metadata => entity.Metadata;
    public uint AttemptCount => entity.AttemptCount;
    public Guid? Receipt => entity.Receipt;
}
