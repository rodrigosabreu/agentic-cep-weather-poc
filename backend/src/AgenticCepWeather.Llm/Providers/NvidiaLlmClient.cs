namespace AgenticCepWeather.Llm.Providers;

using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Domain.Exceptions;
using AgenticCepWeather.Domain.Configuration;
using AgenticCepWeather.Llm.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class NvidiaLlmClient : BaseLlmClient
{
    private readonly NvidiaSettings _settings;
    private readonly ILogger<NvidiaLlmClient> _logger;

    public override string ProviderName => "Nvidia";
    public override bool SupportsStreaming => _settings.EnableStreaming;

    public NvidiaLlmClient(
        HttpClient httpClient,
        IOptions<LlmSettings> settings,
        ILogger<NvidiaLlmClient> logger) : base(httpClient, logger)
    {
        _settings = settings.Value.Nvidia;
        _logger = logger;

        HttpClient.BaseAddress = new Uri(_settings.BaseUrl);
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public override async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _settings.Model,
            messages = BuildMessages(request),
            tools = BuildTools(request.Tools),
            tool_choice = "auto",
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = false
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = content
        };

        try
        {
            var response = await HttpClient.SendAsync(requestMessage, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("NVIDIA API erro {StatusCode}: {Content}", response.StatusCode, responseContent);
                throw new DomainExceptions.LlmProviderException("Nvidia", $"API retornou {response.StatusCode}: {responseContent}");
            }

            return ParseResponse(responseContent);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de rede ao chamar NVIDIA API");
            throw new DomainExceptions.LlmProviderException("Nvidia", "Erro de conexão com a API", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Erro ao deserializar resposta da NVIDIA API");
            throw new DomainExceptions.LlmProviderException("Nvidia", "Resposta inválida da API", ex);
        }
    }

    public override async Task<IAsyncEnumerable<LlmStreamChunk>> StreamAsync(LlmRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _settings.Model,
            messages = BuildMessages(request),
            tools = BuildTools(request.Tools),
            tool_choice = "auto",
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = true
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = content
        };

        var response = await HttpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("NVIDIA API streaming erro {StatusCode}: {Content}", response.StatusCode, errorContent);
            throw new DomainExceptions.LlmProviderException("Nvidia", $"API retornou {response.StatusCode}: {errorContent}");
        }

        var stream = await response.Content.ReadAsStreamAsync(ct);
        return ParseStreamAsync(stream, ct);
    }

    private LlmResponse ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var choice = root.GetProperty("choices")[0];
        var message = choice.GetProperty("message");

        var content = message.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? string.Empty : string.Empty;

        var toolCalls = new List<ToolCall>();
        if (message.TryGetProperty("tool_calls", out var toolCallsProp))
        {
            foreach (var tc in toolCallsProp.EnumerateArray())
            {
                var function = tc.GetProperty("function");
                toolCalls.Add(new ToolCall(
                    tc.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                    function.GetProperty("name").GetString() ?? string.Empty,
                    function.GetProperty("arguments").GetString() ?? "{}"
                ));
            }
        }

        var usage = root.TryGetProperty("usage", out var usageProp) ? new LlmUsage(
            usageProp.GetProperty("prompt_tokens").GetInt32(),
            usageProp.GetProperty("completion_tokens").GetInt32(),
            usageProp.GetProperty("total_tokens").GetInt32()
        ) : new LlmUsage(0, 0, 0);

        var model = root.GetProperty("model").GetString() ?? _settings.Model;

        return new LlmResponse(
            new ChatMessage("assistant", content, DateTime.UtcNow, toolCalls),
            toolCalls,
            usage,
            model
        );
    }

    private async IAsyncEnumerable<LlmStreamChunk> ParseStreamAsync(Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream);
        var accumulatedContent = new System.Text.StringBuilder();
        var accumulatedToolCalls = new Dictionary<int, ToolCallBuilder>();

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            var data = line[6..].Trim();
            if (data == "[DONE]")
                break;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    continue;

                var choice = choices[0];
                var delta = choice.GetProperty("delta");

                string? contentDelta = null;
                if (delta.TryGetProperty("content", out var contentProp) && contentProp.ValueKind != JsonValueKind.Null)
                {
                    contentDelta = contentProp.GetString();
                    if (!string.IsNullOrEmpty(contentDelta))
                    {
                        accumulatedContent.Append(contentDelta);
                    }
                }

                if (delta.TryGetProperty("tool_calls", out var toolCallsProp))
                {
                    foreach (var tc in toolCallsProp.EnumerateArray())
                    {
                        var index = tc.GetProperty("index").GetInt32();
                        if (!accumulatedToolCalls.TryGetValue(index, out var builder))
                        {
                            builder = new ToolCallBuilder();
                            accumulatedToolCalls[index] = builder;
                        }

                        if (tc.TryGetProperty("id", out var idProp))
                            builder.Id = idProp.GetString() ?? Guid.NewGuid().ToString();

                        if (tc.TryGetProperty("function", out var funcProp))
                        {
                            if (funcProp.TryGetProperty("name", out var nameProp))
                                builder.Name = nameProp.GetString() ?? string.Empty;

                            if (funcProp.TryGetProperty("arguments", out var argsProp))
                                builder.Arguments += argsProp.GetString() ?? string.Empty;
                        }
                    }
                }

                var isComplete = choice.TryGetProperty("finish_reason", out var finishReason) &&
                                 finishReason.ValueKind != JsonValueKind.Null &&
                                 finishReason.GetString() != null;

                if (isComplete)
                {
                    var toolCalls = accumulatedToolCalls.Values
                        .Where(b => !string.IsNullOrEmpty(b.Name))
                        .Select(b => new ToolCall(b.Id, b.Name, b.Arguments))
                        .ToList();

                    var usage = root.TryGetProperty("usage", out var usageProp) ? new LlmUsage(
                        usageProp.GetProperty("prompt_tokens").GetInt32(),
                        usageProp.GetProperty("completion_tokens").GetInt32(),
                        usageProp.GetProperty("total_tokens").GetInt32()
                    ) : null;

                    yield return new LlmStreamChunk(
                        null,
                        toolCalls,
                        true,
                        usage
                    );
                    yield break;
                }

                if (!string.IsNullOrEmpty(contentDelta))
                {
                    yield return new LlmStreamChunk(contentDelta, null, false);
                }
            }
            catch (JsonException)
            {
                // Ignora linhas malformadas no stream
            }
        }

        // Se o stream terminou sem finish_reason, retorna o que acumulamos
        var finalToolCalls = accumulatedToolCalls.Values
            .Where(b => !string.IsNullOrEmpty(b.Name))
            .Select(b => new ToolCall(b.Id, b.Name, b.Arguments))
            .ToList();

        yield return new LlmStreamChunk(null, finalToolCalls, true);
    }

    private class ToolCallBuilder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }
}