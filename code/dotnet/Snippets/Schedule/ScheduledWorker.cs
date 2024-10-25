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
        var hasNextJob = await WaitForNextJobAsync(ct);
        if (!hasNextJob)
        {
            Logger?.LogWarning("Schedule has no upcoming jobs");
            return;
        }

        while (hasNextJob && !ct.IsCancellationRequested)
        {
            try
            {
                await InvokeAsync(ct);
                hasNextJob = await WaitForNextJobAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // NOP
            }
        }

        Logger?.LogInformation("Completed");
    }

    protected abstract Task InvokeAsync(CancellationToken ct);

    private async Task<bool> WaitForNextJobAsync(CancellationToken ct)
    {
        var nextTs = GetNextJobTimestamp();
        if (!nextTs.HasValue)
        {
            return false;
        }

        Logger?.LogInformation("Next job timestamp: {T}>", nextTs.Value.ToString("O"));
        await Task.Delay(NowProvider() - nextTs.Value, ct);
        return true;
    }

    private DateTime? GetNextJobTimestamp() => Schedule.GetNextOccurrence(NowProvider());
}
