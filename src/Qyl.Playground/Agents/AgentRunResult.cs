namespace Qyl.Playground;

public sealed record AgentRunResult(
    string Scenario,
    string Model,
    string Outcome,
    int ToolCalls,
    long InputTokens,
    long OutputTokens,
    double DurationMs);

