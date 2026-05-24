using System.Collections.Concurrent;
using System.Diagnostics;

namespace Qyl.Playground;

public sealed class AgentActivityListener : IDisposable
{
    private const int MaxRecentTraces = 50;

    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<AgentTraceRecord> _recentTraces = new();

    private long _activitiesStarted;
    private long _activitiesStopped;
    private long _toolCallsTraced;
    private long _chatCallsTraced;
    private long _failedActivities;

    public AgentActivityListener()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentActivitySource.Name,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = OnActivityStarted,
            ActivityStopped = OnActivityStopped
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public AgentTraceSnapshot GetSnapshot()
    {
        return new AgentTraceSnapshot(
            ActivitiesStarted: Interlocked.Read(ref _activitiesStarted),
            ActivitiesStopped: Interlocked.Read(ref _activitiesStopped),
            ToolCallsTraced: Interlocked.Read(ref _toolCallsTraced),
            ChatCallsTraced: Interlocked.Read(ref _chatCallsTraced),
            FailedActivities: Interlocked.Read(ref _failedActivities),
            RecentTraces: _recentTraces.ToArray());
    }

    public void Dispose() => _listener.Dispose();

    private void OnActivityStarted(Activity activity)
    {
        Interlocked.Increment(ref _activitiesStarted);
    }

    private void OnActivityStopped(Activity activity)
    {
        Interlocked.Increment(ref _activitiesStopped);

        var operation = activity.GetTagItem(GenAiConventions.OperationName) as string;
        switch (operation)
        {
            case GenAiConventions.Operations.ExecuteTool:
                Interlocked.Increment(ref _toolCallsTraced);
                break;
            case GenAiConventions.Operations.Chat:
                Interlocked.Increment(ref _chatCallsTraced);
                break;
        }

        if (activity.Status == ActivityStatusCode.Error)
        {
            Interlocked.Increment(ref _failedActivities);
        }

        if (activity.Parent is null)
        {
            _recentTraces.Enqueue(BuildRecord(activity));
            while (_recentTraces.Count > MaxRecentTraces)
            {
                _recentTraces.TryDequeue(out _);
            }
        }
    }

    private static AgentTraceRecord BuildRecord(Activity activity)
    {
        long inputTokens = 0;
        long outputTokens = 0;
        string? scenario = null;
        string? model = null;
        string? outcome = null;

        foreach (var tag in activity.TagObjects)
        {
            switch (tag.Key)
            {
                case GenAiConventions.UsageInputTokens:
                    inputTokens = ToLong(tag.Value);
                    break;
                case GenAiConventions.UsageOutputTokens:
                    outputTokens = ToLong(tag.Value);
                    break;
                case "agent.scenario":
                    scenario = tag.Value?.ToString();
                    break;
                case GenAiConventions.RequestModel:
                    model = tag.Value?.ToString();
                    break;
                case "agent.outcome":
                    outcome = tag.Value?.ToString();
                    break;
            }
        }

        return new AgentTraceRecord(
            TraceId: activity.TraceId.ToString(),
            SpanId: activity.SpanId.ToString(),
            OperationName: activity.OperationName,
            DisplayName: activity.DisplayName,
            Scenario: scenario,
            Model: model,
            Outcome: outcome,
            Status: activity.Status.ToString(),
            DurationMs: activity.Duration.TotalMilliseconds,
            InputTokens: inputTokens,
            OutputTokens: outputTokens);
    }

    private static long ToLong(object? value) => value switch
    {
        long l => l,
        int i => i,
        _ => 0
    };
}
