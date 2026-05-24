namespace Qyl.Playground;

public enum AgentTool
{
    Search,
    ReadFile,
    RunCommand,
    Summarize
}

public static class AgentToolExtensions
{
    public static AgentTool RandomTool() => Random.Shared.Next(4) switch
    {
        0 => AgentTool.Search,
        1 => AgentTool.ReadFile,
        2 => AgentTool.RunCommand,
        _ => AgentTool.Summarize
    };

    public static string ToTagValue(this AgentTool tool) => tool switch
    {
        AgentTool.Search => "search",
        AgentTool.ReadFile => "read_file",
        AgentTool.RunCommand => "run_command",
        AgentTool.Summarize => "summarize",
        _ => "unknown"
    };
}

