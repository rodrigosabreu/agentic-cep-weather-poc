using AgenticCepWeather.Llm.Providers;
using AgenticCepWeather.Llm.Abstractions;
using AgenticCepWeather.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Domain.Exceptions;
using AgenticCepWeather.Domain.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace AgenticCepWeather.UnitTests.Llm.Providers;

public class NvidiaLlmClientTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<NvidiaLlmClient>> _loggerMock;
    private readonly IOptions<LlmSettings> _settings;
    private readonly NvidiaLlmClient _client;

    public NvidiaLlmClientTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<NvidiaLlmClient>>();
        _settings = Options.Create(new LlmSettings
        {
            Provider = "Nvidia",
            Nvidia = new NvidiaSettings { ApiKey = "test-key", BaseUrl = "https://integrate.api.nvidia.com/v1", Model = "nvidia/llama-3.1-nemotron-70b-instruct", EnableStreaming = true, Enabled = true },
            OpenAI = new OpenAISettings { ApiKey = "test-key", BaseUrl = "https://api.openai.com/v1", Model = "gpt-4o-mini", EnableStreaming = true, Enabled = true },
            MaxToolIterations = 5,
            RequestTimeoutSeconds = 30
        });
        _client = new NvidiaLlmClient(_httpClient, _settings, _loggerMock.Object);
    }

    [Fact]
    public void ProviderName_RetornaNvidia()
    {
        _client.ProviderName.Should().Be("Nvidia");
    }

    [Fact]
    public void SupportsStreaming_RetornaTrue()
    {
        _client.SupportsStreaming.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_EnviaToolsNoPayload()
    {
        // Arrange
        var request = new LlmRequest(
            new List<ChatMessage> { new("user", "CEP 01310-100") },
            new List<ToolDefinition>
            {
                new(ToolNames.ConsultarCep, "Consulta CEP", new JsonSchema("object", new Dictionary<string, JsonSchemaProperty>
                {
                    ["cep"] = new JsonSchemaProperty("string", "CEP")
                }, ["cep"]))
            },
            5, 0.3, null);

        var responseJson = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-123",
            @object = "chat.completion",
            model = "nvidia/llama-3.1-nemotron-70b-instruct",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = "Vou consultar o CEP para você.",
                        tool_calls = Array.Empty<object>()
                    },
                    finish_reason = "stop"
                }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var response = await _client.CompleteAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Message.Should().NotBeNull();
        response.ToolCalls.Should().BeEmpty();
        response.Model.Should().Be("nvidia/llama-3.1-nemotron-70b-instruct");
        response.Usage.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteAsync_ProcessaToolCalls()
    {
        // Arrange
        var request = new LlmRequest(
            new List<ChatMessage> { new("user", "CEP 01310-100") },
            new List<ToolDefinition>
            {
                new(ToolNames.ConsultarCep, "Consulta CEP", new JsonSchema("object", new Dictionary<string, JsonSchemaProperty>
                {
                    ["cep"] = new JsonSchemaProperty("string", "CEP")
                }, ["cep"]))
            });

        var responseJson = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-123",
            @object = "chat.completion",
            model = "nvidia/llama-3.1-nemotron-70b-instruct",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = "",
                        tool_calls = new[]
                        {
                            new
                            {
                                id = "call-1",
                                type = "function",
                                function = new
                                {
                                    name = "consultar_cep",
                                    arguments = "{\\\"cep\\\":\\\"01310-100\\\"}"
                                }
                            }
                        }
                    },
                    finish_reason = "tool_calls"
                }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var response = await _client.CompleteAsync(request);

        // Assert
        response.ToolCalls.Should().HaveCount(1);
        response.ToolCalls[0].Name.Should().Be(ToolNames.ConsultarCep);
        response.ToolCalls[0].Arguments.Should().Contain("01310-100");
    }

    [Fact]
    public async Task CompleteAsync_ErroHttp_LancaLlmProviderException()
    {
        // Arrange
        var request = new LlmRequest(new List<ChatMessage> { new("user", "test") }, new List<ToolDefinition>());
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Internal Server Error");

        // Act & Assert
        await _client.Invoking(c => c.CompleteAsync(request))
            .Should().ThrowAsync<LlmProviderException>()
            .Where(e => e.Provider == "Nvidia" && (e.Message.Contains("500") || e.Message.Contains("InternalServerError")));
    }

    [Fact]
    public async Task CompleteAsync_ErroRede_LancaLlmProviderException()
    {
        // Arrange
        var request = new LlmRequest(new List<ChatMessage> { new("user", "test") }, new List<ToolDefinition>());
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        await _client.Invoking(c => c.CompleteAsync(request))
            .Should().ThrowAsync<LlmProviderException>()
            .Where(e => e.Provider == "Nvidia" && e.Message.Contains("Erro de conexão"));
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            })
            .Verifiable();
    }
}

public class OpenAILlmClientTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<OpenAILlmClient>> _loggerMock;
    private readonly IOptions<LlmSettings> _settings;
    private readonly OpenAILlmClient _client;

    public OpenAILlmClientTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<OpenAILlmClient>>();
        _settings = Options.Create(new LlmSettings
        {
            Provider = "OpenAI",
            Nvidia = new NvidiaSettings { ApiKey = "test-key", BaseUrl = "https://integrate.api.nvidia.com/v1", Model = "nvidia/llama-3.1-nemotron-70b-instruct", EnableStreaming = true, Enabled = true },
            OpenAI = new OpenAISettings { ApiKey = "test-key", BaseUrl = "https://api.openai.com/v1", Model = "gpt-4o-mini", EnableStreaming = true, Enabled = true },
            MaxToolIterations = 5,
            RequestTimeoutSeconds = 30
        });
        _client = new OpenAILlmClient(_httpClient, _settings, _loggerMock.Object);
    }

    [Fact]
    public void ProviderName_RetornaOpenAI()
    {
        _client.ProviderName.Should().Be("OpenAI");
    }

    [Fact]
    public void SupportsStreaming_RetornaTrue()
    {
        _client.SupportsStreaming.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_MesmoContratoNvidia()
    {
        // Arrange
        var request = new LlmRequest(
            new List<ChatMessage> { new("user", "test") },
            new List<ToolDefinition>());

        var responseJson = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-123",
            @object = "chat.completion",
            model = "gpt-4o-mini",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = "Olá!", tool_calls = Array.Empty<object>() },
                    finish_reason = "stop"
                }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var response = await _client.CompleteAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Message.Content.Should().Be("Olá!");
        response.Model.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task CompleteAsync_ProcessaToolCalls()
    {
        // Arrange
        var request = new LlmRequest(
            new List<ChatMessage> { new("user", "CEP 01310-100") },
            new List<ToolDefinition>
            {
                new(ToolNames.ConsultarCep, "Consulta CEP", new JsonSchema("object", new Dictionary<string, JsonSchemaProperty>
                {
                    ["cep"] = new JsonSchemaProperty("string", "CEP")
                }, ["cep"]))
            });

        var responseJson = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-123",
            @object = "chat.completion",
            model = "gpt-4o-mini",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = "",
                        tool_calls = new[]
                        {
                            new
                            {
                                id = "call-1",
                                type = "function",
                                function = new
                                {
                                    name = "consultar_cep",
                                    arguments = "{\\\"cep\\\":\\\"01310-100\\\"}"
                                }
                            }
                        }
                    },
                    finish_reason = "tool_calls"
                }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var response = await _client.CompleteAsync(request);

        // Assert
        response.ToolCalls.Should().HaveCount(1);
        response.ToolCalls[0].Name.Should().Be(ToolNames.ConsultarCep);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            })
            .Verifiable();
    }
}

public class LlmClientFactoryTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<LlmClientFactory>> _loggerMock;
    private readonly IOptions<LlmSettings> _settings;
    private readonly LlmClientFactory _factory;

    public LlmClientFactoryTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<LlmClientFactory>>();
        _settings = Options.Create(new LlmSettings
        {
            Provider = "Nvidia",
            Nvidia = new NvidiaSettings { ApiKey = "test", BaseUrl = "https://test.com", Model = "test", Enabled = true },
            OpenAI = new OpenAISettings { ApiKey = "test", BaseUrl = "https://test.com", Model = "test", Enabled = true }
        });
        _factory = new LlmClientFactory(_serviceProviderMock.Object, _loggerMock.Object, _settings);
    }

    [Fact]
    public void CreateClient_Nvidia_RetornaNvidiaLlmClient()
    {
        var nvidiaClient = new Mock<NvidiaLlmClient>(Mock.Of<HttpClient>(), _settings, Mock.Of<ILogger<NvidiaLlmClient>>());
        nvidiaClient.Setup(c => c.ProviderName).Returns("Nvidia");
        _serviceProviderMock.Setup(s => s.GetService(typeof(NvidiaLlmClient))).Returns(nvidiaClient.Object);

        var client = _factory.CreateClient("Nvidia");

        client.Should().NotBeNull();
        client.ProviderName.Should().Be("Nvidia");
    }

    [Fact]
    public void CreateClient_OpenAI_RetornaOpenAILlmClient()
    {
        var openAiClient = new Mock<OpenAILlmClient>(Mock.Of<HttpClient>(), _settings, Mock.Of<ILogger<OpenAILlmClient>>());
        openAiClient.Setup(c => c.ProviderName).Returns("OpenAI");
        _serviceProviderMock.Setup(s => s.GetService(typeof(OpenAILlmClient))).Returns(openAiClient.Object);

        var client = _factory.CreateClient("OpenAI");

        client.Should().NotBeNull();
        client.ProviderName.Should().Be("OpenAI");
    }

    [Fact]
    public void CreateClient_ProvedorDesconhecido_LancaArgumentException()
    {
        _factory.Invoking(f => f.CreateClient("Desconhecido"))
            .Should().Throw<ArgumentException>()
            .WithMessage("*Provedor LLM desconhecido*");
    }

    [Fact]
    public void GetAvailableProviders_RetornaListaCorreta()
    {
        var providers = _factory.GetAvailableProviders();

        providers.Should().HaveCount(2);
        providers.Should().Contain("Nvidia");
        providers.Should().Contain("OpenAI");
    }

    [Theory]
    [InlineData("Nvidia")]
    [InlineData("OpenAI")]
    public void CreateClient_CadaProvedor_RetornaInstanciaCorreta(string providerName)
    {
        if (providerName == "Nvidia")
        {
            var mock = new Mock<NvidiaLlmClient>(Mock.Of<HttpClient>(), _settings, Mock.Of<ILogger<NvidiaLlmClient>>());
            mock.Setup(c => c.ProviderName).Returns("Nvidia");
            _serviceProviderMock.Setup(s => s.GetService(typeof(NvidiaLlmClient))).Returns(mock.Object);
        }
        else
        {
            var mock = new Mock<OpenAILlmClient>(Mock.Of<HttpClient>(), _settings, Mock.Of<ILogger<OpenAILlmClient>>());
            mock.Setup(c => c.ProviderName).Returns("OpenAI");
            _serviceProviderMock.Setup(s => s.GetService(typeof(OpenAILlmClient))).Returns(mock.Object);
        }

        var client = _factory.CreateClient(providerName);

        client.Should().NotBeNull();
        client.ProviderName.Should().Be(providerName);
    }
}

public class LlmFallbackTests
{
    private readonly Mock<ILlmClient> _primaryClientMock;
    private readonly Mock<ILlmClient> _fallbackClientMock;
    private readonly Mock<ILlmClientFactory> _factoryMock;
    private readonly Mock<IChatSessionStore> _sessionStoreMock;
    private readonly Mock<IToolExecutor> _toolExecutorMock;
    private readonly Mock<ISystemPromptProvider> _systemPromptProviderMock;
    private readonly Mock<ILogger<ChatOrchestrator>> _loggerMock;
    private readonly IOptions<LlmSettings> _llmSettings;
    private readonly ChatOrchestrator _orchestrator;

    public LlmFallbackTests()
    {
        _primaryClientMock = new Mock<ILlmClient>();
        _fallbackClientMock = new Mock<ILlmClient>();
        _factoryMock = new Mock<ILlmClientFactory>();
        _sessionStoreMock = new Mock<IChatSessionStore>();
        _toolExecutorMock = new Mock<IToolExecutor>();
        _systemPromptProviderMock = new Mock<ISystemPromptProvider>();
        _loggerMock = new Mock<ILogger<ChatOrchestrator>>();
        _llmSettings = Options.Create(new LlmSettings
        {
            Provider = "Nvidia",
            Nvidia = new NvidiaSettings { ApiKey = "test", BaseUrl = "https://test.com", Model = "test", Enabled = true },
            OpenAI = new OpenAISettings { ApiKey = "test", BaseUrl = "https://test.com", Model = "test", Enabled = true },
            MaxToolIterations = 5,
            RequestTimeoutSeconds = 30
        });
        _systemPromptProviderMock.Setup(s => s.GetSystemPrompt()).Returns("System prompt");
        _systemPromptProviderMock.Setup(s => s.GetToolSystemPrompt()).Returns("Tool prompt");

        _factoryMock.Setup(f => f.CreateClient("Nvidia")).Returns(_primaryClientMock.Object);
        _factoryMock.Setup(f => f.CreateClient("OpenAI")).Returns(_fallbackClientMock.Object);

        _orchestrator = new ChatOrchestrator(
            _sessionStoreMock.Object,
            _factoryMock.Object,
            _toolExecutorMock.Object,
            _systemPromptProviderMock.Object,
            _loggerMock.Object,
            _llmSettings);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(System.TimeoutException))]
    [InlineData(typeof(LlmProviderException))]
    public async Task Fallback_FalhaTransitoriaNvidia_FazFallbackParaOpenAI(Type exceptionType)
    {
        // Arrange
        var sessionId = "session-fallback-transient";
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
        var finalLlmResponse = new LlmResponse(
            new ChatMessage("assistant", "O CEP 01310-100 é Avenida Paulista, São Paulo - SP."),
            [],
            new LlmUsage(10, 5, 15),
            "gpt-4o-mini"
        );

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        _toolExecutorMock.Setup(t => t.ExecuteAsync(ToolNames.ConsultarCep, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult("call-1", ToolNames.ConsultarCep, cepResult, true));

        Exception exception;
        if (exceptionType == typeof(LlmProviderException))
        {
            exception = new LlmProviderException("Nvidia", "API retornou 500: Internal Server Error");
        }
        else
        {
            exception = (Exception)Activator.CreateInstance(exceptionType, "Transient error")!;
        }

        _primaryClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var openAiCallCount = 0;
        _fallbackClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                openAiCallCount++;
                return openAiCallCount == 1 ? llmResponse : finalLlmResponse;
            });

        // Act
        var response = await _orchestrator.ProcessMessageAsync(sessionId, userMessage);

        // Assert
        response.Should().NotBeNull();
        response.Message.Should().Contain("Avenida Paulista");
        _primaryClientMock.Verify(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _fallbackClientMock.Verify(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Fallback_FalhaNaoTransitoria401_NaoFazFallback()
    {
        // Arrange
        var sessionId = "session-no-fallback-401";
        var userMessage = "Olá";
        var session = ChatSession.Create(sessionId);

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        _primaryClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LlmProviderException("Nvidia", "API retornou 401: Unauthorized"));

        // Act & Assert
        await _orchestrator.Invoking(o => o.ProcessMessageAsync(sessionId, userMessage))
            .Should().ThrowAsync<LlmFallbackException>();

        _fallbackClientMock.Verify(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Fallback_FalhaNaoTransitoria400_NaoFazFallback()
    {
        // Arrange
        var sessionId = "session-no-fallback-400";
        var userMessage = "Olá";
        var session = ChatSession.Create(sessionId);

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        _primaryClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LlmProviderException("Nvidia", "API retornou 400: Bad Request"));

        // Act & Assert
        await _orchestrator.Invoking(o => o.ProcessMessageAsync(sessionId, userMessage))
            .Should().ThrowAsync<LlmFallbackException>();

        _fallbackClientMock.Verify(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Fallback_AmbosFalharem_LancaLlmFallbackException()
    {
        // Arrange
        var sessionId = "session-both-fail";
        var userMessage = "Olá";
        var session = ChatSession.Create(sessionId);

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        _sessionStoreMock.Setup(s => s.CreateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        _primaryClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("NVIDIA timeout"));
        _fallbackClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("OpenAI timeout"));

        // Act & Assert
        await _orchestrator.Invoking(o => o.ProcessMessageAsync(sessionId, userMessage))
            .Should().ThrowAsync<LlmFallbackException>()
            .Where(e => e.AttemptedExceptions.Count == 2);
    }

    [Fact]
    public async Task Fallback_ContextoPreservadoNoFallback()
    {
        // Arrange
        var sessionId = "session-context-preserved";
        var userMessage = "CEP 01310-100";
        var session = ChatSession.Create(sessionId)
            .AddMessage(new ChatMessage("user", "Olá"))
            .AddMessage(new ChatMessage("assistant", "Olá! Como posso ajudar?"));

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
        var finalLlmResponse = new LlmResponse(
            new ChatMessage("assistant", "O CEP 01310-100 é Avenida Paulista, São Paulo - SP."),
            [],
            new LlmUsage(10, 5, 15),
            "gpt-4o-mini"
        );

        _sessionStoreMock.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        _toolExecutorMock.Setup(t => t.ExecuteAsync(ToolNames.ConsultarCep, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult("call-1", ToolNames.ConsultarCep, cepResult, true));

        _primaryClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("NVIDIA timeout"));

        var openAiCallCount = 0;
        _fallbackClientMock.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                openAiCallCount++;
                if (openAiCallCount == 1)
                {
                    return llmResponse;
                }
                return finalLlmResponse;
            });

        // Act
        var response = await _orchestrator.ProcessMessageAsync(sessionId, userMessage);

        // Assert
        response.Should().NotBeNull();
        _fallbackClientMock.Verify(c => c.CompleteAsync(
            It.Is<LlmRequest>(r => r.Messages.Count >= 4), // system + tool prompt + 2 mensagens históricas + user message
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
