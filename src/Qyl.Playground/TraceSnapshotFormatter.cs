using System.Globalization;
using System.Text;

namespace Qyl.Playground;

public static class TraceSnapshotFormatter
{
    public static string Format(AgentTraceSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Trace counters");
        builder.AppendLine("--------------------------------------------------------------------------------");
        builder.AppendLine($"activities started   : {snapshot.ActivitiesStarted.ToString("N0", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"activities stopped   : {snapshot.ActivitiesStopped.ToString("N0", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"tool calls traced    : {snapshot.ToolCallsTraced.ToString("N0", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"chat calls traced    : {snapshot.ChatCallsTraced.ToString("N0", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"failed activities    : {snapshot.FailedActivities.ToString("N0", CultureInfo.InvariantCulture)}");
        builder.AppendLine();

        builder.AppendLine("Recent root traces (most recent first)");
        builder.AppendLine("--------------------------------------------------------------------------------");

        if (snapshot.RecentTraces.Count == 0)
        {
            builder.AppendLine("(none yet)");
            return builder.ToString();
        }

        builder.AppendLine("trace_id (first 16)  scenario  model      outcome     status  duration   tokens(in/out)");
        builder.AppendLine("-------------------- --------- ---------- ----------- ------- ---------- --------------");

        foreach (var trace in snapshot.RecentTraces.TakeLast(10).Reverse())
        {
            var traceShort = trace.TraceId.Length >= 16 ? trace.TraceId[..16] : trace.TraceId;
            builder.Append(traceShort.PadRight(21));
            builder.Append((trace.Scenario ?? "-").PadRight(10));
            builder.Append((trace.Model ?? "-").PadRight(11));
            builder.Append((trace.Outcome ?? "-").PadRight(12));
            builder.Append(trace.Status.PadRight(8));
            builder.Append($"{trace.DurationMs,7:F1}ms  ");
            builder.AppendLine($"{trace.InputTokens}/{trace.OutputTokens}");
        }

        return builder.ToString();
    }
}
