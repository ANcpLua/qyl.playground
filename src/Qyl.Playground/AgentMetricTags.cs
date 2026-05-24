using System.Diagnostics;

namespace Qyl.Playground;

public readonly record struct AgentTurnTags(
    AgentScenario Scenario,
    AgentModel Model)
{
    public TagList ToTagList()
    {
        var tags = new TagList
        {
            { "agent.scenario", Scenario.ToTagValue() },
            { "ai.model", Model.ToTagValue() }
        };

        return tags;
    }
}

public readonly record struct AgentOutcomeTags(
    AgentScenario Scenario,
    AgentModel Model,
    AgentOutcome Outcome)
{
    public TagList ToTagList()
    {
        var tags = new TagList
        {
            { "agent.scenario", Scenario.ToTagValue() },
            { "ai.model", Model.ToTagValue() },
            { "agent.outcome", Outcome.ToTagValue() }
        };

        return tags;
    }
}

public readonly record struct AgentToolTags(
    AgentScenario Scenario,
    AgentModel Model,
    AgentTool Tool)
{
    public TagList ToTagList()
    {
        var tags = new TagList
        {
            { "agent.scenario", Scenario.ToTagValue() },
            { "ai.model", Model.ToTagValue() },
            { "agent.tool.name", Tool.ToTagValue() }
        };

        return tags;
    }
}
