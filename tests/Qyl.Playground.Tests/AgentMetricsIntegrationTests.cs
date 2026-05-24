using Qyl.Playground;
using Microsoft.Extensions.DependencyInjection;

namespace Qyl.Playground.Tests;

public sealed class AgentMetricsIntegrationTests
{
    [Fact]
    public async Task AgentRunsAreVisibleToInProcessMetricListener()
    {
        await using var provider = CreateProvider();
        var runs = provider.GetRequiredService<AgentRunService>();
        var listener = provider.GetRequiredService<AgentMetricListener>();
        var request = new AgentRunRequest(AgentScenario.Coding, AgentModel.Frontier);

        for (var i = 0; i < 8; i++)
        {
            await runs.RunAsync(request, CancellationToken.None);
        }

        var snapshot = listener.GetSnapshot();

        Assert.Equal(8, snapshot.TurnsStarted);
        Assert.Equal(8, snapshot.TurnsCompleted);
        Assert.Equal(0, snapshot.ActiveTurns);
        Assert.Equal(0, snapshot.LastQueueDepth);
        Assert.True(snapshot.ToolCalls >= 8);
        Assert.True(snapshot.TotalInputTokens > 0);
        Assert.True(snapshot.TotalOutputTokens > 0);
        Assert.Equal(snapshot.TotalInputTokens + snapshot.TotalOutputTokens, snapshot.TokenUsageDeltas);
        Assert.Equal(snapshot.TurnsCompleted, snapshot.DurationSamples);
        Assert.Equal(snapshot.TurnsCompleted, snapshot.TokenHistogramSamples);
        Assert.InRange(snapshot.SuccessRate, 0, 1);
    }

    [Fact]
    public void MetricDefinitionsCoverEveryImplementedInstrumentFamily()
    {
        using var provider = CreateProvider();
        var metrics = provider.GetRequiredService<AgentWorkflowMetrics>();

        Assert.Contains(metrics.Definitions, definition => definition.Type == "Counter<long>");
        Assert.Contains(metrics.Definitions, definition => definition.Type == "UpDownCounter<long>");
        Assert.Contains(metrics.Definitions, definition => definition.Type == "Gauge<long>");
        Assert.Contains(metrics.Definitions, definition => definition.Type == "Histogram<double>");
        Assert.Contains(metrics.Definitions, definition => definition.Type == "Histogram<long>");
        Assert.Contains(metrics.Definitions, definition => definition.Type == "ObservableCounter<long>");
        Assert.Contains(metrics.Definitions, definition => definition.Type == "ObservableUpDownCounter<long>");
        Assert.Contains(metrics.Definitions, definition => definition.Type == "ObservableGauge<double>");
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton<AgentWorkflowMetrics>();
        services.AddSingleton<AgentMetricListener>();
        services.AddSingleton<AgentRunService>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
