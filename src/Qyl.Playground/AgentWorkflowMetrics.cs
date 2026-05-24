using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Qyl.Playground;

public sealed class AgentWorkflowMetrics
{
    public const string MeterName = "Qyl.Playground.AgentRuntime";

    private readonly Counter<long> _turnsStarted;
    private readonly Counter<long> _turnsCompleted;
    private readonly Counter<long> _toolCalls;
    private readonly Counter<long> _tokensUsed;
    private readonly UpDownCounter<long> _activeTurns;
    private readonly Gauge<long> _queueDepth;
    private readonly Histogram<double> _turnDuration;
    private readonly Histogram<long> _tokensPerTurn;

    private long _totalInputTokens;
    private long _totalOutputTokens;
    private long _activeResearchTurns;
    private long _activeCodingTurns;
    private long _activeReviewTurns;
    private long _succeededTurns;
    private long _finishedTurns;
    private long _queueDepthValue;

    public AgentWorkflowMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _turnsStarted = meter.CreateCounter<long>(
            "agent.turn.started",
            unit: "{turn}",
            description: "Number of agent turns started.");

        _turnsCompleted = meter.CreateCounter<long>(
            "agent.turn.completed",
            unit: "{turn}",
            description: "Number of agent turns completed, tagged by bounded outcome.");

        _toolCalls = meter.CreateCounter<long>(
            "agent.tool.call.count",
            unit: "{call}",
            description: "Number of tool calls made by agent turns.");

        _tokensUsed = meter.CreateCounter<long>(
            "agent.token.usage",
            unit: "{token}",
            description: "Input and output tokens consumed by agent turns.");

        _activeTurns = meter.CreateUpDownCounter<long>(
            "agent.turn.active",
            unit: "{turn}",
            description: "Currently active agent turns.");

        _queueDepth = meter.CreateGauge<long>(
            "agent.queue.depth",
            unit: "{turn}",
            description: "Last observed queued agent turns.");

        _turnDuration = meter.CreateHistogram(
            "agent.turn.duration",
            unit: "s",
            description: "Duration of completed agent turns.",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5]
            });

        _tokensPerTurn = meter.CreateHistogram(
            "agent.turn.tokens",
            unit: "{token}",
            description: "Total tokens consumed per completed agent turn.",
            advice: new InstrumentAdvice<long>
            {
                HistogramBucketBoundaries = [128, 256, 512, 1024, 2048, 4096, 8192]
            });

        meter.CreateObservableCounter(
            "agent.token.total",
            ObserveTokenTotals,
            unit: "{token}",
            description: "Running total of input and output tokens.");

        meter.CreateObservableUpDownCounter(
            "agent.turn.active_by_scenario",
            ObserveActiveTurnsByScenario,
            unit: "{turn}",
            description: "Active agent turns split by bounded scenario.");

        meter.CreateObservableGauge(
            "agent.turn.success_rate",
            ObserveSuccessRate,
            unit: "1",
            description: "Ratio of successful completed agent turns.");
    }

    public IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        new("agent.turn.started", "Counter<long>", "Producer emits one positive delta when a turn starts."),
        new("agent.turn.completed", "Counter<long>", "Producer emits one positive delta when a turn finishes, tagged by outcome."),
        new("agent.tool.call.count", "Counter<long>", "Producer emits one positive delta per bounded tool name."),
        new("agent.token.usage", "Counter<long>", "Producer emits token deltas tagged as input or output."),
        new("agent.turn.active", "UpDownCounter<long>", "Producer increments on start and decrements on completion."),
        new("agent.queue.depth", "Gauge<long>", "Producer records the latest observed queue depth."),
        new("agent.turn.duration", "Histogram<double>", "Producer records request-like latency values in seconds."),
        new("agent.turn.tokens", "Histogram<long>", "Producer records per-turn token totals."),
        new("agent.token.total", "ObservableCounter<long>", "Consumer polls running token totals."),
        new("agent.turn.active_by_scenario", "ObservableUpDownCounter<long>", "Consumer polls current active work per scenario."),
        new("agent.turn.success_rate", "ObservableGauge<double>", "Consumer polls current success ratio.")
    ];

    public ActiveTurnScope StartTurn(AgentRunRequest request)
    {
        var tags = new AgentTurnTags(request.Scenario, request.Model).ToTagList();

        _turnsStarted.Add(1, tags);
        _activeTurns.Add(1, tags);
        AddActiveScenario(request.Scenario, 1);

        var depth = Interlocked.Increment(ref _queueDepthValue);
        _queueDepth.Record(depth, tags);

        var activity = AgentActivitySource.Instance.StartActivity(
            $"{GenAiConventions.Operations.InvokeAgent} {request.Scenario.ToTagValue()}",
            ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag(GenAiConventions.System, GenAiConventions.SystemName);
            activity.SetTag(GenAiConventions.OperationName, GenAiConventions.Operations.InvokeAgent);
            activity.SetTag(GenAiConventions.AgentName, request.Scenario.ToTagValue());
            activity.SetTag(GenAiConventions.RequestModel, request.Model.ToTagValue());
            activity.SetTag("agent.scenario", request.Scenario.ToTagValue());
            activity.SetTag("ai.model", request.Model.ToTagValue());
            BaggageLimits.TryAddBaggage(activity, "agent.session.id", Guid.NewGuid().ToString("N"));
        }

        return new ActiveTurnScope(this, request, Stopwatch.GetTimestamp(), activity);
    }

    public Activity? StartChat(AgentRunRequest request)
    {
        var activity = AgentActivitySource.Instance.StartActivity(
            $"{GenAiConventions.Operations.Chat} {request.Model.ToTagValue()}",
            ActivityKind.Client);

        if (activity is not null)
        {
            activity.SetTag(GenAiConventions.System, GenAiConventions.SystemName);
            activity.SetTag(GenAiConventions.OperationName, GenAiConventions.Operations.Chat);
            activity.SetTag(GenAiConventions.RequestModel, request.Model.ToTagValue());
            activity.SetTag(GenAiConventions.ResponseModel, request.Model.ToTagValue());
            activity.SetTag("agent.scenario", request.Scenario.ToTagValue());
        }

        return activity;
    }

    public Activity? StartToolCall(AgentRunRequest request, AgentTool tool)
    {
        var tags = new AgentToolTags(request.Scenario, request.Model, tool).ToTagList();
        _toolCalls.Add(1, tags);

        var activity = AgentActivitySource.Instance.StartActivity(
            $"{GenAiConventions.Operations.ExecuteTool} {tool.ToTagValue()}",
            ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag(GenAiConventions.System, GenAiConventions.SystemName);
            activity.SetTag(GenAiConventions.OperationName, GenAiConventions.Operations.ExecuteTool);
            activity.SetTag(GenAiConventions.ToolName, tool.ToTagValue());
            activity.SetTag(GenAiConventions.ToolCallId, Guid.NewGuid().ToString("N"));
            activity.SetTag(GenAiConventions.ToolType, "function");
            activity.SetTag("agent.scenario", request.Scenario.ToTagValue());
            activity.SetTag("ai.model", request.Model.ToTagValue());
        }

        return activity;
    }

    public void RecordTokenUsage(AgentRunRequest request, long inputTokens, long outputTokens)
    {
        var turnTags = new AgentTurnTags(request.Scenario, request.Model).ToTagList();

        var inputTags = turnTags;
        inputTags.Add("agent.token.kind", "input");
        _tokensUsed.Add(inputTokens, inputTags);

        var outputTags = turnTags;
        outputTags.Add("agent.token.kind", "output");
        _tokensUsed.Add(outputTokens, outputTags);

        Interlocked.Add(ref _totalInputTokens, inputTokens);
        Interlocked.Add(ref _totalOutputTokens, outputTokens);
    }

    private void FinishTurn(AgentRunRequest request, AgentOutcome outcome, long startTimestamp, long totalTokens)
    {
        var turnTags = new AgentTurnTags(request.Scenario, request.Model).ToTagList();
        var outcomeTags = new AgentOutcomeTags(request.Scenario, request.Model, outcome).ToTagList();

        _turnsCompleted.Add(1, outcomeTags);
        _activeTurns.Add(-1, turnTags);
        AddActiveScenario(request.Scenario, -1);

        var depth = Math.Max(0, Interlocked.Decrement(ref _queueDepthValue));
        _queueDepth.Record(depth, turnTags);

        _turnDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, outcomeTags);
        _tokensPerTurn.Record(totalTokens, outcomeTags);

        Interlocked.Increment(ref _finishedTurns);
        if (outcome == AgentOutcome.Succeeded)
        {
            Interlocked.Increment(ref _succeededTurns);
        }
    }

    private IEnumerable<Measurement<long>> ObserveTokenTotals()
    {
        yield return new Measurement<long>(
            Interlocked.Read(ref _totalInputTokens),
            new KeyValuePair<string, object?>("agent.token.kind", "input"));

        yield return new Measurement<long>(
            Interlocked.Read(ref _totalOutputTokens),
            new KeyValuePair<string, object?>("agent.token.kind", "output"));
    }

    private IEnumerable<Measurement<long>> ObserveActiveTurnsByScenario()
    {
        yield return new Measurement<long>(
            Interlocked.Read(ref _activeResearchTurns),
            new KeyValuePair<string, object?>("agent.scenario", AgentScenario.Research.ToTagValue()));

        yield return new Measurement<long>(
            Interlocked.Read(ref _activeCodingTurns),
            new KeyValuePair<string, object?>("agent.scenario", AgentScenario.Coding.ToTagValue()));

        yield return new Measurement<long>(
            Interlocked.Read(ref _activeReviewTurns),
            new KeyValuePair<string, object?>("agent.scenario", AgentScenario.Review.ToTagValue()));
    }

    private double ObserveSuccessRate()
    {
        var finished = Interlocked.Read(ref _finishedTurns);
        if (finished == 0)
        {
            return 0;
        }

        return (double)Interlocked.Read(ref _succeededTurns) / finished;
    }

    private void AddActiveScenario(AgentScenario scenario, long delta)
    {
        switch (scenario)
        {
            case AgentScenario.Research:
                Interlocked.Add(ref _activeResearchTurns, delta);
                break;
            case AgentScenario.Coding:
                Interlocked.Add(ref _activeCodingTurns, delta);
                break;
            case AgentScenario.Review:
                Interlocked.Add(ref _activeReviewTurns, delta);
                break;
        }
    }

    public sealed class ActiveTurnScope : IDisposable
    {
        private readonly AgentWorkflowMetrics _metrics;
        private readonly AgentRunRequest _request;
        private readonly long _startTimestamp;
        private readonly Activity? _activity;
        private long _inputTokens;
        private long _outputTokens;
        private AgentOutcome _outcome;
        private bool _disposed;

        internal ActiveTurnScope(
            AgentWorkflowMetrics metrics,
            AgentRunRequest request,
            long startTimestamp,
            Activity? activity)
        {
            _metrics = metrics;
            _request = request;
            _startTimestamp = startTimestamp;
            _activity = activity;
            _inputTokens = 0;
            _outputTokens = 0;
            _outcome = AgentOutcome.Cancelled;
            _disposed = false;
        }

        public Activity? Activity => _activity;

        public void AddTokens(long inputTokens, long outputTokens)
        {
            _inputTokens += inputTokens;
            _outputTokens += outputTokens;
            _metrics.RecordTokenUsage(_request, inputTokens, outputTokens);

            _activity?.SetTag(GenAiConventions.UsageInputTokens, _inputTokens);
            _activity?.SetTag(GenAiConventions.UsageOutputTokens, _outputTokens);
        }

        public void Complete(AgentOutcome outcome)
        {
            _outcome = outcome;
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _metrics.FinishTurn(_request, _outcome, _startTimestamp, _inputTokens + _outputTokens);

            if (_activity is not null)
            {
                _activity.SetTag("agent.outcome", _outcome.ToTagValue());
                _activity.SetTag(GenAiConventions.ResponseFinishReasons, new[] { _outcome.ToTagValue() });
                _activity.SetStatus(_outcome switch
                {
                    AgentOutcome.Succeeded => ActivityStatusCode.Ok,
                    AgentOutcome.Failed => ActivityStatusCode.Error,
                    AgentOutcome.Cancelled => ActivityStatusCode.Error,
                    _ => ActivityStatusCode.Unset
                });
                _activity.Dispose();
            }
        }
    }
}
