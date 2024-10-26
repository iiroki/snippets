namespace Snippets.Threading;

public class AsyncLock
{
    /// <summary>
    /// A sempaphore lock that only allows one request to be processed at the same time.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task RunAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Do stuff
        }
        finally
        {
            _lock.Release();
        }
    }
}
