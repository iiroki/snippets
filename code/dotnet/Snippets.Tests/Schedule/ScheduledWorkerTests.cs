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
        Assert.Equal(0, worker.Count);
    }

    // TODO: More tests
}

public class TestScheduledWorker(string? schedule, Func<DateTime>? nowProvider = null)
    : ScheduledWorker(schedule, nowProvider)
{
    public int Count { get; private set; }

    protected override Task InvokeAsync(CancellationToken ct)
    {
        ++Count;
        return Task.CompletedTask;
    }
}
