namespace Qyl.Playground;

public readonly record struct AgentRunRequest(AgentScenario Scenario, AgentModel Model)
{
    public static AgentRunRequest FromQuery(string? scenario, string? model) =>
        new(AgentScenarioParser.Parse(scenario), AgentModelParser.Parse(model));
}

