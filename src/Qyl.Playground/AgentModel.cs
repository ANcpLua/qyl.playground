namespace Qyl.Playground;

public enum AgentModel
{
    Fast,
    Balanced,
    Frontier
}

public static class AgentModelParser
{
    public static AgentModel Parse(string? value)
    {
        if (Enum.TryParse<AgentModel>(value, ignoreCase: true, out var model))
        {
            return model;
        }

        return Random.Shared.Next(3) switch
        {
            0 => AgentModel.Fast,
            1 => AgentModel.Balanced,
            _ => AgentModel.Frontier
        };
    }

    public static string ToTagValue(this AgentModel model) => model switch
    {
        AgentModel.Fast => "fast",
        AgentModel.Balanced => "balanced",
        AgentModel.Frontier => "frontier",
        _ => "unknown"
    };
}

