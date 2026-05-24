namespace Qyl.Playground;

public enum AgentScenario
{
    Research,
    Coding,
    Review
}

public static class AgentScenarioParser
{
    public static AgentScenario Parse(string? value)
    {
        if (Enum.TryParse<AgentScenario>(value, ignoreCase: true, out var scenario))
        {
            return scenario;
        }

        return Random.Shared.Next(3) switch
        {
            0 => AgentScenario.Research,
            1 => AgentScenario.Coding,
            _ => AgentScenario.Review
        };
    }

    public static string ToTagValue(this AgentScenario scenario) => scenario switch
    {
        AgentScenario.Research => "research",
        AgentScenario.Coding => "coding",
        AgentScenario.Review => "review",
        _ => "unknown"
    };
}

