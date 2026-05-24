using System.Diagnostics;

namespace Qyl.Playground;

public sealed class AgentRunService(AgentWorkflowMetrics metrics)
{
    public async Task<AgentRunResult> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var turn = metrics.StartTurn(request);

        var toolCount = Random.Shared.Next(1, 5);
        var inputTokens = (long)Random.Shared.Next(250, 1_400);
        var outputTokens = (long)Random.Shared.Next(120, 1_200);

        using (var chat = metrics.StartChat(request))
        {
            await Task.Delay(Random.Shared.Next(20, 60), cancellationToken);

            turn.AddTokens(inputTokens, outputTokens);

            if (chat is not null)
            {
                chat.SetTag(GenAiConventions.UsageInputTokens, inputTokens);
                chat.SetTag(GenAiConventions.UsageOutputTokens, outputTokens);
                chat.AddEvent(new ActivityEvent("gen_ai.choice"));
            }
        }

        for (var i = 0; i < toolCount; i++)
        {
            var tool = AgentToolExtensions.RandomTool();
            using var toolActivity = metrics.StartToolCall(request, tool);
            await Task.Delay(Random.Shared.Next(15, 90), cancellationToken);
        }

        var outcome = Random.Shared.NextDouble() switch
        {
            < 0.88 => AgentOutcome.Succeeded,
            < 0.97 => AgentOutcome.Failed,
            _ => AgentOutcome.Cancelled
        };

        turn.Complete(outcome);

        return new AgentRunResult(
            request.Scenario.ToTagValue(),
            request.Model.ToTagValue(),
            outcome.ToTagValue(),
            toolCount,
            inputTokens,
            outputTokens,
            stopwatch.Elapsed.TotalMilliseconds);
    }
}
