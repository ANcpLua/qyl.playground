using System.Diagnostics;

namespace Qyl.Playground;

public static class AgentActivitySource
{
    public const string Name = "Qyl.Playground.AgentRuntime";
    public const string Version = "1.0.0";

    public static readonly ActivitySource Instance = new(Name, Version);
}
