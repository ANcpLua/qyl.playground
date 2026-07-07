using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Qyl.OpenTelemetry.AutoInstrumentation;

namespace Qyl.Playground;

// Environment-aware OpenTelemetry wiring. The exporter choice is automatic:
//   - OTLP    if OTEL_EXPORTER_OTLP_ENDPOINT is set (collector / agent in front).
//   - Console if Development AND OTLP not configured AND the live dashboard
//             is not active (otherwise console writes would corrupt the UI).
//   - Neither in Production with no OTLP endpoint — the in-process dashboard
//             and /metrics/snapshot still read through the SDK in-memory reader.
//
// Override at runtime with:
//   OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317  dotnet run ...
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddPlaygroundOpenTelemetry(
        this IServiceCollection services,
        IHostEnvironment environment,
        DemoOptions options)
    {
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var useOtlp = !string.IsNullOrEmpty(otlpEndpoint);
        var useConsole = environment.IsDevelopment() && !useOtlp && !options.EnableLiveDashboard;

        // Non-HTTP context propagation flows through the stack's own W3C propagator
        // (TraceContext + Baggage). HTTP ingress/egress is propagated automatically by
        // the ASP.NET Core and HttpClient instrumentation; this covers queues, gRPC,
        // scheduled work, and other custom transports.
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(
            new TextMapPropagator[] { new TraceContextPropagator(), new BaggagePropagator() }));

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: "Qyl.Playground",
                serviceVersion: AgentActivitySource.Version))
            .WithMetrics(m =>
            {
                m.AddMeter(AgentWorkflowMetrics.MeterName);
                if (useConsole) m.AddConsoleExporter();
                if (useOtlp) m.AddOtlpExporter();
            })
            .WithTracing(t =>
            {
                t.AddSource(AgentActivitySource.Name)
                 .AddSource(QylActivitySource.Name) // zero-code spans from the Qyl auto-instrumentation stack
                 .SetSampler(new AlwaysOnSampler());
                if (useConsole) t.AddConsoleExporter();
                if (useOtlp) t.AddOtlpExporter();
            });

        return services;
    }
}
