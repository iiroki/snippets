using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Snippets.Outbox;

/// <summary>
/// Outbox job for implementing "outbox pattern".
/// </summary>
/// <seealso href="https://microservices.io/patterns/data/transactional-outbox.html">
/// Pattern: Transactional outbox
/// </seealso>
[PrimaryKey(nameof(Id))]
[Index(nameof(Key))]
public class OutboxJobEntity : IOutboxJobWrapper
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; init; }

    public required string Key { get; init; }

    public uint AttemptCount { get; init; }

    public Guid? Receipt { get; init; }

    public required JsonElement Data { get; init; }

    public JsonElement Metadata { get; init; }

    public required DateTime CreatedTs { get; init; }

    public required DateTime UpdatedTs { get; init; }

    public DateTime? NextVisibleTs { get; init; }

    public DateTime? CompletedTs { get; init; }

    public OutboxStatus? Status { get; init; }
}

public enum OutboxStatus
{
    Unknown,
    Success,
    Error,
}
