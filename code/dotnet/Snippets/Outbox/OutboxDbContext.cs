using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Snippets.Database;

namespace Snippets.Outbox;

public class OutboxDbContext(DbContextOptions<OutboxDbContext> opt, JsonSerializerOptions? jsonOpt = null)
    : DbContext(opt)
{
    private readonly JsonSerializerOptions? _jsonOpt = jsonOpt;

    public DbSet<OutboxJobEntity> Outbox { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder builder)
    {
        builder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var converter = new JsonStringConverter(_jsonOpt);
        builder.HasGlobalValueConverter<JsonElement>(converter);
    }
}

public class OutboxDbFactory(DbContextOptions<OutboxDbContext> opt) : IDbContextFactory<OutboxDbContext>
{
    public OutboxDbContext CreateDbContext() => new(opt);
}
