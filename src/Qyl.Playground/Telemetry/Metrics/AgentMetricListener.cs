using System.Diagnostics.Metrics;

namespace Qyl.Playground;

public sealed class AgentMetricListener : IDisposable
{
    private readonly MeterListener _listener;
    private readonly Lock _durationLock = new();
    private readonly Lock _tokenHistogramLock = new();

    private long _turnsStarted;
    private long _turnsCompleted;
    private long _toolCalls;
    private long _tokenUsage;
    private long _activeTurns;
    private long _queueDepth;
    private long _totalInputTokens;
    private long _totalOutputTokens;
    private long _activeResearchTurns;
    private long _activeCodingTurns;
    private long _activeReviewTurns;
    private double _successRate;
    private long _durationCount;
    private double _durationSeconds;
    private long _tokenHistogramCount;
    private long _tokenHistogramTotal;

    public AgentMetricListener()
    {
        _listener = new MeterListener
        {
            InstrumentPublished = OnInstrumentPublished
        };

        _listener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);
        _listener.Start();
    }

    public AgentMetricSnapshot GetSnapshot()
    {
        _listener.RecordObservableInstruments();

        long durationCount;
        double durationSeconds;
        lock (_durationLock)
        {
            durationCount = _durationCount;
            durationSeconds = _durationSeconds;
        }

        long tokenHistogramCount;
        long tokenHistogramTotal;
        lock (_tokenHistogramLock)
        {
            tokenHistogramCount = _tokenHistogramCount;
            tokenHistogramTotal = _tokenHistogramTotal;
        }

        return new AgentMetricSnapshot(
            TurnsStarted: Interlocked.Read(ref _turnsStarted),
            TurnsCompleted: Interlocked.Read(ref _turnsCompleted),
            ToolCalls: Interlocked.Read(ref _toolCalls),
            TokenUsageDeltas: Interlocked.Read(ref _tokenUsage),
            ActiveTurns: Interlocked.Read(ref _activeTurns),
            LastQueueDepth: Interlocked.Read(ref _queueDepth),
            TotalInputTokens: Interlocked.Read(ref _totalInputTokens),
            TotalOutputTokens: Interlocked.Read(ref _totalOutputTokens),
            ActiveResearchTurns: Interlocked.Read(ref _activeResearchTurns),
            ActiveCodingTurns: Interlocked.Read(ref _activeCodingTurns),
            ActiveReviewTurns: Interlocked.Read(ref _activeReviewTurns),
            SuccessRate: Volatile.Read(ref _successRate),
            AverageDurationMs: durationCount == 0 ? 0 : durationSeconds / durationCount * 1_000,
            DurationSamples: durationCount,
            AverageTokensPerTurn: tokenHistogramCount == 0 ? 0 : tokenHistogramTotal / (double)tokenHistogramCount,
            TokenHistogramSamples: tokenHistogramCount);
    }

    public void Dispose() => _listener.Dispose();

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        if (instrument.Meter.Name != AgentWorkflowMetrics.MeterName)
        {
            return;
        }

        var enabled = instrument.Name is
            "agent.turn.started" or
            "agent.turn.completed" or
            "agent.tool.call.count" or
            "agent.token.usage" or
            "agent.turn.active" or
            "agent.queue.depth" or
            "agent.turn.duration" or
            "agent.turn.tokens" or
            "agent.token.total" or
            "agent.turn.active_by_scenario" or
            "agent.turn.success_rate";

        if (enabled)
        {
            listener.EnableMeasurementEvents(instrument, this);
        }
    }

    private static void OnMeasurementRecorded(
        Instrument instrument,
        long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        var listener = (AgentMetricListener)state!;

        switch (instrument.Name)
        {
            case "agent.turn.started":
                Interlocked.Add(ref listener._turnsStarted, measurement);
                break;
            case "agent.turn.completed":
                Interlocked.Add(ref listener._turnsCompleted, measurement);
                break;
            case "agent.tool.call.count":
                Interlocked.Add(ref listener._toolCalls, measurement);
                break;
            case "agent.token.usage":
                Interlocked.Add(ref listener._tokenUsage, measurement);
                break;
            case "agent.turn.active":
                Interlocked.Add(ref listener._activeTurns, measurement);
                break;
            case "agent.queue.depth":
                Interlocked.Exchange(ref listener._queueDepth, measurement);
                break;
            case "agent.turn.tokens":
                lock (listener._tokenHistogramLock)
                {
                    listener._tokenHistogramCount++;
                    listener._tokenHistogramTotal += measurement;
                }

                break;
            case "agent.token.total":
                RecordTokenTotal(listener, measurement, tags);
                break;
            case "agent.turn.active_by_scenario":
                RecordActiveByScenario(listener, measurement, tags);
                break;
        }
    }

    private static void OnMeasurementRecorded(
        Instrument instrument,
        double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        var listener = (AgentMetricListener)state!;

        switch (instrument.Name)
        {
            case "agent.turn.duration":
                lock (listener._durationLock)
                {
                    listener._durationCount++;
                    listener._durationSeconds += measurement;
                }

                break;
            case "agent.turn.success_rate":
                Volatile.Write(ref listener._successRate, measurement);
                break;
        }
    }

    private static void RecordTokenTotal(
        AgentMetricListener listener,
        long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var kind = FindTag(tags, "agent.token.kind");
        switch (kind)
        {
            case "input":
                Interlocked.Exchange(ref listener._totalInputTokens, measurement);
                break;
            case "output":
                Interlocked.Exchange(ref listener._totalOutputTokens, measurement);
                break;
        }
    }

    private static void RecordActiveByScenario(
        AgentMetricListener listener,
        long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var scenario = FindTag(tags, "agent.scenario");
        switch (scenario)
        {
            case "research":
                Interlocked.Exchange(ref listener._activeResearchTurns, measurement);
                break;
            case "coding":
                Interlocked.Exchange(ref listener._activeCodingTurns, measurement);
                break;
            case "review":
                Interlocked.Exchange(ref listener._activeReviewTurns, measurement);
                break;
        }
    }

    private static string? FindTag(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        string name)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == name)
            {
                return tag.Value?.ToString();
            }
        }

        return null;
    }
}
