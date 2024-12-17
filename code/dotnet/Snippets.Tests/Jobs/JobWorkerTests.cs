// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging;
// using Snippets.Jobs;
//
// namespace Snippets.Tests.Jobs;
//
// public class JobWorkerTests
// {
//     private readonly IServiceProvider _services = new ServiceCollection().AddLogging().BuildServiceProvider();
//
//     private ILogger<JobWorker<TestJob>> Logger => _services.GetRequiredService<ILogger<JobWorker<TestJob>>>();
//
//     [Fact]
//     public async Task Start_SimpleProcessing_Ok()
//     {
//         // Arrange
//         var handlerA = new TestJobHandler("a");
//         var handlerB = new TestJobHandler("b");
//         var worker = new JobWorker<TestJob>(new TestJobService(), [handlerA, handlerB], Logger);
//
//         // Arrange
//         var ctSource = new CancellationTokenSource();
//         _ = worker.StartAsync(ctSource.Token);
//         await Task.Delay(TimeSpan.FromSeconds(60), CancellationToken.None);
//         await ctSource.CancelAsync();
//
//         // Assert
//         Assert.True(handlerA.Jobs.Count > 0);
//         Assert.True(handlerB.Jobs.Count > 0);
//     }
//
//     [Fact]
//     public async Task Start_VariousResults_Ok()
//     {
//         // Arrange
//         var service = new TestJobService();
//         var handlerA = new TestJobHandler("a");
//         var handlerB = new TestJobHandler("b", _ => JobResult.Error);
//         var handlerC = new TestJobHandler("c", _ => throw new InvalidOperationException()); // = Unknown
//         var worker = new JobWorker<TestJob>(service, [handlerA, handlerB, handlerC], Logger);
//
//         // Arrange
//         var ctSource = new CancellationTokenSource();
//         _ = worker.StartAsync(ctSource.Token);
//         await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);
//         await ctSource.CancelAsync();
//
//         // Assert
//         foreach (var group in service.Resolved.GroupBy(p => p.Item1.Key).ToList())
//         {
//             // TODO: Fix this test
//             Assert.All(group, i => Assert.Equal(i.Item2, GetExpectedResult(group.Key)));
//         }
//
//         return;
//
//         JobResult GetExpectedResult(string key) =>
//             key switch
//             {
//                 "a" => JobResult.Complete,
//                 "b" => JobResult.Error,
//                 "c" => JobResult.Unknown,
//                 _ => throw new ArgumentException("Unknown"),
//             };
//     }
//
//     private class TestJobService : IJobService<TestJob>
//     {
//         private long _id;
//         public readonly List<(TestJob, JobResult)> Resolved = [];
//
//         public Task<IEnumerable<TestJob>> GetNextAsync(string key, int count, CancellationToken ct = default)
//         {
//             var jobs = Enumerable
//                 .Range(0, count)
//                 .Select(_ => new TestJob(Interlocked.Increment(ref _id), key, DateTime.UtcNow))
//                 .ToList();
//
//             return Task.FromResult<IEnumerable<TestJob>>(jobs);
//         }
//
//         public Task ResolveAsync(TestJob job, JobResult result, Exception? error, CancellationToken ct = default)
//         {
//             Resolved.Add((job, result));
//             return Task.CompletedTask;
//         }
//     }
//
//     private class TestJobHandler(string key, Func<TestJob, JobResult>? resultFn = null) : IJobHandler<TestJob>
//     {
//         private readonly Func<TestJob, JobResult> _resultFn = resultFn ?? (_ => JobResult.Complete);
//         public readonly List<TestJob> Jobs = [];
//
//         public string Key { get; } = key;
//         public TimeSpan? Timeout => null;
//         public int? Concurrency => 10;
//
//         public async Task<JobResult> HandleAsync(TestJob ctx, CancellationToken ct = default)
//         {
//             Jobs.Add(ctx);
//             await Task.Delay(TimeSpan.FromSeconds(1), ct);
//             return _resultFn(ctx);
//         }
//     }
//
//     private record TestJob(long Id, string Key, DateTime Timestamp);
// }
