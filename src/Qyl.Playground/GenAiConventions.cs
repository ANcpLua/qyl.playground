namespace Qyl.Playground;

// OpenTelemetry GenAI semantic conventions.
// Spec: https://opentelemetry.io/docs/specs/semconv/gen-ai/
// Stable as of OTel semantic conventions 1.34 (2025-2026).
public static class GenAiConventions
{
    public const string System = "gen_ai.system";
    public const string OperationName = "gen_ai.operation.name";
    public const string RequestModel = "gen_ai.request.model";
    public const string ResponseModel = "gen_ai.response.model";
    public const string UsageInputTokens = "gen_ai.usage.input_tokens";
    public const string UsageOutputTokens = "gen_ai.usage.output_tokens";
    public const string ResponseFinishReasons = "gen_ai.response.finish_reasons";

    public const string AgentName = "gen_ai.agent.name";
    public const string AgentId = "gen_ai.agent.id";

    public const string ToolName = "gen_ai.tool.name";
    public const string ToolCallId = "gen_ai.tool.call.id";
    public const string ToolType = "gen_ai.tool.type";

    public const string SystemName = "qyl-playground";

    public static class Operations
    {
        public const string InvokeAgent = "invoke_agent";
        public const string ExecuteTool = "execute_tool";
        public const string Chat = "chat";
    }
}
