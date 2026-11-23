namespace AgenticCepWeather.Infrastructure.ExternalApis;

using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Domain.Exceptions;
using AgenticCepWeather.Domain.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class OpenMeteoService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoService> _logger;
    private readonly OpenMeteoSettings _settings;

    public OpenMeteoService(HttpClient httpClient, IOptions<ExternalApiSettings> settings, ILogger<OpenMeteoService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value.OpenMeteo;
    }

    public async Task<Coordinates?> GeocodeAsync(string cidade, string uf, CancellationToken ct = default)
    {
        var query = $"{cidade},{uf},BR";
        var url = $"{_settings.GeocodingUrl}?name={Uri.EscapeDataString(query)}&count=1&language=pt&format=json";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("AgenticCepWeather/1.0");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        try
        {
            var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Open-Meteo Geocoding retornou status {StatusCode} para {Cidade}/{Uf}", response.StatusCode, cidade, uf);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            var geoResponse = JsonSerializer.Deserialize<GeoResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var result = geoResponse?.Results?.FirstOrDefault();
            if (result == null)
            {
                _logger.LogInformation("Nenhuma coordenada encontrada para {Cidade}/{Uf}", cidade, uf);
                return null;
            }

            return new Coordinates(result.Latitude, result.Longitude);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogError("Timeout ao geocodificar {Cidade}/{Uf}", cidade, uf);
            throw new TimeoutException("Open-Meteo Geocoding", TimeSpan.FromSeconds(_settings.TimeoutSeconds));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de rede ao geocodificar {Cidade}/{Uf}", cidade, uf);
            throw new ExternalApiException("Open-Meteo Geocoding", "Erro de conexão com a API", null, ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Erro ao deserializar resposta do Open-Meteo Geocoding para {Cidade}/{Uf}", cidade, uf);
            throw new ExternalApiException("Open-Meteo Geocoding", "Resposta inválida da API", null, ex);
        }
    }

    public async Task<WeatherResult?> GetForecastAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var url = $"{_settings.ForecastUrl}?" +
                  $"latitude={latitude}&longitude={longitude}&" +
                  $"current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m&" +
                  $"daily=temperature_2m_max,temperature_2m_min,weather_code&" +
                  $"timezone=America/Sao_Paulo&forecast_days=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("AgenticCepWeather/1.0");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        try
        {
            var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Open-Meteo Forecast retornou status {StatusCode} para lat={Lat}, lon={Lon}", response.StatusCode, latitude, longitude);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            var forecastResponse = JsonSerializer.Deserialize<ForecastResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (forecastResponse?.Current == null || forecastResponse.Daily == null)
            {
                _logger.LogWarning("Resposta do Open-Meteo Forecast incompleta");
                return null;
            }

            var current = forecastResponse.Current;
            var daily = forecastResponse.Daily;

            var weatherCode = current.WeatherCode;
            var description = GetWeatherDescription(weatherCode);

            return new WeatherResult(
                "Localidade", // Será preenchido pelo chamador se necessário
                "UF",
                current.Temperature2m,
                daily.Temperature2mMin.FirstOrDefault(),
                daily.Temperature2mMax.FirstOrDefault(),
                description,
                current.RelativeHumidity2m,
                current.WindSpeed10m,
                DateTime.TryParse(current.Time, out var dt) ? dt : DateTime.UtcNow
            );
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogError("Timeout ao obter previsão do tempo para lat={Lat}, lon={Lon}", latitude, longitude);
            throw new TimeoutException("Open-Meteo Forecast", TimeSpan.FromSeconds(_settings.TimeoutSeconds));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de rede ao obter previsão do tempo para lat={Lat}, lon={Lon}", latitude, longitude);
            throw new ExternalApiException("Open-Meteo Forecast", "Erro de conexão com a API", null, ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Erro ao deserializar resposta do Open-Meteo Forecast para lat={Lat}, lon={Lon}", latitude, longitude);
            throw new ExternalApiException("Open-Meteo Forecast", "Resposta inválida da API", null, ex);
        }
    }

    private static string GetWeatherDescription(int code) => code switch
    {
        0 => "Céu limpo",
        1 => "Principalmente limpo",
        2 => "Parcialmente nublado",
        3 => "Nublado",
        45 => "Nevoeiro",
        48 => "Nevoeiro com depósito de geada",
        51 => "Chuvisco leve",
        53 => "Chuvisco moderado",
        55 => "Chuvisco denso",
        56 => "Chuvisco gelado leve",
        57 => "Chuvisco gelado denso",
        61 => "Chuva leve",
        63 => "Chuva moderada",
        65 => "Chuva forte",
        66 => "Chuva gelada leve",
        67 => "Chuva gelada forte",
        71 => "Neve leve",
        73 => "Neve moderada",
        75 => "Neve forte",
        77 => "Grãos de neve",
        80 => "Pancadas de chuva leves",
        81 => "Pancadas de chuva moderadas",
        82 => "Pancadas de chuva violentas",
        85 => "Pancadas de neve leves",
        86 => "Pancadas de neve fortes",
        95 => "Tempestade",
        96 => "Tempestade com granizo leve",
        99 => "Tempestade com granizo forte",
        _ => "Desconhecido"
    };
}

internal record GeoResponse(IReadOnlyList<GeoResult>? Results);
internal record GeoResult(double Latitude, double Longitude, string Name, string CountryCode, string? Admin1);

internal record ForecastResponse(CurrentWeather Current, DailyWeather Daily);
internal record CurrentWeather(double Temperature2m, int RelativeHumidity2m, int WeatherCode, double WindSpeed10m, string Time);
internal record DailyWeather(IReadOnlyList<double> Temperature2mMax, IReadOnlyList<double> Temperature2mMin, IReadOnlyList<int> WeatherCode);