namespace AgenticCepWeather.Application.Services;

using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Domain.Exceptions;
using AgenticCepWeather.Llm.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgenticCepWeather.Domain.Configuration;
using System.Collections.Concurrent;
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

        var llmClient = _llmClientFactory.CreateClient(_llmSettings.Provider);
        var tools = _toolExecutor.GetToolDefinitions();

        var request = new LlmRequest(
            messages,
            tools,
            _llmSettings.MaxToolIterations
        );

        LlmResponse response;
        var attemptedExceptions = new List<Exception>();

        try
        {
            response = await ExecuteWithTimeoutAsync(
                () => llmClient.CompleteAsync(request, ct),
                TimeSpan.FromSeconds(_llmSettings.RequestTimeoutSeconds),
                "LLM completion",
                ct
            );
        }
        catch (Exception ex) when (ex is not AgenticCepWeatherException)
        {
            _logger.LogWarning(ex, "Provedor primário {Provider} falhou, tentando fallback", _llmSettings.Provider);
            attemptedExceptions.Add(ex);

            var fallbackProvider = _llmSettings.Provider == "Nvidia" ? "OpenAI" : "Nvidia";
            var fallbackClient = _llmClientFactory.CreateClient(fallbackProvider);

            try
            {
                response = await ExecuteWithTimeoutAsync(
                    () => fallbackClient.CompleteAsync(request, ct),
                    TimeSpan.FromSeconds(_llmSettings.RequestTimeoutSeconds),
                    "LLM fallback completion",
                    ct
                );
                _logger.LogInformation("Fallback para {Provider} bem-sucedido", fallbackProvider);
            }
            catch (Exception fallbackEx)
            {
                attemptedExceptions.Add(fallbackEx);
                _logger.LogError(fallbackEx, "Fallback para {Provider} também falhou", fallbackProvider);
                throw new LlmFallbackException(attemptedExceptions);
            }
        }

        var assistantMessage = response.Message;
        session = session.AddMessage(assistantMessage);
        await _sessionStore.UpdateAsync(session, ct);

        if (response.ToolCalls.Count > 0)
        {
            var toolResults = new List<ToolResult>();

            foreach (var toolCall in response.ToolCalls)
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

            var finalMessages = BuildMessages(systemPrompt, toolPrompt, session.Messages);
            var finalRequest = new LlmRequest(finalMessages, tools, _llmSettings.MaxToolIterations);

            try
            {
                var finalResponse = await ExecuteWithTimeoutAsync(
                    () => llmClient.CompleteAsync(finalRequest, ct),
                    TimeSpan.FromSeconds(_llmSettings.RequestTimeoutSeconds),
                    "LLM final completion",
                    ct
                );

                var finalMessage = finalResponse.Message;
                session = session.AddMessage(finalMessage);
                await _sessionStore.UpdateAsync(session, ct);

                return new ChatResponse(
                    finalMessage.Content,
                    toolResults,
                    session.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na resposta final do LLM após tools");
                return new ChatResponse(
                    "Ocorreu um erro ao processar a resposta final. Tente novamente.",
                    toolResults,
                    session.Id
                );
            }
        }

        return new ChatResponse(
            assistantMessage.Content,
            [],
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