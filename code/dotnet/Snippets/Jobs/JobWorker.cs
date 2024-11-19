using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Snippets.Jobs;

public enum JobResult
{
    Unknown,
    Completed,
    Error,
}

public interface IJobService<TJob>
{
    /// <summary>
    /// Gets N jobs that match the given key.
    /// </summary>
    Task<IEnumerable<TJob>> GetNextAsync(string key, int count, CancellationToken ct = default);

    /// <summary>
    /// Resolves the job based on the result and a possible error.
    /// </summary>
    Task ResolveAsync(TJob job, JobResult result, Exception? error, CancellationToken ct = default);
}

public interface IJobHandler<in TJob>
{
    string Key { get; }

    TimeSpan? Timeout { get; }

    int? Concurrency { get; }

    Task<JobResult> HandleAsync(TJob job, CancellationToken ct = default);
}

/// <summary>
/// Background worker for processing jobs.
/// </summary>
/// <param name="service">Job management service</param>
/// <param name="handlers">Job handlers</param>
/// <param name="logger">Logger</param>
/// <typeparam name="TJob">Job type</typeparam>
public class JobWorker<TJob>(IJobService<TJob> service, IEnumerable<IJobHandler<TJob>> handlers, ILogger logger)
    : BackgroundService
{
    private readonly IJobService<TJob> _service = service;
    private readonly ImmutableList<IJobHandler<TJob>> _handlers = handlers.ToImmutableList();
    private readonly ILogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting...");
        var tasks = _handlers.Select(handler => Task.Run(() => ProcessUntilStoppedAsync(handler, ct), ct));
        await Task.WhenAll(tasks);
        _logger.LogInformation("Completed");
    }

    private async Task ProcessUntilStoppedAsync(IJobHandler<TJob> handler, CancellationToken ct)
    {
        _logger.LogInformation("[Job: {K}] Starting...", handler.Key);
        while (!ct.IsCancellationRequested)
        {
            var didProcess = await ProcessAsync(handler, ct);
            if (!didProcess)
            {
                await Task.Delay(handler.Timeout ?? TimeSpan.FromSeconds(5), ct);
            }
        }

        _logger.LogInformation("[Job: {K}] Completed", handler.Key);
    }

    private async Task<bool> ProcessAsync(IJobHandler<TJob> handler, CancellationToken ct)
    {
        var jobs = (await _service.GetNextAsync(handler.Key, handler.Concurrency ?? 1, ct)).ToList();
        if (jobs.Count == 0)
        {
            return false;
        }

        var tasks = jobs.Select(job => Task.Run(() => HandleAndResolveAsync(handler, job, ct), ct)).ToList();
        await Task.WhenAll(tasks);
        return true;
    }

    private async Task HandleAndResolveAsync(IJobHandler<TJob> handler, TJob job, CancellationToken ct)
    {
        try
        {
            var result = await handler.HandleAsync(job, ct);
            await _service.ResolveAsync(job, result, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Job: {K}] Unknown handler error", handler.Key);
            await _service.ResolveAsync(job, JobResult.Unknown, ex, ct);
        }
    }
}
