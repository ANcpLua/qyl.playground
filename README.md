# Qyl.Playground

A runnable .NET 10 sample that exercises the full 2026 observability stack for an agent-style workload:

- `System.Diagnostics.Metrics` with every standard and observable instrument family.
- `System.Diagnostics.Activity` traces wrapped in the OpenTelemetry GenAI semantic conventions (`gen_ai.*`).
- An in-process `MeterListener` consumer and an in-process `ActivityListener` consumer, side-by-side.
- The OpenTelemetry SDK with parallel metrics + tracing pipelines and console exporters.
- Tier 4 production concerns most demos skip: `BaggageLimits` (W3C-spec caps) and `TraceContextPropagation` (W3C inject/extract for non-HTTP transports).

## What It Demonstrates

The sample uses the raw metrics APIs directly. That keeps call sites readable, allows units and descriptions on every instrument, and pairs strongly-typed helper methods with bounded tag values (enums + extension methods, no `enum.ToString()`).

Instrument families:

- `Counter<long>` for started turns, completed turns, tool calls, and token deltas.
- `UpDownCounter<long>` for currently active turns.
- `Gauge<long>` for the latest queue depth.
- `Histogram<double>` and `Histogram<long>` for turn duration and per-turn tokens (with `InstrumentAdvice<T>` bucket boundaries).
- `ObservableCounter<long>` for running token totals.
- `ObservableUpDownCounter<long>` for active turns by scenario.
- `ObservableGauge<double>` for success rate.

Trace span hierarchy emitted per agent run, applying the OpenTelemetry GenAI semantic conventions:

```
invoke_agent {scenario}       ActivityKind.Internal  (root)
├── chat {model}              ActivityKind.Client
│   └── event: gen_ai.choice
└── execute_tool {tool}       ActivityKind.Internal  (1..4 children)
```

Root spans carry `gen_ai.system`, `gen_ai.operation.name`, `gen_ai.agent.name`, `gen_ai.request.model`, `gen_ai.response.finish_reasons`, cumulative `gen_ai.usage.input_tokens` / `output_tokens`, and `ActivityStatusCode` set from the agent outcome.

## Run It

```bash
dotnet run --project src/Qyl.Playground -- --demo --duration 6 --parallelism 4
```

The bounded demo prints both the in-process metric snapshot and the in-process trace snapshot at the end.

Use the HTTP endpoints when running without `--demo`:

```bash
dotnet run --project src/Qyl.Playground
curl 'http://localhost:5000/agent/run?scenario=coding&model=frontier'
curl 'http://localhost:5000/metrics/snapshot'
curl 'http://localhost:5000/metrics/definitions'
curl 'http://localhost:5000/trace/snapshot'
curl 'http://localhost:5000/agent/propagation-headers'
curl 'http://localhost:5000/agent/run-with-context?traceparent=00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01&scenario=coding'
```

## Verify It

```bash
dotnet test Qyl.Playground.slnx --no-restore
```

The tests exercise the producer and the in-process metric listener together: completed agent turns appear in the snapshot, observable totals are polled correctly, active counts return to zero, and the implemented instrument definitions cover the full sample surface.

## What It Is Good For

A local observability lab for .NET agent workloads. Useful for learning metric shape, testing listener aggregation, seeing the difference between standard and observable instruments, watching GenAI-convention spans flow through the OpenTelemetry SDK, and proving that the raw `MeterListener` / `ActivityListener` channels can coexist with the OTel pipeline (parallel consumers, no contention).

For production export, swap `AddConsoleExporter()` for `AddOtlpExporter()` pointed at your collector. The `MeterListener` / `ActivityListener` code is illustrative — in production you would normally rely on the OpenTelemetry pipeline alone.
