namespace AgenticCepWeather.Llm.Abstractions;

using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

public abstract class BaseLlmClient : ILlmClient
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    protected BaseLlmClient(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient;
        Logger = logger;
    }

    public abstract string ProviderName { get; }
    public abstract bool SupportsStreaming { get; }
    public abstract Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);
    public abstract Task<IAsyncEnumerable<LlmStreamChunk>> StreamAsync(LlmRequest request, CancellationToken ct = default);

    protected static List<Dictionary<string, object>> BuildMessages(LlmRequest request)
    {
        var messages = new List<Dictionary<string, object>>();

        foreach (var msg in request.Messages)
        {
            var message = new Dictionary<string, object>
            {
                ["role"] = msg.Role,
                ["content"] = msg.Content ?? string.Empty
            };

            if (msg.ToolCalls?.Count > 0)
            {
                message["tool_calls"] = msg.ToolCalls.Select(tc => new Dictionary<string, object>
                {
                    ["id"] = tc.Id,
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = tc.Name,
                        ["arguments"] = tc.Arguments
                    }
                }).ToList();
            }

            if (!string.IsNullOrEmpty(msg.ToolCallId))
            {
                message["tool_call_id"] = msg.ToolCallId;
            }

            messages.Add(message);
        }

        return messages;
    }

    protected static List<Dictionary<string, object>> BuildTools(IReadOnlyList<ToolDefinition> tools)
    {
        return tools.Select(t => new Dictionary<string, object>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object>
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["parameters"] = new Dictionary<string, object>
                {
                    ["type"] = t.Parameters.Type,
                    ["properties"] = t.Parameters.Properties?.ToDictionary(
                        p => p.Key,
                        p => (object)new Dictionary<string, object>
                        {
                            ["type"] = p.Value.Type,
                            ["description"] = p.Value.Description ?? string.Empty
                        }) ?? new Dictionary<string, object>(),
                    ["required"] = t.Parameters.Required ?? new List<string>()
                }
            }
        }).ToList();
    }
}