using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Llm.Abstractions;
using AgenticCepWeather.Application.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace AgenticCepWeather.IntegrationTests.Api;

public class ApiEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing registrations and add mocks
                var descriptorsToRemove = services.Where(d =>
                    d.ServiceType == typeof(IChatSessionStore) ||
                    d.ServiceType == typeof(ICepService) ||
                    d.ServiceType == typeof(IWeatherService) ||
                    d.ServiceType == typeof(IToolExecutor) ||
                    d.ServiceType == typeof(IChatOrchestrator) ||
                    d.ServiceType == typeof(ILlmClientFactory)).ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                // Add mocks
                var sessionStoreMock = new Mock<IChatSessionStore>();
                var session = AgenticCepWeather.Domain.Models.ChatSession.Create("test-session");
                sessionStoreMock.Setup(s => s.GetAsync("test-session", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(session);
                sessionStoreMock.Setup(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string id, CancellationToken _) => AgenticCepWeather.Domain.Models.ChatSession.Create(id));
                sessionStoreMock.Setup(s => s.UpdateAsync(It.IsAny<AgenticCepWeather.Domain.Models.ChatSession>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult<AgenticCepWeather.Domain.Models.ChatSession>(null!));
                sessionStoreMock.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                var cepServiceMock = new Mock<ICepService>();
                cepServiceMock.Setup(s => s.ConsultarCepAsync("01310-100", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AgenticCepWeather.Domain.Models.CepResult("01310-100", "Avenida Paulista", "lado ímpar", "Bela Vista", "São Paulo", "SP", "3550308", "1004", "11", "7107"));

                var weatherServiceMock = new Mock<IWeatherService>();
                weatherServiceMock.Setup(s => s.GeocodeAsync("São Paulo", "SP", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AgenticCepWeather.Domain.Models.Coordinates(-23.5505, -46.6333));
                weatherServiceMock.Setup(s => s.GetForecastAsync(-23.5505, -46.6333, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AgenticCepWeather.Domain.Models.WeatherResult("São Paulo", "SP", 24.5, 18.0, 28.0, "Céu limpo", 65, 12.3, DateTime.UtcNow));

                var toolExecutorMock = new Mock<IToolExecutor>();
                toolExecutorMock.Setup(t => t.ExecuteAsync(AgenticCepWeather.Domain.Models.ToolNames.ConsultarCep, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AgenticCepWeather.Domain.Models.ToolResult("call-1", AgenticCepWeather.Domain.Models.ToolNames.ConsultarCep, JsonSerializer.Serialize(new AgenticCepWeather.Domain.Models.CepResult("01310-100", "Avenida Paulista", "", "Bela Vista", "São Paulo", "SP", "", "", "", "")), true));
                toolExecutorMock.Setup(t => t.ExecuteAsync(AgenticCepWeather.Domain.Models.ToolNames.ConsultarPrevisaoTempo, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AgenticCepWeather.Domain.Models.ToolResult("call-2", AgenticCepWeather.Domain.Models.ToolNames.ConsultarPrevisaoTempo, JsonSerializer.Serialize(new AgenticCepWeather.Domain.Models.WeatherResult("São Paulo", "SP", 24.5, 18.0, 28.0, "Céu limpo", 65, 12.3, DateTime.UtcNow)), true));
                toolExecutorMock.Setup(t => t.GetToolDefinitions())
                    .Returns(new List<AgenticCepWeather.Domain.Models.ToolDefinition>
                    {
                        new(AgenticCepWeather.Domain.Models.ToolNames.ConsultarCep, "Consulta CEP", new AgenticCepWeather.Domain.Models.JsonSchema("object", new Dictionary<string, AgenticCepWeather.Domain.Models.JsonSchemaProperty>
                        {
                            ["cep"] = new AgenticCepWeather.Domain.Models.JsonSchemaProperty("string", "CEP")
                        }, ["cep"])),
                        new(AgenticCepWeather.Domain.Models.ToolNames.ConsultarPrevisaoTempo, "Consulta tempo", new AgenticCepWeather.Domain.Models.JsonSchema("object", new Dictionary<string, AgenticCepWeather.Domain.Models.JsonSchemaProperty>
                        {
                            ["cidade"] = new AgenticCepWeather.Domain.Models.JsonSchemaProperty("string", "Cidade"),
                            ["uf"] = new AgenticCepWeather.Domain.Models.JsonSchemaProperty("string", "UF")
                        }, ["cidade", "uf"]))
                    });

                var orchestratorMock = new Mock<AgenticCepWeather.Application.Services.IChatOrchestrator>();
                orchestratorMock.Setup(o => o.ProcessMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AgenticCepWeather.Application.Services.ChatResponse("Resposta do agente", new List<AgenticCepWeather.Domain.Models.ToolResult>(), "test-session"));
                orchestratorMock.Setup(o => o.ProcessMessageStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(GetTestStreamAsync());

                var llmClientFactoryMock = new Mock<AgenticCepWeather.Llm.Abstractions.ILlmClientFactory>();

                services.AddSingleton(sessionStoreMock.Object);
                services.AddSingleton(cepServiceMock.Object);
                services.AddSingleton(weatherServiceMock.Object);
                services.AddSingleton(toolExecutorMock.Object);
                services.AddSingleton(orchestratorMock.Object);
                services.AddSingleton(llmClientFactoryMock.Object);
            });
        });

        _client = _factory.CreateClient();
    }

    private async IAsyncEnumerable<AgenticCepWeather.Application.Services.ChatStreamChunk> GetTestStreamAsync()
    {
        yield return new AgenticCepWeather.Application.Services.ChatStreamChunk("Resposta ", false, [], "test-session");
        yield return new AgenticCepWeather.Application.Services.ChatStreamChunk("do agente", true, [], "test-session");
    }

    [Fact]
    public async Task PostApiChat_MessageValido_Retorna200ComSessionIdEResponse()
    {
        // Arrange
        var request = new { SessionId = "test-session", Message = "Olá" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("sessionId").GetString().Should().Be("test-session");
        content.GetProperty("message").GetString().Should().Be("Resposta do agente");
        content.GetProperty("toolResults").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task PostApiChat_SessionIdExistente_Retorna200ComMesmoSessionId()
    {
        // Arrange
        var request = new { SessionId = "test-session", Message = "Como vai?" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("sessionId").GetString().Should().Be("test-session");
    }

    [Fact]
    public async Task PostApiChat_MessageVazio_Retorna400()
    {
        // Arrange
        var request = new { SessionId = "test-session", Message = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostApiChat_SessionIdVazio_Retorna400()
    {
        // Arrange
        var request = new { SessionId = "", Message = "Olá" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetApiHealth_Retorna200Healthy()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("status").GetString().Should().Be("healthy");
        content.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetApiTools_RetornaListaDeTools()
    {
        // Act
        var response = await _client.GetAsync("/api/tools");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task PostApiChatStream_RetornaEventStream()
    {
        // Arrange
        var request = new { SessionId = "test-session", Message = "Olá" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat/stream", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
    }

    [Fact]
    public async Task CorsHeaders_Presentes()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/chat");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }
}

public class ApiEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetApiHealth_RealEndpoint_Retorna200()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}