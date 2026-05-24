namespace Qyl.Playground;

public sealed record AgentTraceSnapshot(
    long ActivitiesStarted,
    long ActivitiesStopped,
    long ToolCallsTraced,
    long ChatCallsTraced,
    long FailedActivities,
    IReadOnlyList<AgentTraceRecord> RecentTraces);

public sealed record AgentTraceRecord(
    string TraceId,
    string SpanId,
    string OperationName,
    string DisplayName,
    string? Scenario,
    string? Model,
    string? Outcome,
    string Status,
    double DurationMs,
    long InputTokens,
    long OutputTokens);
