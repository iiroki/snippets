using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snippets.Schedule;

namespace Snippets.Tests.Schedule;

public class ScheduledWorkerTests
{
    [Fact]
    public async Task NoSchedule_Disabled()
    {
        // Arrange
        var ctSource = new CancellationTokenSource();
        var worker = new TestScheduledWorker(null); // No schedule

        // Act
        await worker.StartAsync(ctSource.Token);

        // Assert
        Assert.True(worker.IsDisabled);
        Assert.True(worker.ExecuteTask?.IsCompletedSuccessfully);
        Assert.Empty(worker.Invocations);
    }

    [Fact]
    public async Task ValidSchedule_Invoked()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging(opt =>
        {
            opt.AddConsole();
            opt.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = services.BuildServiceProvider().GetRequiredService<ILogger<ScheduledWorkerTests>>();
        var ctSource = new CancellationTokenSource();
        var worker = new TestScheduledWorker("@every_second", logger: logger);

        // Act
        _ = worker.StartAsync(ctSource.Token);
        await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
        await ctSource.CancelAsync();
        if (worker.ExecuteTask != null)
        {
            await worker.ExecuteTask;
        }

        // Assert
        Assert.False(worker.IsDisabled);
        Assert.True(worker.ExecuteTask?.IsCompletedSuccessfully);
        Assert.True(
            worker.Invocations.Count is >= 4 and <= 6,
            $"Invocation count should be 5 +- 1 (Actual: {worker.Invocations.Count})"
        );
    }
}

public class TestScheduledWorker(
    string? schedule,
    Func<DateTime>? nowProvider = null,
    TimeSpan? timeout = null,
    ILogger? logger = null
) : ScheduledWorker(schedule, nowProvider, logger)
{
    private readonly TimeSpan? _timeout = timeout;

    public List<(DateTime, DateTime)> Invocations { get; } = [];

    protected override async Task InvokeAsync(DateTime ts, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return;
        }

        Invocations.Add((ts, DateTime.UtcNow));
        if (_timeout.HasValue)
        {
            await Task.Delay(_timeout.Value, ct);
        }
    }
}
