# Qyl.Playground

A .NET 10 sample that instruments a simulated AI-agent workload with metrics and traces and ships them through the OpenTelemetry SDK and the Qyl.OpenTelemetry auto-instrumentation stack.

The workload simulates an agent making turns that each issue one chat call and a handful of tool calls. This produces a realistic span tree and a representative set of metrics without needing a real model. The result is a concrete reference for how `Meter`, `ActivitySource`, the OpenTelemetry SDK, and the Qyl stack fit together in one app.

## Quick start

```bash
git clone https://github.com/ANcpLua/qyl.playground
cd qyl.playground
dotnet run --project src/Qyl.Playground -- --demo
```

The demo runs for a few seconds with a live console dashboard. Press ctrl+c to stop.

## What it produces

Each simulated agent turn emits:

- one root `invoke_agent` span,
- one `chat` span for the model call, with input and output token counts as tags,
- one to four `execute_tool` spans for tool invocations inside the turn.

Metrics record turn counts, active turns, queue depth, token totals, success rate, and per-turn duration and token histograms.

Telemetry is collected and exported by the OpenTelemetry SDK, alongside the Qyl auto-instrumentation stack (booted via `AddQylAutoInstrumentation()`). The live console dashboard reads the same signals back in-process — metrics through an OpenTelemetry SDK in-memory reader, spans through a thin `ActivityListener` on the agent's `ActivitySource`.

Spans carry OpenTelemetry GenAI semantic-convention attributes sourced from `Qyl.OpenTelemetry.SemanticConventions`: `gen_ai.operation.name`, `gen_ai.request.model`, `gen_ai.usage.input_tokens`, `gen_ai.tool.name`, and so on.

## Project layout

```
src/Qyl.Playground/
├── Agents/              the simulated workload
├── Telemetry/
│   ├── Metrics/         agent-domain Meter + SDK in-memory reader
│   ├── Tracing/         agent-domain ActivitySource + thin span listener
│   └── Exporters/       OpenTelemetry SDK + Qyl stack wiring
└── Hosting/             background services + entry point
tests/Qyl.Playground.Tests/
```

## Configuration

Exporter selection is automatic:

| Condition | Exporter |
|-----------|----------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` is set | OTLP |
| Development, no OTLP, no live dashboard | Console |
| Production, no OTLP endpoint | None (the in-process dashboard and `/metrics/snapshot` still read via the SDK) |

The Spectre.Console live dashboard activates when stdout is a TTY and `--demo` is passed. Piped runs and CI fall back to a periodic `ILogger` reporter so the output stays parseable.

### Command-line flags

| Flag | Default | Description |
|------|---------|-------------|
| `--demo` | off | run the bounded workload |
| `--duration N` | 8 | seconds to run |
| `--parallelism N` | 4 | parallel agent workers |
| `--interval N` | 1 | dashboard / reporter tick seconds |
| `--no-dashboard` | off | skip the Spectre UI in a TTY |
| `--report` | off | force the structured logger |

## HTTP endpoints

When run without `--demo`, the app stays up and serves:

| Path | Description |
|------|-------------|
| `GET /agent/run` | Run one agent turn |
| `GET /agent/run-with-context` | Run one turn under an externally supplied W3C `traceparent` |
| `GET /agent/propagation-headers` | Get W3C headers to inject into a downstream non-HTTP message |
| `GET /metrics/snapshot` | Current metric snapshot (SDK in-memory reader) as JSON |
| `GET /metrics/definitions` | Instruments this app declares |
| `GET /trace/snapshot` | Current trace snapshot as JSON |

## Tests

```bash
dotnet test Qyl.Playground.slnx
```

## Requirements

- .NET 10 SDK
- A terminal that supports ANSI escapes if you want the live dashboard (most modern ones)
