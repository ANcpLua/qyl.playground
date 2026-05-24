# Qyl.Playground — Agent Guidance

A .NET 10 lab for the 2026 observability stack against an agent-style workload. Treat it as a reference implementation for learning, not a production framework.

## Build, test, run

```bash
dotnet build Qyl.Playground.slnx
dotnet test  Qyl.Playground.slnx --no-build
dotnet run   --project src/Qyl.Playground -- --demo --duration 6 --parallelism 4
```

The bounded demo prints two final snapshots: one from the raw `MeterListener` and one from the raw `ActivityListener`. Both run alongside the OpenTelemetry SDK console exporters, which emit per-span and per-metric output during the run.

## Layout

| Path | Purpose |
|------|---------|
| `src/Qyl.Playground/` | The runnable demo: instruments, listeners, OTel wiring, endpoints. |
| `tests/Qyl.Playground.Tests/` | Integration tests that exercise producer + in-process listener together. |
| `Qyl.Playground.slnx` | XML-format solution. Use this with `dotnet build` / `dotnet test`. |

## Architectural conventions

These choices are intentional. Don't "fix" them without checking the rationale first.

- **Raw `System.Diagnostics.Metrics` APIs, no `[Counter<T>]` source generator.** The generator from `Microsoft.Extensions.Telemetry.Abstractions` adds boilerplate, requires `#pragma` for `Unit`, and calls `enum.ToString()` (slow). The hand-rolled tag-struct pattern in `AgentMetricTags.cs` covers the strongly-typed-tags use case better.
- **Bounded tag values via enum + `ToTagValue()` extensions** (e.g. `AgentScenario.cs:27`). Never use `enum.ToString()` for tag values.
- **OpenTelemetry GenAI semantic conventions** for AI workload spans. Constants live in `GenAiConventions.cs`. New AI-related attributes go through that file, not as inline string literals.
- **Two parallel consumption channels are intentional.** `AgentMetricListener` / `AgentActivityListener` use the raw `MeterListener` / `ActivityListener` APIs (educational). The OpenTelemetry SDK runs alongside via `AddOpenTelemetry()` (production realism). The same `Meter` / `ActivitySource` feeds both — they do not contend.
- **`System.Threading.Lock` instead of `object` for lock targets** (.NET 9+ runtime path).
- **`[LoggerMessage]` for structured logging on the hot path.** Don't go back to `ILogger.LogInformation(string, params object?[])` for the snapshot tick — it boxes every value-type arg.
- **Tier 4 production concerns are demonstrated, not glossed over.** `BaggageLimits` enforces W3C baggage caps; `TraceContextPropagation` provides W3C inject/extract for non-HTTP transports.

## Span hierarchy emitted per agent run

```
invoke_agent {scenario}       ActivityKind.Internal  (root)
├── chat {model}              ActivityKind.Client     (LLM round)
│   └── event: gen_ai.choice
└── execute_tool {tool}       ActivityKind.Internal   (1..4 children, wraps simulated work)
```

The root span carries cumulative `gen_ai.usage.input_tokens` / `output_tokens`, `gen_ai.response.finish_reasons`, `agent.outcome`, and an `ActivityStatusCode` derived from the outcome.

## What this repo is NOT

- Not a production observability framework. For production, swap `AddConsoleExporter()` for `AddOtlpExporter()` and drop the raw listeners.
- Not a tutorial for `dotnet-counters` — the `Meter` is exposed by name (`Qyl.Playground.AgentRuntime`) so `dotnet-counters monitor -n Qyl.Playground --counters Qyl.Playground.AgentRuntime` works, but the focus is in-process consumption + OTel pipeline.
- Not coupled to any external blog series or upstream sample.
