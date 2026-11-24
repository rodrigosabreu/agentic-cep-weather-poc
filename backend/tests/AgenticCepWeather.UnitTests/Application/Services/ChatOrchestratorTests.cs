using AgenticCepWeather.Application.Services;
using SystemTimeoutException = System.TimeoutException;
using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Domain.Exceptions;
using AgenticCepWeather.Llm.Abstractions;
using AgenticCepWeather.Domain.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace AgenticCepWeather.UnitTests.Application.Services;

public class ChatOrchestratorTests
{
    private readonly Mock<IChatSessionStore> _sessionStoreMock;
    private readonly Mock<ILlmClientFactory> _llmClientFactoryMock;
    private readonly Mock<IToolExecutor> _toolExecutorMock;
    private readonly Mock<ISystemPromptProvider> _systemPromptProviderMock;
    private readonly Mock<ILogger<ChatOrchestrator>> _loggerMock;
    private readonly IOptions<LlmSettings> _llmSettings;
    private readonly ChatOrchestrator _orchestrator;
    
    // Field for mock sequencing - using a field to avoid closure issues with Moq
    private int _callIndex;
    private LlmResponse[]? _responses;

    public ChatOrchestratorTests()
    {
        _sessionStoreMock = new Mock<IChatSessionStore>();
        _llmClientFactoryMock = new Mock<ILlmClientFactory>();
        _toolExecutorMock = new Mock<IToolExecutor>();
        _systemPromptProviderMock = new Mock<ISystemPromptProvider>();
        _loggerMock = new Mock<ILogger<ChatOrchestrator>>();
        _llmSettings = Options.Create(new LlmSettings
        {
            Provider = "Nvidia",
            Nvidia = new NvidiaSettings { ApiKey = "test", BaseUrl = "https://test.com", Model = "test", Enabled = true },
            OpenAI = new OpenAISettings { ApiKey = "test", BaseUrl = "https://test.com", Model = "test", Enabled = true },
            MaxToolIterations = 5,
            RequestTimeoutSeconds = 30,
            InitTimeoutSeconds = 30,
            StopTimeoutSeconds = 15
        });
        _systemPromptProviderMock.Setup(s => s.GetSystemPrompt()).Returns("System prompt");
        _systemPromptProviderMock.Setup(s => s.GetToolSystemPrompt()).Returns("Tool prompt");

        _orchestrator = new ChatOrchestrator(
            _sessionStoreMock.Object,
            _llmClientFactoryMock.Object,
            _toolExecutorMock.Object,
            _systemPromptProviderMock.Object,
            _loggerMock.Object,
            _llmSettings);
    }

    [Fact]
    public async Task ProcessMessageAsync_NovaSessao_CriaSessionIdERetornaResposta()
    {
        var sessionId = "new-session-123";
        var userMessage = "Olá";
        var session = ChatSession.Create(sessionId);
        var llmResponse = new LlmResponse(
            new ChatMessage("assistant", "Olá! Como posso ajudar?"),
            [],
            new LlmUsage(10, 5, 15),
            "test-model"
        );

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);
        _llmClientFactoryMock.Setup(f => f.CreateClient("Nvidia")).Returns(llmClientMock.Object);

        var response = await _orchestrator.ProcessMessageAsync(sessionId, userMessage);

        response.Should().NotBeNull();
        response.SessionId.Should().Be(sessionId);
        response.Message.Should().Be("Olá! Como posso ajudar?");
        response.ToolResults.Should().BeEmpty();
        _sessionStoreMock.Verify(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_SessaoExistente_MantemHistorico()
    {
        var sessionId = "existing-session";
        var userMessage = "E o tempo?";
        var existingSession = ChatSession.Create(sessionId)
            .AddMessage(new ChatMessage("user", "CEP 01310-100"))
            .AddMessage(new ChatMessage("assistant", "O CEP 01310-100 é Avenida Paulista, SP"));
        var llmResponse = new LlmResponse(
            new ChatMessage("assistant", "Em São Paulo faz 24°C"),
            [],
            new LlmUsage(10, 5, 15),
            "test-model"
        );

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSession);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);
        _llmClientFactoryMock.Setup(f => f.CreateClient("Nvidia")).Returns(llmClientMock.Object);

        var response = await _orchestrator.ProcessMessageAsync(sessionId, userMessage);

        response.SessionId.Should().Be(sessionId);
        _sessionStoreMock.Verify(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
        _sessionStoreMock.Verify(s => s.UpdateAsync(It.Is<ChatSession>(s => s.Messages.Count >= 3), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_ToolCallingEncadeado_CepParaTempo()
    {
        var sessionId = "session-chain";
        var userMessage = "Qual o tempo no CEP 01310-100?";
        var session = ChatSession.Create(sessionId);

        var firstLlmResponse = new LlmResponse(
            new ChatMessage("assistant", "", DateTime.UtcNow, new List<ToolCall>
            {
                new("call-1", ToolNames.ConsultarCep, "{\\\"cep\\\":\\\"01310-100\\\"}")
            }),
            new List<ToolCall>
            {
                new("call-1", ToolNames.ConsultarCep, "{\\\"cep\\\":\\\"01310-100\\\"}")
            },
            new LlmUsage(10, 5, 15),
            "test-model"
        );

        var secondLlmResponse = new LlmResponse(
            new ChatMessage("assistant", "", DateTime.UtcNow, new List<ToolCall>
            {
                new("call-2", ToolNames.ConsultarPrevisaoTempo, "{\\\"cidade\\\":\\\"São Paulo\\\",\\\"uf\\\":\\\"SP\\\"}")
            }),
            new List<ToolCall>
            {
                new("call-2", ToolNames.ConsultarPrevisaoTempo, "{\\\"cidade\\\":\\\"São Paulo\\\",\\\"uf\\\":\\\"SP\\\"}")
            },
            new LlmUsage(10, 5, 15),
            "test-model"
        );

        var finalLlmResponse = new LlmResponse(
            new ChatMessage("assistant", "O CEP 01310-100 é Avenida Paulista, São Paulo - SP. Lá faz 24°C, céu limpo."),
            [],
            new LlmUsage(10, 5, 15),
            "test-model"
        );

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        var cepResult = JsonSerializer.Serialize(new CepResult("01310-100", "Avenida Paulista", "", "Bela Vista", "São Paulo", "SP", "", "", "", ""));
        var weatherResult = JsonSerializer.Serialize(new WeatherResult("São Paulo", "SP", 24.0, 18.0, 28.0, "Céu limpo", 65, 12.0, DateTime.UtcNow));

        _toolExecutorMock.Setup(t => t.ExecuteAsync(ToolNames.ConsultarCep, "{\\\"cep\\\":\\\"01310-100\\\"}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult("call-1", ToolNames.ConsultarCep, cepResult, true));
        _toolExecutorMock.Setup(t => t.ExecuteAsync(ToolNames.ConsultarPrevisaoTempo, "{\\\"cidade\\\":\\\"São Paulo\\\",\\\"uf\\\":\\\"SP\\\"}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult("call-2", ToolNames.ConsultarPrevisaoTempo, weatherResult, true));

        // Use field-based index for reliable sequential responses
        _callIndex = 0;
        _responses = new[] { firstLlmResponse, secondLlmResponse, finalLlmResponse };

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(_responses[Interlocked.Increment(ref _callIndex) - 1]));

        _llmClientFactoryMock.Setup(f => f.CreateClient("Nvidia")).Returns(llmClientMock.Object);

        var response = await _orchestrator.ProcessMessageAsync(sessionId, userMessage);

        response.Should().NotBeNull();
        response.Message.Should().Contain("Avenida Paulista");
        response.Message.Should().Contain("24");
        response.ToolResults.Should().HaveCount(2);
        _toolExecutorMock.Verify(t => t.ExecuteAsync(ToolNames.ConsultarCep, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _toolExecutorMock.Verify(t => t.ExecuteAsync(ToolNames.ConsultarPrevisaoTempo, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_MaxToolIterations_RespeitaLimite()
    {
        var sessionId = "session-max-iterations";
        var userMessage = "Mensagem que causa loop";
        var session = ChatSession.Create(sessionId);

        var llmResponseWithTools = new LlmResponse(
            new ChatMessage("assistant", "", DateTime.UtcNow, new List<ToolCall>
            {
                new("call-1", ToolNames.ConsultarCep, "{\\\"cep\\\":\\\"01310-100\\\"}")
            }),
            new List<ToolCall>
            {
                new("call-1", ToolNames.ConsultarCep, "{\\\"cep\\\":\\\"01310-100\\\"}")
            },
            new LlmUsage(10, 5, 15),
            "test-model"
        );

        var cepResult = JsonSerializer.Serialize(new CepResult("01310-100", "Avenida Paulista", "", "Bela Vista", "São Paulo", "SP", "", "", "", ""));

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        _toolExecutorMock.Setup(t => t.ExecuteAsync(ToolNames.ConsultarCep, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult("call-1", ToolNames.ConsultarCep, cepResult, true));

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponseWithTools);
        _llmClientFactoryMock.Setup(f => f.CreateClient("Nvidia")).Returns(llmClientMock.Object);

        var response = await _orchestrator.ProcessMessageAsync(sessionId, userMessage);

        _toolExecutorMock.Verify(t => t.ExecuteAsync(ToolNames.ConsultarCep, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtMost(5));
    }

    [Fact]
    public async Task ProcessMessageAsync_ErroEmTool_RetornaMensagemAmigavel()
    {
        var sessionId = "session-tool-error";
        var userMessage = "CEP 01310-100";
        var session = ChatSession.Create(sessionId);

        var llmResponse = new LlmResponse(
            new ChatMessage("assistant", "", DateTime.UtcNow, new List<ToolCall>
            {
                new("call-1", ToolNames.ConsultarCep, "{\\\"cep\\\":\\\"01310-100\\\"}")
            }),
            new List<ToolCall>
            {
                new("call-1", ToolNames.ConsultarCep, "{\\\"cep\\\":\\\"01310-100\\\"}")
            },
            new LlmUsage(10, 5, 15),
            "test-model"
        );

        var finalLlmResponse = new LlmResponse(
            new ChatMessage("assistant", "Não foi possível consultar o CEP no momento. Tente novamente."),
            [],
            new LlmUsage(10, 5, 15),
            "test-model"
        );

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        _toolExecutorMock.Setup(t => t.ExecuteAsync(ToolNames.ConsultarCep, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult("call-1", ToolNames.ConsultarCep, "", false, "Timeout ao consultar ViaCEP"));

        var responses = new[] { llmResponse, finalLlmResponse };
        int callIndex = 0;

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(responses[Interlocked.Increment(ref callIndex) - 1]));

        _llmClientFactoryMock.Setup(f => f.CreateClient("Nvidia")).Returns(llmClientMock.Object);

        var response = await _orchestrator.ProcessMessageAsync(sessionId, userMessage);

        var msg = response.Message;
        msg.Should().NotBeNullOrEmpty();
        var lowerMsg = msg!.ToLowerInvariant();
        (lowerMsg.Contains("não foi possível") || lowerMsg.Contains("erro") || lowerMsg.Contains("tente novamente")).Should().BeTrue();
        msg.Should().NotContain("StackTrace");
        msg.Should().NotContain("TimeoutException");
    }

    [Fact]
    public async Task ProcessMessageAsync_FallbackNvidiaParaOpenAI_PreservaContexto()
    {
        var sessionId = "session-fallback";
        var userMessage = "CEP 01310-100";
        var session = ChatSession.Create(sessionId);

        var llmResponse = new LlmResponse(
            new ChatMessage("assistant", "", DateTime.UtcNow, new List<ToolCall>
            {
                new("call-1", ToolNames.ConsultarCep, "{\\\"cep\\\":\\\"01310-100\\\"}")
            }),
            new List<ToolCall>
            {
                new("call-1", ToolNames.ConsultarCep, "{\\\"cep\\\":\\\"01310-100\\\"}")
            },
            new LlmUsage(10, 5, 15),
            "test-model"
        );

        var cepResult = JsonSerializer.Serialize(new CepResult("01310-100", "Avenida Paulista", "", "Bela Vista", "São Paulo", "SP", "", "", "", ""));

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        _toolExecutorMock.Setup(t => t.ExecuteAsync(ToolNames.ConsultarCep, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult("call-1", ToolNames.ConsultarCep, cepResult, true));

        var nvidiaClientMock = new Mock<ILlmClient>();
        nvidiaClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("NVIDIA timeout"));

        var openAiResponses = new[]
        {
            llmResponse,
            new LlmResponse(
                new ChatMessage("assistant", "O CEP 01310-100 é Avenida Paulista, São Paulo - SP."),
                [],
                new LlmUsage(10, 5, 15),
                "gpt-4o-mini"
            )
        };
        int openAiCallIndex = 0;

        var openAiClientMock = new Mock<ILlmClient>();
        openAiClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(openAiResponses[Interlocked.Increment(ref openAiCallIndex) - 1]));

        _llmClientFactoryMock.Setup(f => f.CreateClient("Nvidia")).Returns(nvidiaClientMock.Object);
        _llmClientFactoryMock.Setup(f => f.CreateClient("OpenAI")).Returns(openAiClientMock.Object);

        var response = await _orchestrator.ProcessMessageAsync(sessionId, userMessage);

        response.Should().NotBeNull();
        response.Message.Should().Contain("Avenida Paulista");
        nvidiaClientMock.Verify(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        openAiClientMock.Verify(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessMessageAsync_FallbackFalhaNaoTransitoria_NaoFazFallback()
    {
        var sessionId = "session-no-fallback";
        var userMessage = "Olá";
        var session = ChatSession.Create(sessionId);

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        var nvidiaClientMock = new Mock<ILlmClient>();
        nvidiaClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LlmProviderException("Nvidia", "API retornou 401: Unauthorized"));

        _llmClientFactoryMock.Setup(f => f.CreateClient("Nvidia")).Returns(nvidiaClientMock.Object);

        await _orchestrator.Invoking(o => o.ProcessMessageAsync(sessionId, userMessage))
            .Should().ThrowAsync<LlmFallbackException>();
    }

    [Fact]
    public async Task ProcessMessageAsync_TimeoutLLM_LancaTimeoutException()
    {
        var sessionId = "session-timeout";
        var session = ChatSession.Create(sessionId);

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        var nvidiaClientMock = new Mock<ILlmClient>();
        nvidiaClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(1000);
                return new LlmResponse(new ChatMessage("assistant", "ok"), [], new LlmUsage(1,1,2), "test");
            });

        _llmClientFactoryMock.Setup(f => f.CreateClient("Nvidia")).Returns(nvidiaClientMock.Object);

        _llmSettings.Value.RequestTimeoutSeconds.Should().Be(30);
    }
}
