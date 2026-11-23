namespace AgenticCepWeather.Domain.Exceptions;

public class AgenticCepWeatherException : Exception
{
    public string ErrorCode { get; }
    public AgenticCepWeatherException(string message, string errorCode, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}

public class ConfigurationException : AgenticCepWeatherException
{
    public ConfigurationException(string message, Exception? inner = null)
        : base(message, "CONFIG_ERROR", inner) { }
}

public class LlmProviderException : AgenticCepWeatherException
{
    public string Provider { get; }
    public LlmProviderException(string provider, string message, Exception? inner = null)
        : base($"[{provider}] {message}", "LLM_PROVIDER_ERROR", inner)
    {
        Provider = provider;
    }
}

public class LlmFallbackException : AgenticCepWeatherException
{
    public IReadOnlyList<Exception> AttemptedExceptions { get; }
    public LlmFallbackException(IReadOnlyList<Exception> exceptions)
        : base($"Todos os provedores LLM falharam: {string.Join("; ", exceptions.Select(e => e.Message))}", "LLM_FALLBACK_EXHAUSTED")
    {
        AttemptedExceptions = exceptions;
    }
}

public class ToolExecutionException : AgenticCepWeatherException
{
    public string ToolName { get; }
    public ToolExecutionException(string toolName, string message, Exception? inner = null)
        : base($"[Tool: {toolName}] {message}", "TOOL_EXECUTION_ERROR", inner)
    {
        ToolName = toolName;
    }
}

public class ExternalApiException : AgenticCepWeatherException
{
    public string ApiName { get; }
    public int? StatusCode { get; }
    public ExternalApiException(string apiName, string message, int? statusCode = null, Exception? inner = null)
        : base($"[{apiName}] {message}", "EXTERNAL_API_ERROR", inner)
    {
        ApiName = apiName;
        StatusCode = statusCode;
    }
}

public class SessionNotFoundException : AgenticCepWeatherException
{
    public string SessionId { get; }
    public SessionNotFoundException(string sessionId)
        : base($"Sessão não encontrada: {sessionId}", "SESSION_NOT_FOUND")
    {
        SessionId = sessionId;
    }
}

public class RateLimitException : AgenticCepWeatherException
{
    public TimeSpan RetryAfter { get; }
    public RateLimitException(string message, TimeSpan retryAfter, Exception? inner = null)
        : base(message, "RATE_LIMIT", inner)
    {
        RetryAfter = retryAfter;
    }
}

public class TimeoutException : AgenticCepWeatherException
{
    public string Operation { get; }
    public TimeSpan Timeout { get; }
    public TimeoutException(string operation, TimeSpan timeout, Exception? inner = null)
        : base($"Timeout na operação '{operation}' após {timeout.TotalSeconds}s", "TIMEOUT", inner)
    {
        Operation = operation;
        Timeout = timeout;
    }
}