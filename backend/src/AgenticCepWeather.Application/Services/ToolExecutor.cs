namespace AgenticCepWeather.Application.Services;

using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class ToolExecutor : IToolExecutor
{
    private readonly ICepService _cepService;
    private readonly IWeatherService _weatherService;
    private readonly ILogger<ToolExecutor> _logger;
    private readonly IReadOnlyList<ToolDefinition> _toolDefinitions;

    public ToolExecutor(
        ICepService cepService,
        IWeatherService weatherService,
        ILogger<ToolExecutor> logger)
    {
        _cepService = cepService;
        _weatherService = weatherService;
        _logger = logger;

        _toolDefinitions =
        [
            new ToolDefinition(
                ToolNames.ConsultarCep,
                "Consulta informações de endereço a partir de um CEP brasileiro. Retorna logradouro, bairro, cidade, estado e outros dados.",
                new JsonSchema(
                    "object",
                    new Dictionary<string, JsonSchemaProperty>
                    {
                        ["cep"] = new JsonSchemaProperty("string", "CEP no formato 00000-000 ou 00000000")
                    },
                    ["cep"]
                )
            ),
            new ToolDefinition(
                ToolNames.ConsultarPrevisaoTempo,
                "Consulta a previsão do tempo atual para uma cidade e estado brasileiro. Retorna temperatura, umidade, vento e condições.",
                new JsonSchema(
                    "object",
                    new Dictionary<string, JsonSchemaProperty>
                    {
                        ["cidade"] = new JsonSchemaProperty("string", "Nome da cidade"),
                        ["uf"] = new JsonSchemaProperty("string", "Sigla do estado (ex: SP, RJ, MG)")
                    },
                    ["cidade", "uf"]
                )
            )
        ];
    }

    public async Task<ToolResult> ExecuteAsync(string toolName, string arguments, CancellationToken ct = default)
    {
        _logger.LogInformation("Executando tool: {ToolName} com args: {Arguments}", toolName, arguments);

        try
        {
            return toolName switch
            {
                ToolNames.ConsultarCep => await ExecuteCepAsync(arguments, ct),
                ToolNames.ConsultarPrevisaoTempo => await ExecuteWeatherAsync(arguments, ct),
                _ => new ToolResult(Guid.NewGuid().ToString(), toolName, string.Empty, false, $"Tool desconhecida: {toolName}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar tool {ToolName}", toolName);
            return new ToolResult(Guid.NewGuid().ToString(), toolName, string.Empty, false, ex.Message);
        }
    }

    private async Task<ToolResult> ExecuteCepAsync(string arguments, CancellationToken ct)
    {
        var input = JsonSerializer.Deserialize<CepInput>(arguments);
        if (input == null || string.IsNullOrWhiteSpace(input.Cep))
        {
            return new ToolResult(Guid.NewGuid().ToString(), ToolNames.ConsultarCep, string.Empty, false, "CEP é obrigatório");
        }

        var result = await _cepService.ConsultarCepAsync(input.Cep, ct);
        if (result == null)
        {
            return new ToolResult(Guid.NewGuid().ToString(), ToolNames.ConsultarCep, string.Empty, false, "CEP não encontrado");
        }

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new ToolResult(Guid.NewGuid().ToString(), ToolNames.ConsultarCep, json, true);
    }

    private async Task<ToolResult> ExecuteWeatherAsync(string arguments, CancellationToken ct)
    {
        var input = JsonSerializer.Deserialize<WeatherInput>(arguments);
        if (input == null || string.IsNullOrWhiteSpace(input.Cidade) || string.IsNullOrWhiteSpace(input.Uf))
        {
            return new ToolResult(Guid.NewGuid().ToString(), ToolNames.ConsultarPrevisaoTempo, string.Empty, false, "Cidade e UF são obrigatórios");
        }

        var coords = await _weatherService.GeocodeAsync(input.Cidade, input.Uf, ct);
        if (coords == null)
        {
            return new ToolResult(Guid.NewGuid().ToString(), ToolNames.ConsultarPrevisaoTempo, string.Empty, false, $"Não foi possível encontrar coordenadas para {input.Cidade}/{input.Uf}");
        }

        var weather = await _weatherService.GetForecastAsync(coords.Latitude, coords.Longitude, ct);
        if (weather == null)
        {
            return new ToolResult(Guid.NewGuid().ToString(), ToolNames.ConsultarPrevisaoTempo, string.Empty, false, "Não foi possível obter a previsão do tempo");
        }

        var json = JsonSerializer.Serialize(weather, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new ToolResult(Guid.NewGuid().ToString(), ToolNames.ConsultarPrevisaoTempo, json, true);
    }

    public IReadOnlyList<ToolDefinition> GetToolDefinitions() => _toolDefinitions;
}