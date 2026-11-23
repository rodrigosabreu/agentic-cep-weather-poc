using AgenticCepWeather.Infrastructure.Session;
using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace AgenticCepWeather.UnitTests.Infrastructure.Session;

public class InMemoryChatSessionStoreTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<InMemoryChatSessionStore>> _loggerMock;
    private readonly InMemoryChatSessionStore _store;

    public InMemoryChatSessionStoreTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<InMemoryChatSessionStore>>();
        _store = new InMemoryChatSessionStore(_memoryCache, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAsync_SessaoNaoExiste_RetornaNull()
    {
        // Act
        var result = await _store.GetAsync("sessao-inexistente");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_NovaSessao_CriaERetornaSessao()
    {
        // Arrange
        var sessionId = "nova-sessao-123";

        // Act
        var result = await _store.CreateAsync(sessionId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(sessionId);
        result.Messages.Should().BeEmpty();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAsync_SessaoExiste_RetornaSessao()
    {
        // Arrange
        var sessionId = "sessao-existente";
        await _store.CreateAsync(sessionId);

        // Act
        var result = await _store.GetAsync(sessionId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(sessionId);
    }

    [Fact]
    public async Task UpdateAsync_SessaoExistente_AtualizaMensagensETimestamp()
    {
        // Arrange
        var sessionId = "sessao-update";
        var session = await _store.CreateAsync(sessionId);
        var updatedSession = session.AddMessage(new ChatMessage("user", "Nova mensagem"));

        // Act
        await _store.UpdateAsync(updatedSession);
        var result = await _store.GetAsync(sessionId);

        // Assert
        result.Should().NotBeNull();
        result!.Messages.Should().HaveCount(1);
        result.Messages[0].Content.Should().Be("Nova mensagem");
        result.UpdatedAt.Should().BeAfter(session.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_SessaoExistente_RemoveERetornaTrue()
    {
        // Arrange
        var sessionId = "sessao-delete";
        await _store.CreateAsync(sessionId);

        // Act
        var result = await _store.DeleteAsync(sessionId);

        // Assert
        result.Should().BeTrue();
        var getResult = await _store.GetAsync(sessionId);
        getResult.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_SessaoInexistente_RetornaTrue()
    {
        // Act
        var result = await _store.DeleteAsync("sessao-inexistente");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ListSessionIdsAsync_RetornaListaVazia_MemoryCacheLimitation()
    {
        // Arrange
        await _store.CreateAsync("sessao-1");
        await _store.CreateAsync("sessao-2");

        // Act
        var result = await _store.ListSessionIdsAsync();

        // Assert
        // MemoryCache não suporta enumeração de chaves - retorna lista vazia
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SessaoExpiracaoTTL_AposTempoExpirado_SessaoRemovida()
    {
        // Arrange - Criar store com TTL curto para teste
        var shortTtlCache = new MemoryCache(new MemoryCacheOptions());
        var shortTtlStore = new InMemoryChatSessionStore(shortTtlCache, _loggerMock.Object);
        
        // Usar reflection para acessar o campo privado _sessionTtl
        var ttlField = typeof(InMemoryChatSessionStore).GetField("_sessionTtl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ttlField!.SetValue(shortTtlStore, TimeSpan.FromMilliseconds(100));

        var sessionId = "sessao-ttl";
        await shortTtlStore.CreateAsync(sessionId);

        // Verificar que existe imediatamente
        var immediate = await shortTtlStore.GetAsync(sessionId);
        immediate.Should().NotBeNull();

        // Aguardar expiração
        await Task.Delay(200);

        // Act
        var expired = await shortTtlStore.GetAsync(sessionId);

        // Assert
        expired.Should().BeNull();
    }

    [Fact]
    public async Task MultiplasSessoes_Independentes()
    {
        // Arrange
        await _store.CreateAsync("sessao-1");
        await _store.CreateAsync("sessao-2");

        var session1 = await _store.GetAsync("sessao-1");
        var session2 = await _store.GetAsync("sessao-2");

        session1 = session1!.AddMessage(new ChatMessage("user", "Msg sessao 1"));
        session2 = session2!.AddMessage(new ChatMessage("user", "Msg sessao 2"));

        await _store.UpdateAsync(session1);
        await _store.UpdateAsync(session2);

        // Act
        var result1 = await _store.GetAsync("sessao-1");
        var result2 = await _store.GetAsync("sessao-2");

        // Assert
        result1!.Messages.Should().HaveCount(1);
        result1.Messages[0].Content.Should().Be("Msg sessao 1");
        result2!.Messages.Should().HaveCount(1);
        result2.Messages[0].Content.Should().Be("Msg sessao 2");
    }
}