using System.Diagnostics;
using Qyl.Playground;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var options = DemoOptions.FromArgs(args);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMetrics();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<AgentWorkflowMetrics>();
builder.Services.AddSingleton<AgentRunService>();
builder.Services.AddSingleton<AgentMetricListener>();
builder.Services.AddSingleton<AgentActivityListener>();

// OpenTelemetry SDK: parallel pipeline that exports both metrics and traces,
// applying GenAI semantic conventions to the AI workload spans.
// AlwaysOnSampler is fine for a demo; production would use ParentBasedSampler
// or TraceIdRatioBasedSampler to cap volume.
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "Qyl.Playground",
        serviceVersion: AgentActivitySource.Version))
    .WithMetrics(m => m
        .AddMeter(AgentWorkflowMetrics.MeterName)
        .AddConsoleExporter())
    .WithTracing(t => t
        .AddSource(AgentActivitySource.Name)
        .SetSampler(new AlwaysOnSampler())
        .AddConsoleExporter());

if (options.RunBoundedDemo)
{
    builder.Services.AddHostedService<DemoRunnerService>();
}

if (options.EnablePeriodicReporter)
{
    builder.Services.AddHostedService<MetricReporterService>();
}

var app = builder.Build();

// Eagerly resolve both raw in-process listeners so they start receiving
// events before any activity or measurement is produced.
_ = app.Services.GetRequiredService<AgentMetricListener>();
_ = app.Services.GetRequiredService<AgentActivityListener>();

app.MapGet("/", () => Results.Ok(new
{
    app = "Qyl.Playground",
    purpose = "System.Diagnostics.Metrics + OpenTelemetry trace sample for agent-style workloads",
    endpoints = new[] { "/agent/run", "/metrics/snapshot", "/metrics/definitions", "/trace/snapshot" },
    demo = "dotnet run --project src/Qyl.Playground -- --demo --duration 6 --parallelism 4"
}));

app.MapGet("/agent/run", async (
    AgentRunService runs,
    string? scenario,
    string? model,
    CancellationToken cancellationToken) =>
{
    var request = AgentRunRequest.FromQuery(scenario, model);
    return Results.Ok(await runs.RunAsync(request, cancellationToken));
});

// Demonstrates the Tier 4 non-HTTP propagation pattern. Pass a `traceparent`
// (and optional `tracestate`) value via query string. The endpoint extracts
// them through DistributedContextPropagator and roots the agent activity
// under the supplied context, exactly as a queue consumer or gRPC server
// would do after receiving a message with the same headers.
app.MapGet("/agent/run-with-context", async (
    AgentRunService runs,
    string traceparent,
    string? tracestate,
    string? scenario,
    string? model,
    CancellationToken cancellationToken) =>
{
    var carrier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["traceparent"] = traceparent
    };
    if (!string.IsNullOrEmpty(tracestate))
    {
        carrier["tracestate"] = tracestate;
    }

    var parentContext = TraceContextPropagation.Extract(carrier);

    using var consumerActivity = AgentActivitySource.Instance.StartActivity(
        "agent.consume",
        ActivityKind.Consumer,
        parentContext);

    var request = AgentRunRequest.FromQuery(scenario, model);
    var result = await runs.RunAsync(request, cancellationToken);
    return Results.Ok(new
    {
        result,
        extractedParent = parentContext != default
            ? new { traceId = parentContext.TraceId.ToString(), spanId = parentContext.SpanId.ToString() }
            : null
    });
});

// Producer side of the same pattern: returns the headers a downstream
// non-HTTP transport would need to attach so the receiver could rejoin
// the current trace. We create an activity here so Activity.Current
// is populated; in a real app this would already be the inbound HTTP
// server span emitted by OpenTelemetry.Instrumentation.AspNetCore.
app.MapGet("/agent/propagation-headers", () =>
{
    using var demoActivity = AgentActivitySource.Instance.StartActivity(
        "propagation.demo",
        ActivityKind.Producer);
    BaggageLimits.TryAddBaggage(demoActivity, "agent.session.id", Guid.NewGuid().ToString("N"));

    var headers = new Dictionary<string, string>();
    TraceContextPropagation.Inject(Activity.Current, headers);
    return Results.Ok(new
    {
        attach_to_outbound_message = headers,
        active_trace = Activity.Current is { } a
            ? new { traceId = a.TraceId.ToString(), spanId = a.SpanId.ToString() }
            : null
    });
});

app.MapGet("/metrics/snapshot", (AgentMetricListener listener) =>
    Results.Ok(listener.GetSnapshot()));

app.MapGet("/metrics/definitions", (AgentWorkflowMetrics metrics) =>
    Results.Ok(metrics.Definitions));

app.MapGet("/trace/snapshot", (AgentActivityListener listener) =>
    Results.Ok(listener.GetSnapshot()));

app.Run();
