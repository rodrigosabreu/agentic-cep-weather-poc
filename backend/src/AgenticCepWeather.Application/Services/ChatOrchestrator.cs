namespace AgenticCepWeather.Application.Services;

using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Domain.Exceptions;
using AgenticCepWeather.Llm.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgenticCepWeather.Domain.Configuration;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;

public class ChatOrchestrator : IChatOrchestrator
{
    private readonly IChatSessionStore _sessionStore;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly IToolExecutor _toolExecutor;
    private readonly ISystemPromptProvider _systemPromptProvider;
    private readonly ILogger<ChatOrchestrator> _logger;
    private readonly LlmSettings _llmSettings;

    public ChatOrchestrator(
        IChatSessionStore sessionStore,
        ILlmClientFactory llmClientFactory,
        IToolExecutor toolExecutor,
        ISystemPromptProvider systemPromptProvider,
        ILogger<ChatOrchestrator> logger,
        IOptions<LlmSettings> llmSettings)
    {
        _sessionStore = sessionStore;
        _llmClientFactory = llmClientFactory;
        _toolExecutor = toolExecutor;
        _systemPromptProvider = systemPromptProvider;
        _logger = logger;
        _llmSettings = llmSettings.Value;
    }

    public async Task<ChatResponse> ProcessMessageAsync(string sessionId, string userMessage, CancellationToken ct = default)
    {
        var session = await _sessionStore.GetAsync(sessionId, ct)
            ?? await _sessionStore.CreateAsync(sessionId, ct);

        var userMsg = new ChatMessage("user", userMessage);
        session = session.AddMessage(userMsg);
        await _sessionStore.UpdateAsync(session, ct);

        var systemPrompt = _systemPromptProvider.GetSystemPrompt();
        var toolPrompt = _systemPromptProvider.GetToolSystemPrompt();
        var messages = BuildMessages(systemPrompt, toolPrompt, session.Messages);

        var tools = _toolExecutor.GetToolDefinitions();
        var request = new LlmRequest(messages, tools, _llmSettings.MaxToolIterations);

        LlmResponse response;
        var attemptedExceptions = new List<Exception>();
        string? activeProvider = null;
        ILlmClient? activeClient = null;

        // Try primary provider
        try
        {
            activeProvider = _llmSettings.Provider;
            activeClient = _llmClientFactory.CreateClient(activeProvider);
            response = await ExecuteWithTimeoutAsync(
                () => activeClient.CompleteAsync(request, ct),
                TimeSpan.FromSeconds(_llmSettings.RequestTimeoutSeconds),
                "LLM completion",
                ct
            );
        }
        catch (Exception ex)
        {
            if (!IsTransientFailure(ex))
            {
                _logger.LogWarning(ex, "Provedor primário {Provider} falhou com erro não transitório, sem fallback", _llmSettings.Provider);
                throw new LlmFallbackException(new[] { ex });
            }

            _logger.LogWarning(ex, "Provedor primário {Provider} falhou com erro transitório, tentando fallback", _llmSettings.Provider);
            attemptedExceptions.Add(ex);

            // Try fallback provider
            var fallbackProvider = _llmSettings.Provider == "Nvidia" ? "OpenAI" : "Nvidia";
            var fallbackSettings = _llmSettings.GetProviderSettings(fallbackProvider);
            
            if (fallbackSettings == null || !fallbackSettings.Enabled)
            {
                _logger.LogWarning("Provedor de fallback {Provider} está desabilitado", fallbackProvider);
                throw new LlmFallbackException(attemptedExceptions);
            }

            try
            {
                activeProvider = fallbackProvider;
                activeClient = _llmClientFactory.CreateClient(activeProvider);
                response = await ExecuteWithTimeoutAsync(
                    () => activeClient.CompleteAsync(request, ct),
                    TimeSpan.FromSeconds(_llmSettings.RequestTimeoutSeconds),
                    "LLM fallback completion",
                    ct
                );
                _logger.LogInformation("Fallback para {Provider} bem-sucedido", fallbackProvider);
            }
            catch (Exception fallbackEx)
            {
                if (!IsTransientFailure(fallbackEx))
                {
                    attemptedExceptions.Add(fallbackEx);
                    _logger.LogError(fallbackEx, "Fallback para {Provider} também falhou com erro não transitório", fallbackProvider);
                    throw new LlmFallbackException(attemptedExceptions);
                }
                
                attemptedExceptions.Add(fallbackEx);
                _logger.LogError(fallbackEx, "Fallback para {Provider} também falhou", fallbackProvider);
                throw new LlmFallbackException(attemptedExceptions);
            }
        }

        // Process tool calls iteratively up to MaxToolIterations
        return await ProcessToolCallsIterativelyAsync(
            activeProvider!, 
            activeClient!, 
            session, 
            systemPrompt, 
            toolPrompt, 
            tools, 
            response, 
            ct);
    }

    private async Task<ChatResponse> ProcessToolCallsIterativelyAsync(
        string activeProvider,
        ILlmClient activeClient,
        ChatSession session,
        string systemPrompt,
        string toolPrompt,
        IReadOnlyList<ToolDefinition> tools,
        LlmResponse response,
        CancellationToken ct)
    {
        var allToolResults = new List<ToolResult>();
        var iteration = 0;
        var maxIterations = _llmSettings.MaxToolIterations;
        LlmResponse currentResponse = response;

        while (currentResponse.ToolCalls.Count > 0 && iteration < maxIterations)
        {
            iteration++;
            _logger.LogInformation("Iniciando iteração {Iteration} de tool calling", iteration);

            var toolResults = new List<ToolResult>();

            foreach (var toolCall in currentResponse.ToolCalls)
            {
                try
                {
                    var result = await ExecuteWithTimeoutAsync(
                        () => _toolExecutor.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct),
                        TimeSpan.FromSeconds(_llmSettings.RequestTimeoutSeconds),
                        $"Tool {toolCall.Name}",
                        ct
                    );
                    toolResults.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao executar tool {ToolName}", toolCall.Name);
                    toolResults.Add(new ToolResult(
                        toolCall.Id,
                        toolCall.Name,
                        string.Empty,
                        false,
                        ex.Message
                    ));
                }
            }

            allToolResults.AddRange(toolResults);

            var toolMessages = toolResults.Select(r => new ChatMessage(
                "tool",
                r.Success ? r.Result : $"Erro: {r.Error}",
                DateTime.UtcNow,
                null,
                r.ToolCallId
            )).ToList();

            foreach (var tm in toolMessages)
            {
                session = session.AddMessage(tm);
            }
            await _sessionStore.UpdateAsync(session, ct);

            var messages = BuildMessages(
                _systemPromptProvider.GetSystemPrompt(), 
                _systemPromptProvider.GetToolSystemPrompt(), 
                session.Messages);
            var nextRequest = new LlmRequest(messages, tools, _llmSettings.MaxToolIterations);

            try
            {
                currentResponse = await ExecuteWithTimeoutAsync(
                    () => _llmClientFactory.CreateClient(activeProvider).CompleteAsync(nextRequest, ct),
                    TimeSpan.FromSeconds(_llmSettings.RequestTimeoutSeconds),
                    $"LLM iteration {iteration + 1}",
                    ct
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na resposta do LLM na iteração {Iteration}", iteration + 1);
                return new ChatResponse(
                    "Ocorreu um erro ao processar a resposta. Tente novamente.",
                    allToolResults,
                    session.Id
                );
            }
        }

        // Final response after all tool iterations (or if no tool calls from start)
        var finalMessage = currentResponse.Message;
        session = session.AddMessage(finalMessage);
        await _sessionStore.UpdateAsync(session, ct);

        return new ChatResponse(
            finalMessage.Content,
            allToolResults,
            session.Id
        );
    }

    public async IAsyncEnumerable<ChatStreamChunk> ProcessMessageStreamAsync(string sessionId, string userMessage, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var session = await _sessionStore.GetAsync(sessionId, ct)
            ?? await _sessionStore.CreateAsync(sessionId, ct);

        var userMsg = new ChatMessage("user", userMessage);
        session = session.AddMessage(userMsg);
        await _sessionStore.UpdateAsync(session, ct);

        var systemPrompt = _systemPromptProvider.GetSystemPrompt();
        var toolPrompt = _systemPromptProvider.GetToolSystemPrompt();
        var messages = BuildMessages(systemPrompt, toolPrompt, session.Messages);

        var llmClient = _llmClientFactory.CreateClient(_llmSettings.Provider);
        var tools = _toolExecutor.GetToolDefinitions();

        var request = new LlmRequest(messages, tools, _llmSettings.MaxToolIterations);

        if (!llmClient.SupportsStreaming)
        {
            var response = await ProcessMessageAsync(sessionId, userMessage, ct);
            yield return new ChatStreamChunk(response.Message, true, response.ToolResults, response.SessionId);
            yield break;
        }

        var streamTask = llmClient.StreamAsync(request, ct);
        var stream = await streamTask;
        var accumulatedContent = new System.Text.StringBuilder();
        var accumulatedToolCalls = new List<ToolCall>();

        await foreach (var chunk in stream.WithCancellation<LlmStreamChunk>(ct))
        {
            if (chunk.ContentDelta != null)
            {
                accumulatedContent.Append(chunk.ContentDelta);
            }

            if (chunk.ToolCallsDelta != null && chunk.ToolCallsDelta.Count > 0)
            {
                accumulatedToolCalls.AddRange(chunk.ToolCallsDelta);
            }

            if (chunk.IsComplete)
            {
                break;
            }

            yield return new ChatStreamChunk(
                chunk.ContentDelta ?? string.Empty,
                false,
                [],
                sessionId
            );
        }

        var assistantMessage = new ChatMessage("assistant", accumulatedContent.ToString(), DateTime.UtcNow, accumulatedToolCalls);
        session = session.AddMessage(assistantMessage);
        await _sessionStore.UpdateAsync(session, ct);

        if (accumulatedToolCalls.Count > 0)
        {
            var toolResults = new List<ToolResult>();

            foreach (var toolCall in accumulatedToolCalls)
            {
                try
                {
                    var result = await _toolExecutor.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct);
                    toolResults.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao executar tool {ToolName}", toolCall.Name);
                    toolResults.Add(new ToolResult(toolCall.Id, toolCall.Name, string.Empty, false, ex.Message));
                }
            }

            var toolMessages = toolResults.Select(r => new ChatMessage("tool", r.Success ? r.Result : $"Erro: {r.Error}", DateTime.UtcNow, null, r.ToolCallId)).ToList();
            foreach (var tm in toolMessages)
            {
                session = session.AddMessage(tm);
            }
            await _sessionStore.UpdateAsync(session, ct);

            var finalMessages = BuildMessages(systemPrompt, toolPrompt, session.Messages);
            var finalRequest = new LlmRequest(finalMessages, tools, _llmSettings.MaxToolIterations);

            ChatStreamChunk? finalChunk = null;

            try
            {
                var finalResponse = await llmClient.CompleteAsync(finalRequest, ct);
                var finalMessage = finalResponse.Message;
                session = session.AddMessage(finalMessage);
                await _sessionStore.UpdateAsync(session, ct);

                finalChunk = new ChatStreamChunk(finalMessage.Content, true, toolResults, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na resposta final do LLM após tools (stream)");
                finalChunk = new ChatStreamChunk("Erro ao processar resposta final.", true, toolResults, sessionId);
            }

            if (finalChunk != null)
            {
                yield return finalChunk;
            }
        }
        else
        {
            yield return new ChatStreamChunk(string.Empty, true, [], sessionId);
        }
    }

    private static IReadOnlyList<ChatMessage> BuildMessages(string systemPrompt, string toolPrompt, IReadOnlyList<ChatMessage> history)
    {
        var messages = new List<ChatMessage>
        {
            new("system", systemPrompt),
            new("system", toolPrompt)
        };
        messages.AddRange(history);
        return messages;
    }

    private static async Task<T> ExecuteWithTimeoutAsync<T>(Func<Task<T>> func, TimeSpan timeout, string operationName, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            return await func();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(operationName, timeout);
        }
    }

    private static bool IsTransientFailure(Exception ex)
    {
        if (ex is TimeoutException) return true;
        if (ex is HttpRequestException) return true;
        if (ex is TaskCanceledException) return true;
        if (ex is LlmProviderException providerEx)
        {
            var message = providerEx.Message;
            if (message.Contains("500") || message.Contains("502") || message.Contains("503") || message.Contains("504") || message.Contains("429"))
                return true;
            if (message.Contains("401") || message.Contains("400") || message.Contains("403") || message.Contains("404"))
                return false;
        }
        return true;
    }
}

public interface IChatOrchestrator
{
    Task<ChatResponse> ProcessMessageAsync(string sessionId, string userMessage, CancellationToken ct = default);
    IAsyncEnumerable<ChatStreamChunk> ProcessMessageStreamAsync(string sessionId, string userMessage, CancellationToken ct = default);
}

public record ChatResponse(
    string Message,
    IReadOnlyList<ToolResult> ToolResults,
    string SessionId
);

public record ChatStreamChunk(
    string ContentDelta,
    bool IsComplete,
    IReadOnlyList<ToolResult> ToolResults,
    string SessionId
);
