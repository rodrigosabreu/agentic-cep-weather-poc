using AgenticCepWeather.Infrastructure.ExternalApis;
using CustomTimeoutException = AgenticCepWeather.Domain.Exceptions.TimeoutException;
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

namespace AgenticCepWeather.UnitTests.Infrastructure.ExternalApis;

public class OpenMeteoServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<OpenMeteoService>> _loggerMock;
    private readonly IOptions<ExternalApiSettings> _settings;
    private readonly OpenMeteoService _service;

    public OpenMeteoServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<OpenMeteoService>>();
        _settings = Options.Create(new ExternalApiSettings
        {
            ViaCep = new ViaCepSettings { BaseUrl = "https://viacep.com.br/ws", TimeoutSeconds = 10 },
            OpenMeteo = new OpenMeteoSettings { GeocodingUrl = "https://geocoding-api.open-meteo.com/v1/search", ForecastUrl = "https://api.open-meteo.com/v1/forecast", TimeoutSeconds = 10 }
        });
        _service = new OpenMeteoService(_httpClient, _settings, _loggerMock.Object);
    }

    [Fact]
    public async Task GeocodeAsync_CidadeValida_RetornaCoordenadas()
    {
        // Arrange
        var cidade = "São Paulo";
        var uf = "SP";
        var responseJson = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new
                {
                    latitude = -23.5505,
                    longitude = -46.6333,
                    name = "São Paulo",
                    country_code = "BR",
                    admin1 = "São Paulo"
                }
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.GeocodeAsync(cidade, uf);

        // Assert
        result.Should().NotBeNull();
        result!.Latitude.Should().BeApproximately(-23.5505, 0.001);
        result.Longitude.Should().BeApproximately(-46.6333, 0.001);
    }

    [Fact]
    public async Task GeocodeAsync_CidadeComUf_Desambiguacao()
    {
        // Arrange
        var cidade = "Rio de Janeiro";
        var uf = "RJ";
        var responseJson = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new
                {
                    latitude = -22.9068,
                    longitude = -43.1729,
                    name = "Rio de Janeiro",
                    country_code = "BR",
                    admin1 = "Rio de Janeiro"
                }
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.GeocodeAsync(cidade, uf);

        // Assert
        result.Should().NotBeNull();
        result!.Latitude.Should().BeApproximately(-22.9068, 0.001);
        result.Longitude.Should().BeApproximately(-43.1729, 0.001);
    }

    [Fact]
    public async Task GeocodeAsync_CidadeInexistente_RetornaNull()
    {
        // Arrange
        var cidade = "Xyzabc";
        var uf = "SP";
        var responseJson = JsonSerializer.Serialize(new { results = new object[0] });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.GeocodeAsync(cidade, uf);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GeocodeAsync_Timeout_LancaTimeoutException()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("The operation was canceled"))
            .Verifiable();

        // Act & Assert
        await _service.Invoking(s => s.GeocodeAsync("São Paulo", "SP"))
            .Should().ThrowAsync<CustomTimeoutException>()
            .WithMessage("*Timeout na operação 'Open-Meteo Geocoding'*");
    }

    [Fact]
    public async Task GeocodeAsync_ErroRede_LancaExternalApiException()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"))
            .Verifiable();

        // Act & Assert
        await _service.Invoking(s => s.GeocodeAsync("São Paulo", "SP"))
            .Should().ThrowAsync<ExternalApiException>()
            .Where(e => e.ApiName == "Open-Meteo Geocoding" && e.Message.Contains("Erro de conexão"));
    }

    [Fact]
    public async Task GetForecastAsync_CoordenadasValidas_RetornaPrevisaoCompleta()
    {
        // Arrange
        var latitude = -23.5505;
        var longitude = -46.6333;
        var responseJson = JsonSerializer.Serialize(new
        {
            current = new
            {
                temperature_2m = 24.5,
                relative_humidity_2m = 65,
                weather_code = 0,
                wind_speed_10m = 12.3,
                time = "2026-11-23T10:00"
            },
            daily = new
            {
                temperature_2m_max = new[] { 28.0 },
                temperature_2m_min = new[] { 18.0 },
                weather_code = new[] { 0 }
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.GetForecastAsync(latitude, longitude);

        // Assert
        result.Should().NotBeNull();
        result!.TemperaturaAtual.Should().Be(24.5);
        result.TemperaturaMaxima.Should().Be(28.0);
        result.TemperaturaMinima.Should().Be(18.0);
        result.Descricao.Should().Be("Céu limpo");
        result.Umidade.Should().Be(65);
        result.VelocidadeVento.Should().Be(12.3);
    }

    [Fact]
    public async Task GetForecastAsync_CoordenadasInvalidas_RetornaNull()
    {
        // Arrange
        var latitude = 100.0;
        var longitude = 200.0;
        SetupHttpResponse(HttpStatusCode.BadRequest, "");

        // Act
        var result = await _service.GetForecastAsync(latitude, longitude);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetForecastAsync_Timeout_LancaTimeoutException()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("The operation was canceled"))
            .Verifiable();

        // Act & Assert
        await _service.Invoking(s => s.GetForecastAsync(-23.55, -46.63))
            .Should().ThrowAsync<CustomTimeoutException>()
            .WithMessage("*Timeout na operação 'Open-Meteo Forecast'*");
    }

    [Fact]
    public async Task GetForecastAsync_ErroRede_LancaExternalApiException()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"))
            .Verifiable();

        // Act & Assert
        await _service.Invoking(s => s.GetForecastAsync(-23.55, -46.63))
            .Should().ThrowAsync<ExternalApiException>()
            .Where(e => e.ApiName == "Open-Meteo Forecast" && e.Message.Contains("Erro de conexão"));
    }

    [Theory]
    [InlineData(0, "Céu limpo")]
    [InlineData(1, "Principalmente limpo")]
    [InlineData(2, "Parcialmente nublado")]
    [InlineData(3, "Nublado")]
    [InlineData(45, "Nevoeiro")]
    [InlineData(48, "Nevoeiro com depósito de geada")]
    [InlineData(51, "Chuvisco leve")]
    [InlineData(53, "Chuvisco moderado")]
    [InlineData(55, "Chuvisco denso")]
    [InlineData(56, "Chuvisco gelado leve")]
    [InlineData(57, "Chuvisco gelado denso")]
    [InlineData(61, "Chuva leve")]
    [InlineData(63, "Chuva moderada")]
    [InlineData(65, "Chuva forte")]
    [InlineData(66, "Chuva gelada leve")]
    [InlineData(67, "Chuva gelada forte")]
    [InlineData(71, "Neve leve")]
    [InlineData(73, "Neve moderada")]
    [InlineData(75, "Neve forte")]
    [InlineData(77, "Grãos de neve")]
    [InlineData(80, "Pancadas de chuva leves")]
    [InlineData(81, "Pancadas de chuva moderadas")]
    [InlineData(82, "Pancadas de chuva violentas")]
    [InlineData(85, "Pancadas de neve leves")]
    [InlineData(86, "Pancadas de neve fortes")]
    [InlineData(95, "Tempestade")]
    [InlineData(96, "Tempestade com granizo leve")]
    [InlineData(99, "Tempestade com granizo forte")]
    [InlineData(999, "Desconhecido")]
    public async Task GetForecastAsync_MapeamentoWeatherCode_RetornaDescricaoCorreta(int weatherCode, string expectedDescription)
    {
        // Arrange - Use weather_code in CURRENT (which is what the service reads)
        var responseJson = JsonSerializer.Serialize(new
        {
            current = new
            {
                temperature_2m = 20.0,
                relative_humidity_2m = 50,
                weather_code = weatherCode,
                wind_speed_10m = 10.0,
                time = "2026-11-23T10:00"
            },
            daily = new
            {
                temperature_2m_max = new[] { 25.0 },
                temperature_2m_min = new[] { 15.0 },
                weather_code = new[] { weatherCode }
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.GetForecastAsync(-23.55, -46.63);

        // Assert
        result.Should().NotBeNull();
        result!.Descricao.Should().Be(expectedDescription);
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
