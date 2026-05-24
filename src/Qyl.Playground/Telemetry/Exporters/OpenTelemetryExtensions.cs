using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Qyl.Playground;

// Environment-aware OpenTelemetry wiring. The exporter choice is automatic:
//   - OTLP    if OTEL_EXPORTER_OTLP_ENDPOINT is set (collector / agent in front).
//   - Console if Development AND OTLP not configured AND the live dashboard
//             is not active (otherwise console writes would corrupt the UI).
//   - Neither in Production with no OTLP endpoint — signals still collect
//             via the raw MeterListener / ActivityListener channels.
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
                 .SetSampler(new AlwaysOnSampler());
                if (useConsole) t.AddConsoleExporter();
                if (useOtlp) t.AddOtlpExporter();
            });

        return services;
    }
}
