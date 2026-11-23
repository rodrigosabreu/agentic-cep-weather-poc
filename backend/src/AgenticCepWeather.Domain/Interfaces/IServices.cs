namespace AgenticCepWeather.Domain.Interfaces;

using AgenticCepWeather.Domain.Models;

public interface IChatSessionStore
{
    Task<ChatSession?> GetAsync(string sessionId, CancellationToken ct = default);
    Task<ChatSession> CreateAsync(string sessionId, CancellationToken ct = default);
    Task<ChatSession> UpdateAsync(ChatSession session, CancellationToken ct = default);
    Task<bool> DeleteAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListSessionIdsAsync(CancellationToken ct = default);
}

public interface ILlmClient
{
    string ProviderName { get; }
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);
    Task<IAsyncEnumerable<LlmStreamChunk>> StreamAsync(LlmRequest request, CancellationToken ct = default);
    bool SupportsStreaming { get; }
}

public record LlmRequest(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<ToolDefinition> Tools,
    int MaxToolIterations = 5,
    double Temperature = 0.3,
    int? MaxTokens = null
);

public record LlmResponse(
    ChatMessage Message,
    IReadOnlyList<ToolCall> ToolCalls,
    LlmUsage Usage,
    string Model
);

public record LlmStreamChunk(
    string? ContentDelta,
    IReadOnlyList<ToolCall>? ToolCallsDelta,
    bool IsComplete,
    LlmUsage? Usage = null
);

public record LlmUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens
);

public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(string toolName, string arguments, CancellationToken ct = default);
    IReadOnlyList<ToolDefinition> GetToolDefinitions();
}

public interface ICepService
{
    Task<CepResult?> ConsultarCepAsync(string cep, CancellationToken ct = default);
}

public interface IWeatherService
{
    Task<Coordinates?> GeocodeAsync(string cidade, string uf, CancellationToken ct = default);
    Task<WeatherResult?> GetForecastAsync(double latitude, double longitude, CancellationToken ct = default);
}

public interface ISystemPromptProvider
{
    string GetSystemPrompt();
    string GetToolSystemPrompt();
}