namespace AgenticCepWeather.Infrastructure.ExternalApis;

using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Domain.Exceptions;
using AgenticCepWeather.Domain.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class ViaCepService : ICepService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ViaCepService> _logger;
    private readonly ViaCepSettings _settings;

    public ViaCepService(HttpClient httpClient, IOptions<ExternalApiSettings> settings, ILogger<ViaCepService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value.ViaCep;
    }

    public async Task<CepResult?> ConsultarCepAsync(string cep, CancellationToken ct = default)
    {
        var cleanCep = CleanCep(cep);
        if (string.IsNullOrEmpty(cleanCep) || cleanCep.Length != 8)
        {
            _logger.LogWarning("CEP inválido: {Cep}", cep);
            return null;
        }

        var url = $"{_settings.BaseUrl.TrimEnd('/')}/{cleanCep}/json/";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("AgenticCepWeather/1.0");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        try
        {
            var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ViaCEP retornou status {StatusCode} para CEP {Cep}", response.StatusCode, cep);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            var viaCepResponse = JsonSerializer.Deserialize<ViaCepResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (viaCepResponse == null || viaCepResponse.Erro == true)
            {
                _logger.LogInformation("CEP não encontrado no ViaCEP: {Cep}", cep);
                return null;
            }

            return new CepResult(
                viaCepResponse.Cep ?? cleanCep,
                viaCepResponse.Logradouro ?? string.Empty,
                viaCepResponse.Complemento ?? string.Empty,
                viaCepResponse.Bairro ?? string.Empty,
                viaCepResponse.Localidade ?? string.Empty,
                viaCepResponse.Uf ?? string.Empty,
                viaCepResponse.Ibge ?? string.Empty,
                viaCepResponse.Gia ?? string.Empty,
                viaCepResponse.Ddd ?? string.Empty,
                viaCepResponse.Siafi ?? string.Empty
            );
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogError("Timeout ao consultar ViaCEP para CEP {Cep}", cep);
            throw new TimeoutException("ViaCEP", TimeSpan.FromSeconds(_settings.TimeoutSeconds));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout from our linked token source (cancelled by CancelAfter)
            _logger.LogError("Timeout ao consultar ViaCEP para CEP {Cep}", cep);
            throw new TimeoutException("ViaCEP", TimeSpan.FromSeconds(_settings.TimeoutSeconds));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de rede ao consultar ViaCEP para CEP {Cep}", cep);
            throw new ExternalApiException("ViaCEP", "Erro de conexão com a API", null, ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Erro ao deserializar resposta do ViaCEP para CEP {Cep}", cep);
            throw new ExternalApiException("ViaCEP", "Resposta inválida da API", null, ex);
        }
    }

    private static string CleanCep(string cep) => new string(cep.Where(char.IsDigit).ToArray());
}

internal record ViaCepResponse(
    string? Cep,
    string? Logradouro,
    string? Complemento,
    string? Bairro,
    string? Localidade,
    string? Uf,
    string? Ibge,
    string? Gia,
    string? Ddd,
    string? Siafi,
    bool Erro
);
