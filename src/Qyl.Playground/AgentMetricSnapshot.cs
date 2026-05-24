namespace Qyl.Playground;

public sealed record AgentMetricSnapshot(
    long TurnsStarted,
    long TurnsCompleted,
    long ToolCalls,
    long TokenUsageDeltas,
    long ActiveTurns,
    long LastQueueDepth,
    long TotalInputTokens,
    long TotalOutputTokens,
    long ActiveResearchTurns,
    long ActiveCodingTurns,
    long ActiveReviewTurns,
    double SuccessRate,
    double AverageDurationMs,
    long DurationSamples,
    double AverageTokensPerTurn,
    long TokenHistogramSamples);

