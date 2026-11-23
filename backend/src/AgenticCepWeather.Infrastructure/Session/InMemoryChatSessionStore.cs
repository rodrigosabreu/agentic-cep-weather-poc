namespace AgenticCepWeather.Infrastructure.Session;

using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

public class InMemoryChatSessionStore : IChatSessionStore
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<InMemoryChatSessionStore> _logger;
    private readonly TimeSpan _sessionTtl = TimeSpan.FromHours(24);
    private const string SessionPrefix = "chat_session_";

    public InMemoryChatSessionStore(IMemoryCache cache, ILogger<InMemoryChatSessionStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<ChatSession?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        var key = GetKey(sessionId);
        if (_cache.TryGetValue(key, out ChatSession? session))
        {
            return Task.FromResult<ChatSession?>(session);
        }
        return Task.FromResult<ChatSession?>(null);
    }

    public Task<ChatSession> CreateAsync(string sessionId, CancellationToken ct = default)
    {
        var session = ChatSession.Create(sessionId);
        var key = GetKey(sessionId);
        _cache.Set(key, session, _sessionTtl);
        _logger.LogDebug("Sessão criada: {SessionId}", sessionId);
        return Task.FromResult(session);
    }

    public Task<ChatSession> UpdateAsync(ChatSession session, CancellationToken ct = default)
    {
        var key = GetKey(session.Id);
        _cache.Set(key, session, _sessionTtl);
        _logger.LogDebug("Sessão atualizada: {SessionId}, Mensagens: {Count}", session.Id, session.Messages.Count);
        return Task.FromResult(session);
    }

    public Task<bool> DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        var key = GetKey(sessionId);
        _cache.Remove(key);
        _logger.LogDebug("Sessão removida: {SessionId}", sessionId);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<string>> ListSessionIdsAsync(CancellationToken ct = default)
    {
        // MemoryCache doesn't support enumeration of keys directly
        // This is a limitation - in production, use Redis or similar
        _logger.LogWarning("ListSessionIdsAsync não é totalmente suportado com MemoryCache");
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static string GetKey(string sessionId) => $"{SessionPrefix}{sessionId}";
}