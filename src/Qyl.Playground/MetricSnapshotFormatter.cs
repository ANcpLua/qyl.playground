using System.Globalization;
using System.Text;

namespace Qyl.Playground;

public static class MetricSnapshotFormatter
{
    public static string Format(AgentMetricSnapshot snapshot)
    {
        var rows = new (string Metric, string Type, string Value)[]
        {
            ("agent.turn.started", "Counter", snapshot.TurnsStarted.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.turn.completed", "Counter", snapshot.TurnsCompleted.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.tool.call.count", "Counter", snapshot.ToolCalls.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.token.usage", "Counter", snapshot.TokenUsageDeltas.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.turn.active", "UpDownCounter", snapshot.ActiveTurns.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.queue.depth", "Gauge", snapshot.LastQueueDepth.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.token.total (input)", "ObservableCounter", snapshot.TotalInputTokens.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.token.total (output)", "ObservableCounter", snapshot.TotalOutputTokens.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.turn.active_by_scenario (research)", "ObservableUpDownCounter", snapshot.ActiveResearchTurns.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.turn.active_by_scenario (coding)", "ObservableUpDownCounter", snapshot.ActiveCodingTurns.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.turn.active_by_scenario (review)", "ObservableUpDownCounter", snapshot.ActiveReviewTurns.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.turn.success_rate", "ObservableGauge", snapshot.SuccessRate.ToString("P1", CultureInfo.InvariantCulture)),
            ("agent.turn.duration", "Histogram", $"{snapshot.AverageDurationMs:F1} ms avg"),
            ("agent.turn.duration (count)", "Histogram", snapshot.DurationSamples.ToString("N0", CultureInfo.InvariantCulture)),
            ("agent.turn.tokens", "Histogram", $"{snapshot.AverageTokensPerTurn:F0} tokens avg")
        };

        var builder = new StringBuilder();
        builder.AppendLine("Metric                                      Type                       Value");
        builder.AppendLine("--------------------------------------------------------------------------------");

        foreach (var row in rows)
        {
            builder.Append(row.Metric.PadRight(43));
            builder.Append(row.Type.PadRight(27));
            builder.AppendLine(row.Value);
        }

        return builder.ToString();
    }
}

