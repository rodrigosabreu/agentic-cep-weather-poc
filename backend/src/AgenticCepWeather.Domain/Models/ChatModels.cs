namespace AgenticCepWeather.Domain.Models;

public record ChatMessage(
    string Role,
    string Content,
    DateTime Timestamp = default,
    IReadOnlyList<ToolCall>? ToolCalls = null,
    string? ToolCallId = null
)
{
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

public record ToolCall(
    string Id,
    string Name,
    string Arguments
);

public record ToolResult(
    string ToolCallId,
    string Name,
    string Result,
    bool Success = true,
    string? Error = null
);

public record ChatSession(
    string Id,
    IReadOnlyList<ChatMessage> Messages,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public static ChatSession Create(string id) => new(
        id,
        [],
        DateTime.UtcNow,
        DateTime.UtcNow
    );

    public ChatSession AddMessage(ChatMessage message) => this with
    {
        Messages = [..Messages, message],
        UpdatedAt = DateTime.UtcNow
    };
}

public enum ToolName
{
    ConsultarCep,
    ConsultarPrevisaoTempo
}

public static class ToolNames
{
    public const string ConsultarCep = "consultar_cep";
    public const string ConsultarPrevisaoTempo = "consultar_previsao_tempo";
    public static readonly IReadOnlyList<string> All = [ConsultarCep, ConsultarPrevisaoTempo];
}