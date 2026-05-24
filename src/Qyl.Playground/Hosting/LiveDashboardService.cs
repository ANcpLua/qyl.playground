using System.Globalization;
using Spectre.Console;

namespace Qyl.Playground;

// Live-updating console dashboard built with Spectre.Console — the modern
// equivalent of the Andrew-Lock-Part-4 example. Only registered when stdout
// is a TTY and --demo was passed; piped / headless runs use
// MetricReporterService (ILogger structured output) instead.
public sealed class LiveDashboardService(
    AgentMetricListener metrics,
    AgentActivityListener traces,
    DemoOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var metricsTable = BuildMetricsTable();
        var tracesTable = BuildTracesTable();

        var layout = new Rows(
            new Markup("[bold cyan]Qyl.Playground[/] — live observability dashboard").Centered(),
            new Markup("[dim]press ctrl+c to stop[/]").Centered(),
            new Rule().RuleStyle(Style.Parse("grey50")),
            metricsTable,
            tracesTable);

        try
        {
            await AnsiConsole.Live(layout).StartAsync(async ctx =>
            {
                ctx.Refresh();

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(options.SnapshotInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    UpdateMetricsTable(metricsTable, metrics.GetSnapshot());
                    UpdateTracesTable(tracesTable, traces.GetSnapshot());
                    ctx.Refresh();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private static Table BuildMetricsTable()
    {
        var table = new Table()
            .Title("[bold cyan]Metrics[/]  [dim](System.Diagnostics.Metrics + MeterListener)[/]")
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Type")
            .AddColumn(new TableColumn("Value").RightAligned());

        table.AddRow("agent.turn.started", "Counter", "0");
        table.AddRow("agent.turn.completed", "Counter", "0");
        table.AddRow("agent.tool.call.count", "Counter", "0");
        table.AddRow("agent.turn.active", "UpDownCounter", "0");
        table.AddRow("agent.queue.depth", "Gauge", "0");
        table.AddRow("agent.token.total (in / out)", "ObservableCounter", "0 / 0");
        table.AddRow("agent.turn.active_by_scenario (r / c / rv)", "ObservableUpDownCounter", "0 / 0 / 0");
        table.AddRow("agent.turn.success_rate", "ObservableGauge", "0.0%");
        table.AddRow("agent.turn.duration (avg / count)", "Histogram", "0.0ms / 0");
        table.AddRow("agent.turn.tokens (avg)", "Histogram", "0");
        return table;
    }

    private static Table BuildTracesTable()
    {
        var table = new Table()
            .Title("[bold magenta]Traces[/]  [dim](System.Diagnostics.Activity + ActivityListener, GenAI semconv)[/]")
            .Border(TableBorder.Rounded)
            .AddColumn("Counter")
            .AddColumn(new TableColumn("Value").RightAligned());

        table.AddRow("activities started", "0");
        table.AddRow("activities stopped", "0");
        table.AddRow("chat calls traced", "0");
        table.AddRow("tool calls traced", "0");
        table.AddRow("failed activities", "0");
        return table;
    }

    private static void UpdateMetricsTable(Table table, AgentMetricSnapshot snap)
    {
        table.UpdateCell(0, 2, snap.TurnsStarted.ToString("N0", CultureInfo.InvariantCulture));
        table.UpdateCell(1, 2, snap.TurnsCompleted.ToString("N0", CultureInfo.InvariantCulture));
        table.UpdateCell(2, 2, snap.ToolCalls.ToString("N0", CultureInfo.InvariantCulture));
        table.UpdateCell(3, 2, snap.ActiveTurns.ToString("N0", CultureInfo.InvariantCulture));
        table.UpdateCell(4, 2, snap.LastQueueDepth.ToString("N0", CultureInfo.InvariantCulture));
        table.UpdateCell(5, 2, $"{snap.TotalInputTokens:N0} / {snap.TotalOutputTokens:N0}");
        table.UpdateCell(6, 2, $"{snap.ActiveResearchTurns} / {snap.ActiveCodingTurns} / {snap.ActiveReviewTurns}");
        table.UpdateCell(7, 2, snap.SuccessRate.ToString("P1", CultureInfo.InvariantCulture));
        table.UpdateCell(8, 2, $"{snap.AverageDurationMs:F1}ms / {snap.DurationSamples:N0}");
        table.UpdateCell(9, 2, snap.AverageTokensPerTurn.ToString("F0", CultureInfo.InvariantCulture));
    }

    private static void UpdateTracesTable(Table table, AgentTraceSnapshot snap)
    {
        table.UpdateCell(0, 1, snap.ActivitiesStarted.ToString("N0", CultureInfo.InvariantCulture));
        table.UpdateCell(1, 1, snap.ActivitiesStopped.ToString("N0", CultureInfo.InvariantCulture));
        table.UpdateCell(2, 1, snap.ChatCallsTraced.ToString("N0", CultureInfo.InvariantCulture));
        table.UpdateCell(3, 1, snap.ToolCallsTraced.ToString("N0", CultureInfo.InvariantCulture));
        table.UpdateCell(4, 1, snap.FailedActivities.ToString("N0", CultureInfo.InvariantCulture));
    }
}
