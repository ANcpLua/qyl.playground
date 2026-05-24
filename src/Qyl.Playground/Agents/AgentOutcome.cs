namespace Qyl.Playground;

public enum AgentOutcome
{
    Succeeded,
    Failed,
    Cancelled
}

public static class AgentOutcomeExtensions
{
    public static string ToTagValue(this AgentOutcome outcome) => outcome switch
    {
        AgentOutcome.Succeeded => "succeeded",
        AgentOutcome.Failed => "failed",
        AgentOutcome.Cancelled => "cancelled",
        _ => "unknown"
    };
}

