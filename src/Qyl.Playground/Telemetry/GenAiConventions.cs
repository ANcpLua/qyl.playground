using Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi;

namespace Qyl.Playground;

// GenAI semconv keys, sourced from Qyl.OpenTelemetry.SemanticConventions (Weaver-generated,
// OTel semconv 1.41.0). This facade is the single binding point to the package — the playground
// holds no gen_ai.* string literals of its own. Every value below is a compile-time alias of a
// package constant, so callers and switch `case` labels keep working unchanged.
public static class GenAiConventions
{
    // gen_ai.system is deprecated upstream in favor of gen_ai.provider.name; use the current key.
    public const string ProviderName = GenAiAttributes.ProviderName;
    public const string OperationName = GenAiAttributes.OperationName;
    public const string RequestModel = GenAiAttributes.RequestModel;
    public const string ResponseModel = GenAiAttributes.ResponseModel;
    public const string UsageInputTokens = GenAiAttributes.UsageInputTokens;
    public const string UsageOutputTokens = GenAiAttributes.UsageOutputTokens;
    public const string ResponseFinishReasons = GenAiAttributes.ResponseFinishReasons;

    public const string AgentName = GenAiAttributes.AgentName;
    public const string AgentId = GenAiAttributes.AgentId;

    public const string ToolName = GenAiAttributes.ToolName;
    public const string ToolCallId = GenAiAttributes.ToolCallId;
    public const string ToolType = GenAiAttributes.ToolType;

    // Playground identity, emitted as the gen_ai.provider.name value.
    public const string SystemName = "qyl-playground";

    public static class Operations
    {
        public const string InvokeAgent = GenAiAttributes.OperationNameValues.InvokeAgent;
        public const string ExecuteTool = GenAiAttributes.OperationNameValues.ExecuteTool;
        public const string Chat = GenAiAttributes.OperationNameValues.Chat;
    }
}
