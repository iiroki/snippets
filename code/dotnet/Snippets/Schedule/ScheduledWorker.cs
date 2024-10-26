using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Snippets.Schedule;

/// <summary>
/// Background worker for invoking a cron scheduled method.
/// </summary>
/// <param name="schedule">
/// Cron schedule<br />
/// (<see href="https://github.com/HangfireIO/Cronos?tab=readme-ov-file#cron-format">Cron format</see>)
/// </param>
/// <param name="nowProvider">Current (now) timestamp provider<br />(null = default <c>DateTime.UtcNow</c>)</param>
/// <param name="logger">Logger (null = disabled)</param>
public abstract class ScheduledWorker(string? schedule, Func<DateTime>? nowProvider, ILogger? logger = null)
    : BackgroundService
{
    private readonly string? _scheduleRaw = schedule;
    protected readonly Func<DateTime> NowProvider = nowProvider ?? (() => DateTime.UtcNow);
    protected readonly ILogger? Logger = logger;

    private CronExpression? _schedule;
    public CronExpression Schedule => _schedule ?? throw new InvalidOperationException("Schedule not initialized");

    public bool IsDisabled => string.IsNullOrWhiteSpace(_scheduleRaw);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.Register(() => Logger?.LogInformation("Stopping..."));

        Logger?.LogInformation("Starting...");
        if (IsDisabled)
        {
            Logger?.LogInformation("Disabled - Schedule not defined");
            return;
        }

        if (!CronExpression.TryParse(_scheduleRaw, out _schedule))
        {
            Logger?.LogError("Unable to start - Invalid schedule: {S}", _scheduleRaw);
            return;
        }

        Logger?.LogInformation("Schedule: {S}", Schedule);

        DateTime? nextJobTs = null;
        while (!ct.IsCancellationRequested)
        {
            nextJobTs = GetNextJobTimestamp(nextJobTs);
            if (!nextJobTs.HasValue)
            {
                Logger?.LogInformation("Schedule has no upcoming jobs: {S}", Schedule);
                break;
            }

            Logger?.LogDebug("Next job timestamp: {T}Z", nextJobTs.Value.ToString("O"));
            try
            {
                await WaitUntilAsync(nextJobTs.Value, ct);

                // Invoke the job on another thread so it won't block this loop
                Logger?.LogDebug("Invoking...");
                var invocationTs = nextJobTs.Value;
                _ = Task.Run(() => InvokeAsync(invocationTs, ct), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // NOP
            }
        }

        Logger?.LogInformation("Completed");
    }

    protected abstract Task InvokeAsync(DateTime ts, CancellationToken ct);

    private async Task WaitUntilAsync(DateTime ts, CancellationToken ct)
    {
        var now = NowProvider();
        var wait = ts - now;
        if (wait.Ticks > 0)
        {
            await Task.Delay(wait, ct);
        }
    }

    private DateTime? GetNextJobTimestamp(DateTime? fromTs) => Schedule.GetNextOccurrence(fromTs ?? NowProvider());
}
