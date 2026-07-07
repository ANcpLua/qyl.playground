# Agent guidance

Conventions for AI assistants and contributors working in this repo. `CLAUDE.md` is a symlink to this file.

## Build, test, run

```bash
dotnet build Qyl.Playground.slnx
dotnet test  Qyl.Playground.slnx --no-build
dotnet run   --project src/Qyl.Playground -- --demo --duration 6 --parallelism 4
```

## The stack

The playground is built **on** the Qyl.OpenTelemetry stack (aligned at 3.0.2). There is no hand-rolled telemetry plumbing — the OpenTelemetry SDK collects and exports, and the app only defines its own agent-domain instruments.

- `Qyl.OpenTelemetry.SemanticConventions(.Incubating)` — the canonical source of `gen_ai.*` and other attribute keys (Weaver-generated, OTel semconv 1.41.0).
- `Qyl.OpenTelemetry.AutoInstrumentation.Hosting` — zero-code instrumentation, booted explicitly via `AddQylAutoInstrumentation()`; its spans flow from `QylActivitySource` and are exported alongside the app's own.

## Layout

```
src/Qyl.Playground/
├── Agents/              the simulated workload
├── Telemetry/
│   ├── Metrics/         agent-domain Meter + SDK in-memory reader
│   ├── Tracing/         agent-domain ActivitySource + thin span listener
│   └── Exporters/       OpenTelemetry SDK + Qyl stack wiring
└── Hosting/             background services + entry point
```

All files use the flat `Qyl.Playground` namespace regardless of folder. This is on purpose — the lab reads link-to-definition rather than alphabetically.

## Conventions

These are deliberate choices, not oversights. Verify the rationale before changing them.

- The app defines its own agent-domain instruments with the raw `System.Diagnostics.Metrics` API (`Meter`, `Counter<T>`, `Histogram<T>`). The `[Counter<T>]` source generator from `Microsoft.Extensions.Telemetry.Abstractions` hides the API and forces `enum.ToString()` for tag values — don't use it. Library-level signals come from the Qyl auto-instrumentation, not from here.
- Tag values come from enums via `ToTagValue()` extension methods (see `Agents/AgentScenario.cs`). Never call `enum.ToString()` on the hot path.
- `gen_ai.*` and other semantic-convention keys come from the `Qyl.OpenTelemetry.SemanticConventions` package through the `Telemetry/GenAiConventions.cs` facade — the single binding point. Never inline `gen_ai.*` string literals. `gen_ai.provider.name` is the model provider (openai/anthropic/…), not the app — the app is identified by `service.name`.
- The in-process dashboard reads metrics through a dedicated OpenTelemetry SDK in-memory reader (`AgentMetricCollector`) and spans through a thin `ActivityListener` (`AgentActivityListener`) — the same pattern the stack's own `LiveInstrumentationDemo` uses. The exported telemetry runs through the SDK in parallel.
- Non-HTTP context propagation uses the stack's `CompositeTextMapPropagator` (W3C TraceContext + Baggage) via `Propagators.DefaultTextMapPropagator`. HTTP ingress/egress is propagated automatically by the instrumentation.
- Lock targets use `System.Threading.Lock` (.NET 9+), not `object`.
- Hot-path logging uses `[LoggerMessage]` partial methods, not `ILogger.LogInformation(string, params object?[])`.

## Span shape per agent run

```
invoke_agent {scenario}       Internal   (root)
├── chat {model}              Client     (LLM call)
│   └── event: gen_ai.choice
└── execute_tool {tool}       Internal   (1..4 children)
```

The root span carries cumulative token usage, `gen_ai.response.finish_reasons`, `agent.outcome`, and an `ActivityStatusCode` derived from the outcome.

## Exporter selection

Picked automatically by `OpenTelemetryExtensions.AddPlaygroundOpenTelemetry`:

- `OTEL_EXPORTER_OTLP_ENDPOINT` set → OTLP.
- Development + no OTLP + no live dashboard → Console.
- Production + no OTLP → none.

The Spectre dashboard owns stdout when active, so the Console exporter is suppressed in that case. Either way the in-process dashboard and `/metrics/snapshot` read through the SDK in-memory reader, independent of the export path.
