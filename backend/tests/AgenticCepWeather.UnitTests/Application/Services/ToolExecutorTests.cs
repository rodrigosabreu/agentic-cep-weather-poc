using AgenticCepWeather.Application.Services;
using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace AgenticCepWeather.UnitTests.Application.Services;

public class ToolExecutorTests
{
    private readonly Mock<ICepService> _cepServiceMock;
    private readonly Mock<IWeatherService> _weatherServiceMock;
    private readonly Mock<ILogger<ToolExecutor>> _loggerMock;
    private readonly ToolExecutor _executor;

    public ToolExecutorTests()
    {
        _cepServiceMock = new Mock<ICepService>();
        _weatherServiceMock = new Mock<IWeatherService>();
        _loggerMock = new Mock<ILogger<ToolExecutor>>();
        _executor = new ToolExecutor(_cepServiceMock.Object, _weatherServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ConsultarCepArgsValidos_ExecutaViaCepService()
    {
        // Arrange
        var cep = "01310-100";
        var arguments = JsonSerializer.Serialize(new CepInput(cep));
        var expectedResult = new CepResult(cep, "Avenida Paulista", "lado ímpar", "Bela Vista", "São Paulo", "SP", "3550308", "1004", "11", "7107");
        _cepServiceMock.Setup(s => s.ConsultarCepAsync(cep, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _executor.ExecuteAsync(ToolNames.ConsultarCep, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Name.Should().Be(ToolNames.ConsultarCep);
        
        var cepResult = JsonSerializer.Deserialize<CepResult>(result.Result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        cepResult.Should().NotBeNull();
        cepResult!.Logradouro.Should().Be("Avenida Paulista");
        cepResult.Bairro.Should().Be("Bela Vista");
        cepResult.Localidade.Should().Be("São Paulo");
        cepResult.Uf.Should().Be("SP");
        
        _cepServiceMock.Verify(s => s.ConsultarCepAsync(cep, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ConsultarCepArgsInvalidosCepMalformado_RetornaErro()
    {
        // Arrange
        var arguments = JsonSerializer.Serialize(new CepInput("abc"));
        // Service returns null for invalid CEP format
        _cepServiceMock.Setup(s => s.ConsultarCepAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CepResult?)null);

        // Act
        var result = await _executor.ExecuteAsync(ToolNames.ConsultarCep, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("CEP");
        _cepServiceMock.Verify(s => s.ConsultarCepAsync("abc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ConsultarCepArgsVazio_RetornaErro()
    {
        // Arrange
        var arguments = JsonSerializer.Serialize(new CepInput(""));
        _cepServiceMock.Setup(s => s.ConsultarCepAsync("", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CepResult?)null);

        // Act
        var result = await _executor.ExecuteAsync(ToolNames.ConsultarCep, arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("CEP é obrigatório");
    }

    [Fact]
    public async Task ExecuteAsync_ConsultarCepCepNaoEncontrado_RetornaErro()
    {
        // Arrange
        var cep = "99999-999";
        var arguments = JsonSerializer.Serialize(new CepInput(cep));
        _cepServiceMock.Setup(s => s.ConsultarCepAsync(cep, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CepResult?)null);

        // Act
        var result = await _executor.ExecuteAsync(ToolNames.ConsultarCep, arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("CEP não encontrado");
    }

    [Fact]
    public async Task ExecuteAsync_ConsultarPrevisaoTempoArgsValidos_ExecutaOpenMeteoService()
    {
        // Arrange
        var cidade = "São Paulo";
        var uf = "SP";
        var arguments = JsonSerializer.Serialize(new WeatherInput(cidade, uf));
        var coords = new Coordinates(-23.5505, -46.6333);
        var weather = new WeatherResult("São Paulo", "SP", 24.5, 18.0, 28.0, "Céu limpo", 65, 12.3, DateTime.UtcNow);

        _weatherServiceMock.Setup(s => s.GeocodeAsync(cidade, uf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coords);
        _weatherServiceMock.Setup(s => s.GetForecastAsync(coords.Latitude, coords.Longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync(weather);

        // Act
        var result = await _executor.ExecuteAsync(ToolNames.ConsultarPrevisaoTempo, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Name.Should().Be(ToolNames.ConsultarPrevisaoTempo);
        
        var weatherResult = JsonSerializer.Deserialize<WeatherResult>(result.Result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        weatherResult.Should().NotBeNull();
        weatherResult!.Cidade.Should().Be("São Paulo");
        weatherResult.TemperaturaAtual.Should().Be(24.5);
        weatherResult.Descricao.Should().Be("Céu limpo");
        
        _weatherServiceMock.Verify(s => s.GeocodeAsync(cidade, uf, It.IsAny<CancellationToken>()), Times.Once);
        _weatherServiceMock.Verify(s => s.GetForecastAsync(coords.Latitude, coords.Longitude, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ConsultarPrevisaoTempoSemCidadeUf_RetornaErro()
    {
        // Arrange
        var arguments = JsonSerializer.Serialize(new WeatherInput("", ""));

        // Act
        var result = await _executor.ExecuteAsync(ToolNames.ConsultarPrevisaoTempo, arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Cidade e UF são obrigatórios");
    }

    [Fact]
    public async Task ExecuteAsync_ConsultarPrevisaoTempoSemLatLon_RetornaErro()
    {
        // Arrange
        var arguments = JsonSerializer.Serialize(new WeatherInput("São Paulo", "SP"));
        _weatherServiceMock.Setup(s => s.GeocodeAsync("São Paulo", "SP", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Coordinates?)null);

        // Act
        var result = await _executor.ExecuteAsync(ToolNames.ConsultarPrevisaoTempo, arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Não foi possível encontrar coordenadas");
    }

    [Fact]
    public async Task ExecuteAsync_ConsultarPrevisaoTempoWeatherNulo_RetornaErro()
    {
        // Arrange
        var arguments = JsonSerializer.Serialize(new WeatherInput("São Paulo", "SP"));
        var coords = new Coordinates(-23.5505, -46.6333);
        _weatherServiceMock.Setup(s => s.GeocodeAsync("São Paulo", "SP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coords);
        _weatherServiceMock.Setup(s => s.GetForecastAsync(coords.Latitude, coords.Longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeatherResult?)null);

        // Act
        var result = await _executor.ExecuteAsync(ToolNames.ConsultarPrevisaoTempo, arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Não foi possível obter a previsão do tempo");
    }

    [Fact]
    public async Task ExecuteAsync_ToolNaoRegistrada_RetornaErro()
    {
        // Arrange
        var arguments = "{}";

        // Act
        var result = await _executor.ExecuteAsync("tool_inexistente", arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Tool desconhecida");
    }

    [Fact]
    public void GetToolDefinitions_RetornaDuasTools()
    {
        // Act
        var tools = _executor.GetToolDefinitions();

        // Assert
        tools.Should().HaveCount(2);
        tools.Should().Contain(t => t.Name == ToolNames.ConsultarCep);
        tools.Should().Contain(t => t.Name == ToolNames.ConsultarPrevisaoTempo);
    }

    [Fact]
    public void GetToolDefinitions_ConsultarCepSchemaCorreto()
    {
        // Act
        var tools = _executor.GetToolDefinitions();
        var cepTool = tools.First(t => t.Name == ToolNames.ConsultarCep);

        // Assert
        cepTool.Description.Should().Contain("CEP brasileiro");
        cepTool.Parameters.Type.Should().Be("object");
        cepTool.Parameters.Properties.Should().ContainKey("cep");
        cepTool.Parameters.Required.Should().Contain("cep");
    }

    [Fact]
    public void GetToolDefinitions_ConsultarPrevisaoTempoSchemaCorreto()
    {
        // Act
        var tools = _executor.GetToolDefinitions();
        var weatherTool = tools.First(t => t.Name == ToolNames.ConsultarPrevisaoTempo);

        // Assert
        weatherTool.Description.Should().Contain("previsão do tempo");
        weatherTool.Parameters.Type.Should().Be("object");
        weatherTool.Parameters.Properties.Should().ContainKey("cidade");
        weatherTool.Parameters.Properties.Should().ContainKey("uf");
        weatherTool.Parameters.Required.Should().Contain("cidade");
        weatherTool.Parameters.Required.Should().Contain("uf");
    }

    [Fact]
    public async Task ExecuteAsync_ExcecaoNoServico_RetornaErroSemStackTrace()
    {
        // Arrange
        var cep = "01310-100";
        var arguments = JsonSerializer.Serialize(new CepInput(cep));
        _cepServiceMock.Setup(s => s.ConsultarCepAsync(cep, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _executor.ExecuteAsync(ToolNames.ConsultarCep, arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotContain("StackTrace");
        result.Error.Should().NotContain("at AgenticCepWeather");
    }
}
