using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Qyl.Playground;

public sealed class AgentMetricCollector : IDisposable
{
    private readonly List<MetricSnapshot> _exported = [];
    private readonly MeterProvider _provider;
    private readonly Lock _gate = new();

    public AgentMetricCollector()
    {
        _provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(AgentWorkflowMetrics.MeterName)
            .AddInMemoryExporter(_exported)
            .Build();
    }

    public AgentMetricSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            _exported.Clear();
            _provider.ForceFlush();
            return Project(_exported);
        }
    }

    public void Dispose() => _provider.Dispose();

    private static AgentMetricSnapshot Project(List<MetricSnapshot> metrics)
    {
        long turnsStarted = 0, turnsCompleted = 0, toolCalls = 0, tokenUsage = 0;
        long activeTurns = 0, queueDepth = 0, totalInput = 0, totalOutput = 0;
        long research = 0, coding = 0, review = 0, durationCount = 0, tokenCount = 0;
        double successRate = 0, durationSum = 0, tokenSum = 0;

        foreach (var metric in metrics)
        {
            switch (metric.Name)
            {
                case "agent.turn.started":
                    turnsStarted = SumLong(metric);
                    break;
                case "agent.turn.completed":
                    turnsCompleted = SumLong(metric);
                    break;
                case "agent.tool.call.count":
                    toolCalls = SumLong(metric);
                    break;
                case "agent.token.usage":
                    tokenUsage = SumLong(metric);
                    break;
                case "agent.turn.active":
                    activeTurns = SumLong(metric);
                    break;
                case "agent.queue.depth":
                    foreach (var point in metric.MetricPoints)
                    {
                        queueDepth += point.GetGaugeLastValueLong();
                    }

                    break;
                case "agent.turn.duration":
                    foreach (var point in metric.MetricPoints)
                    {
                        durationSum += point.GetHistogramSum();
                        durationCount += point.GetHistogramCount();
                    }

                    break;
                case "agent.turn.tokens":
                    foreach (var point in metric.MetricPoints)
                    {
                        tokenSum += point.GetHistogramSum();
                        tokenCount += point.GetHistogramCount();
                    }

                    break;
                case "agent.token.total":
                    foreach (var point in metric.MetricPoints)
                    {
                        switch (FindTag(point, "agent.token.kind"))
                        {
                            case "input":
                                totalInput = point.GetSumLong();
                                break;
                            case "output":
                                totalOutput = point.GetSumLong();
                                break;
                        }
                    }

                    break;
                case "agent.turn.active_by_scenario":
                    foreach (var point in metric.MetricPoints)
                    {
                        switch (FindTag(point, "agent.scenario"))
                        {
                            case "research":
                                research = point.GetSumLong();
                                break;
                            case "coding":
                                coding = point.GetSumLong();
                                break;
                            case "review":
                                review = point.GetSumLong();
                                break;
                        }
                    }

                    break;
                case "agent.turn.success_rate":
                    foreach (var point in metric.MetricPoints)
                    {
                        successRate = point.GetGaugeLastValueDouble();
                    }

                    break;
            }
        }

        return new AgentMetricSnapshot(
            TurnsStarted: turnsStarted,
            TurnsCompleted: turnsCompleted,
            ToolCalls: toolCalls,
            TokenUsageDeltas: tokenUsage,
            ActiveTurns: activeTurns,
            LastQueueDepth: queueDepth,
            TotalInputTokens: totalInput,
            TotalOutputTokens: totalOutput,
            ActiveResearchTurns: research,
            ActiveCodingTurns: coding,
            ActiveReviewTurns: review,
            SuccessRate: successRate,
            AverageDurationMs: durationCount == 0 ? 0 : durationSum / durationCount * 1_000,
            DurationSamples: durationCount,
            AverageTokensPerTurn: tokenCount == 0 ? 0 : tokenSum / tokenCount,
            TokenHistogramSamples: tokenCount);
    }

    private static long SumLong(MetricSnapshot metric)
    {
        long total = 0;
        foreach (var point in metric.MetricPoints)
        {
            total += point.GetSumLong();
        }

        return total;
    }

    private static string? FindTag(in MetricPoint point, string key)
    {
        foreach (var tag in point.Tags)
        {
            if (tag.Key == key)
            {
                return tag.Value?.ToString();
            }
        }

        return null;
    }
}
