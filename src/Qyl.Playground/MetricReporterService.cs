namespace Qyl.Playground;

public sealed partial class MetricReporterService(
    AgentMetricListener listener,
    DemoOptions options,
    ILogger<MetricReporterService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SnapshotInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var snapshot = listener.GetSnapshot();
            LogSnapshot(
                snapshot.TurnsStarted,
                snapshot.TurnsCompleted,
                snapshot.ActiveTurns,
                snapshot.ToolCalls,
                snapshot.TotalInputTokens + snapshot.TotalOutputTokens,
                snapshot.AverageDurationMs,
                snapshot.SuccessRate);
        }
    }

    [LoggerMessage(LogLevel.Information,
        "turns={Turns} completed={Completed} active={Active} tools={Tools} tokens={Tokens} avgDurationMs={AvgMs:F1} successRate={SuccessRate:P1}")]
    private partial void LogSnapshot(
        long turns,
        long completed,
        long active,
        long tools,
        long tokens,
        double avgMs,
        double successRate);
}

