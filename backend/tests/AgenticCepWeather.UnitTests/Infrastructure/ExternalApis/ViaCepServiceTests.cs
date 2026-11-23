using AgenticCepWeather.Infrastructure.ExternalApis;
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

public class ViaCepServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<ViaCepService>> _loggerMock;
    private readonly IOptions<ExternalApiSettings> _settings;
    private readonly ViaCepService _service;

    public ViaCepServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<ViaCepService>>();
        _settings = Options.Create(new ExternalApiSettings
        {
            ViaCep = new ViaCepSettings { BaseUrl = "https://viacep.com.br/ws", TimeoutSeconds = 10 },
            OpenMeteo = new OpenMeteoSettings { GeocodingUrl = "https://geocoding-api.open-meteo.com/v1/search", ForecastUrl = "https://api.open-meteo.com/v1/forecast", TimeoutSeconds = 10 }
        });
        _service = new ViaCepService(_httpClient, _settings, _loggerMock.Object);
    }

    [Fact]
    public async Task ConsultarCepAsync_CepValidoComHifen_RetornaEnderecoCompleto()
    {
        // Arrange
        var cep = "01310-100";
        var responseJson = JsonSerializer.Serialize(new
        {
            cep = "01310-100",
            logradouro = "Avenida Paulista",
            complemento = "lado ímpar",
            bairro = "Bela Vista",
            localidade = "São Paulo",
            uf = "SP",
            ibge = "3550308",
            gia = "1004",
            ddd = "11",
            siafi = "7107",
            erro = false
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.ConsultarCepAsync(cep);

        // Assert
        result.Should().NotBeNull();
        result!.Cep.Should().Be("01310-100");
        result.Logradouro.Should().Be("Avenida Paulista");
        result.Bairro.Should().Be("Bela Vista");
        result.Localidade.Should().Be("São Paulo");
        result.Uf.Should().Be("SP");
    }

    [Fact]
    public async Task ConsultarCepAsync_CepValidoSemHifen_RetornaMesmoResultado()
    {
        // Arrange
        var cep = "01310100";
        var responseJson = JsonSerializer.Serialize(new
        {
            cep = "01310-100",
            logradouro = "Avenida Paulista",
            complemento = "lado ímpar",
            bairro = "Bela Vista",
            localidade = "São Paulo",
            uf = "SP",
            ibge = "3550308",
            gia = "1004",
            ddd = "11",
            siafi = "7107",
            erro = false
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.ConsultarCepAsync(cep);

        // Assert
        result.Should().NotBeNull();
        result!.Cep.Should().Be("01310-100");
        result.Logradouro.Should().Be("Avenida Paulista");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("abcdefgh")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ConsultarCepAsync_CepFormatoInvalido_RetornaNull(string cepInvalido)
    {
        // Act
        var result = await _service.ConsultarCepAsync(cepInvalido);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarCepAsync_CepInexistente_RetornaNull()
    {
        // Arrange
        var cep = "99999-999";
        var responseJson = JsonSerializer.Serialize(new { erro = true });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.ConsultarCepAsync(cep);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarCepAsync_Timeout_LancaTimeoutException()
    {
        // Arrange
        var cep = "01310-100";

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("The operation was canceled"))
            .Verifiable();

        // Act & Assert
        await _service.Invoking(s => s.ConsultarCepAsync(cep))
            .Should().ThrowAsync<TimeoutException>()
            .WithMessage("*Timeout na operação 'ViaCEP'*");
    }

    [Fact]
    public async Task ConsultarCepAsync_ErroRede_LancaExternalApiException()
    {
        // Arrange
        var cep = "01310-100";

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"))
            .Verifiable();

        // Act & Assert
        await _service.Invoking(s => s.ConsultarCepAsync(cep))
            .Should().ThrowAsync<ExternalApiException>()
            .Where(e => e.ApiName == "ViaCEP" && e.Message.Contains("Erro de conexão"));
    }

    [Fact]
    public async Task ConsultarCepAsync_RespostaInvalidaJson_LancaExternalApiException()
    {
        // Arrange
        var cep = "01310-100";
        SetupHttpResponse(HttpStatusCode.OK, "invalid json {");

        // Act & Assert
        await _service.Invoking(s => s.ConsultarCepAsync(cep))
            .Should().ThrowAsync<ExternalApiException>()
            .Where(e => e.ApiName == "ViaCEP" && e.Message.Contains("Resposta inválida"));
    }

    [Fact]
    public async Task ConsultarCepAsync_HttpErrorStatus_RetornaNull()
    {
        // Arrange
        var cep = "01310-100";
        SetupHttpResponse(HttpStatusCode.InternalServerError, "");

        // Act
        var result = await _service.ConsultarCepAsync(cep);

        // Assert
        result.Should().BeNull();
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