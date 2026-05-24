# Qyl.Playground

A .NET 10 sample that instruments a simulated AI-agent workload with metrics and traces, then exposes them through both the raw `System.Diagnostics` APIs and the OpenTelemetry SDK.

The workload simulates an agent making turns that each issue one chat call and a handful of tool calls. This produces a realistic span tree and a representative set of metrics without needing a real model. The result is a concrete reference for how `Meter`, `ActivitySource`, `MeterListener`, `ActivityListener`, and OpenTelemetry fit together in one app.

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

The same `Meter` and `ActivitySource` feed two consumers in parallel:

- a hand-written `MeterListener` and `ActivityListener` that make the .NET diagnostic APIs visible,
- the OpenTelemetry SDK with a console or OTLP exporter.

Spans carry OpenTelemetry GenAI semantic-convention attributes: `gen_ai.system`, `gen_ai.operation.name`, `gen_ai.request.model`, `gen_ai.usage.input_tokens`, `gen_ai.tool.name`, and so on.

## Project layout

```
src/Qyl.Playground/
├── Agents/              the simulated workload
├── Telemetry/
│   ├── Metrics/         Meter + MeterListener consumer
│   ├── Tracing/         ActivitySource + ActivityListener consumer
│   ├── Propagation/     W3C TraceContext + baggage helpers
│   └── Exporters/       OpenTelemetry SDK wiring
└── Hosting/             background services + entry point
tests/Qyl.Playground.Tests/
```

## Configuration

Exporter selection is automatic:

| Condition | Exporter |
|-----------|----------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` is set | OTLP |
| Development, no OTLP, no live dashboard | Console |
| Production, no OTLP endpoint | None (raw listeners still receive data) |

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
| `GET /metrics/snapshot` | Current `MeterListener` snapshot as JSON |
| `GET /metrics/definitions` | Instruments this app declares |
| `GET /trace/snapshot` | Current `ActivityListener` snapshot as JSON |

## Tests

```bash
dotnet test Qyl.Playground.slnx
```

## Requirements

- .NET 10 SDK
- A terminal that supports ANSI escapes if you want the live dashboard (most modern ones)
