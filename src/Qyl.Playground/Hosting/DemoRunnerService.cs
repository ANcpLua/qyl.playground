namespace Qyl.Playground;

public sealed partial class DemoRunnerService(
    AgentRunService runs,
    AgentMetricListener listener,
    AgentActivityListener traceListener,
    DemoOptions options,
    IHostApplicationLifetime lifetime,
    ILogger<DemoRunnerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarting(options.Duration.TotalSeconds, options.Parallelism);

        using var timeout = new CancellationTokenSource(options.Duration);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            timeout.Token);

        try
        {
            var workers = Enumerable.Range(0, options.Parallelism)
                .Select(_ => RunWorkerAsync(linked.Token))
                .ToArray();

            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // Expected when the bounded demo duration elapses.
        }
        finally
        {
            // Spectre's Live() owns the terminal when the dashboard is active —
            // writing snapshots here would corrupt the rendered UI.
            if (!options.EnableLiveDashboard)
            {
                Console.WriteLine();
                Console.WriteLine("Final in-process metric snapshot");
                Console.WriteLine(MetricSnapshotFormatter.Format(listener.GetSnapshot()));
                Console.WriteLine();
                Console.WriteLine("Final in-process trace snapshot");
                Console.WriteLine(TraceSnapshotFormatter.Format(traceListener.GetSnapshot()));
            }
            lifetime.StopApplication();
        }
    }

    private async Task RunWorkerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await runs.RunAsync(
                new AgentRunRequest(
                    AgentScenarioParser.Parse(null),
                    AgentModelParser.Parse(null)),
                cancellationToken);
        }
    }

    [LoggerMessage(LogLevel.Information,
        "Starting bounded demo for {DurationSeconds}s with parallelism {Parallelism}.")]
    private partial void LogStarting(double durationSeconds, int parallelism);
}

