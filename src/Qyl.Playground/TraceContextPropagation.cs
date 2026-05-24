using System.Diagnostics;

namespace Qyl.Playground;

// HTTP ingress gets free W3C TraceContext propagation via the
// OpenTelemetry.Instrumentation.AspNetCore package. Anything else — message
// queues, gRPC streaming, custom RPC, MCP transports, scheduled jobs,
// background work spawning from a parent — needs explicit propagation.
//
// .NET's DistributedContextPropagator is the runtime-native abstraction:
// W3C TraceContext by default, swappable per-process via
// DistributedContextPropagator.Current = ...
//
// Pattern:
//   PRODUCER (before sending):
//     var headers = new Dictionary<string, string>();
//     TraceContextPropagation.Inject(Activity.Current, headers);
//     queue.Send(payload, headers);
//
//   CONSUMER (after receiving):
//     var parent = TraceContextPropagation.Extract(message.Headers);
//     using var activity = AgentActivitySource.Instance.StartActivity(
//         "process_job", ActivityKind.Consumer, parent);
public static class TraceContextPropagation
{
    public static void Inject(Activity? activity, IDictionary<string, string> carrier)
    {
        DistributedContextPropagator.Current.Inject(
            activity,
            carrier,
            static (object? c, string name, string value) =>
            {
                if (c is IDictionary<string, string> dict)
                {
                    dict[name] = value;
                }
            });
    }

    public static ActivityContext Extract(IReadOnlyDictionary<string, string> carrier)
    {
        DistributedContextPropagator.Current.ExtractTraceIdAndState(
            carrier,
            static (object? c, string name, out string? value, out IEnumerable<string>? values) =>
            {
                values = null;
                value = null;
                if (c is IReadOnlyDictionary<string, string> dict &&
                    dict.TryGetValue(name, out var v))
                {
                    value = v;
                }
            },
            out var traceParent,
            out var traceState);

        if (string.IsNullOrEmpty(traceParent))
        {
            return default;
        }

        return ActivityContext.TryParse(traceParent, traceState, out var ctx)
            ? ctx
            : default;
    }

}
