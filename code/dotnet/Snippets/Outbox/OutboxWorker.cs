using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Snippets.Outbox;

/// <summary>
/// Background worker for processing outbox jobs.
/// </summary>
internal class OutboxWorker(IOutboxService service, IEnumerable<IOutboxHandler> handlers, ILogger logger)
    : BackgroundService
{
    private readonly IOutboxService _service = service;
    private readonly ImmutableList<IOutboxHandler> _handlers = handlers.ToImmutableList();
    private readonly ILogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting...");
        var tasks = _handlers.Select(handler => Task.Run(() => ProcessUntilStopped(handler, ct), ct));
        await Task.WhenAll(tasks);
        _logger.LogInformation("Completed");
    }

    private async Task ProcessUntilStopped(IOutboxHandler handler, CancellationToken ct)
    {
        _logger.LogInformation("[Job: {K}] Starting...", handler.Key);
        while (!ct.IsCancellationRequested)
        {
            var didProcess = await Process(handler, ct);
            if (!didProcess)
            {
                await Task.Delay(handler.Timeout ?? TimeSpan.FromSeconds(5), ct);
            }
        }

        _logger.LogInformation("[Job: {K}] Completed", handler.Key);
    }

    private async Task<bool> Process(IOutboxHandler handler, CancellationToken ct)
    {
        // TODO: Independent concurrency instead of batches
        // TODO: Handle errors
        var @params = new OutboxGetParams
        {
            Key = handler.Key,
            Count = handler.Concurrency ?? 1,
            Timeout = handler.Timeout,
            ShouldPeek = handler.ShouldPeek,
        };

        var jobs = await _service.GetNext(@params, ct);
        if (jobs.Count == 0)
        {
            return false;
        }

        var tasks = jobs.Select(job => Task.Run(() => HandleAndResolve(handler, job, ct), ct)).ToList();
        await Task.WhenAll(tasks);
        return true;
    }

    private async Task HandleAndResolve(IOutboxHandler handler, IOutboxJobWrapper job, CancellationToken ct)
    {
        var jobCtx = new OutboxContext(job, ActivateFn);

        // Check if the max attempt count has already been reached
        if (handler.MaxAttemptCount.HasValue && job.AttemptCount > handler.MaxAttemptCount.Value)
        {
            var result = new OutboxResult { Action = OutboxAction.Error };
            var @params = new OutboxResolveParams { Result = result };
            await _service.Resolve(job.Id, @params, ct);
            return;
        }

        // Process the job
        try
        {
            var result = await handler.Handle(jobCtx, ct);
            var shouldCache = result.Action == OutboxAction.Update;
            // TODO: Cache an updated job

            var @params = new OutboxResolveParams { Result = result, Receipt = jobCtx.Properties.Receipt };

            await _service.Resolve(job.Id, @params, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Job: {K}] Unknown handler exception", handler.Key);
        }

        return;

        async Task<IOutboxJob> ActivateFn() =>
            await _service.Activate(job.Id, new OutboxActiveParams { Timeout = handler.Timeout }, ct);
    }
}
