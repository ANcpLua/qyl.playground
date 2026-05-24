# Agent guidance

Conventions for AI assistants and contributors working in this repo. `CLAUDE.md` is a symlink to this file.

## Build, test, run

```bash
dotnet build Qyl.Playground.slnx
dotnet test  Qyl.Playground.slnx --no-build
dotnet run   --project src/Qyl.Playground -- --demo --duration 6 --parallelism 4
```

## Layout

```
src/Qyl.Playground/
├── Agents/              the simulated workload
├── Telemetry/
│   ├── Metrics/         Meter + MeterListener
│   ├── Tracing/         ActivitySource + ActivityListener
│   ├── Propagation/     W3C TraceContext + baggage
│   └── Exporters/       OpenTelemetry SDK wiring
└── Hosting/             background services + entry point
```

All files use the flat `Qyl.Playground` namespace regardless of folder. This is on purpose — the lab reads link-to-definition rather than alphabetically.

## Conventions

These are deliberate choices, not oversights. Verify the rationale before changing them.

- Use the raw `System.Diagnostics.Metrics` APIs. The `[Counter<T>]` source generator from `Microsoft.Extensions.Telemetry.Abstractions` hides the API and forces `enum.ToString()` for tag values.
- Tag values come from enums via `ToTagValue()` extension methods (see `Agents/AgentScenario.cs`). Never call `enum.ToString()` on the hot path.
- OpenTelemetry GenAI semantic-convention attribute names live in `Telemetry/GenAiConventions.cs`. Don't inline `gen_ai.*` strings elsewhere.
- The raw `MeterListener` / `ActivityListener` and the OpenTelemetry SDK both run. The same `Meter` and `ActivitySource` feed both — they don't contend.
- Lock targets use `System.Threading.Lock` (.NET 9+), not `object`.
- Hot-path logging uses `[LoggerMessage]` partial methods, not `ILogger.LogInformation(string, params object?[])`.
- `BaggageLimits.TryAddBaggage` enforces W3C baggage caps. The .NET runtime does not. Use it instead of `Activity.AddBaggage` directly.
- `TraceContextPropagation.Inject` / `.Extract` handles W3C propagation for non-HTTP transports.

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

The Spectre dashboard owns stdout when active, so the Console exporter is suppressed in that case.

## What this is not

A production observability framework. The raw listeners are for reading and learning. The OpenTelemetry pipeline is the production path — set `OTEL_EXPORTER_OTLP_ENDPOINT` to a collector address and drop the raw listeners.
