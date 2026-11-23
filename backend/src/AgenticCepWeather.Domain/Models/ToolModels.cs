namespace AgenticCepWeather.Domain.Models;

public record CepInput(string Cep);

public record CepResult(
    string Cep,
    string Logradouro,
    string Complemento,
    string Bairro,
    string Localidade,
    string Uf,
    string Ibge,
    string Gia,
    string Ddd,
    string Siafi
);

public record Coordinates(double Latitude, double Longitude);

public record WeatherInput(string Cidade, string Uf);

public record WeatherResult(
    string Cidade,
    string Uf,
    double TemperaturaAtual,
    double TemperaturaMinima,
    double TemperaturaMaxima,
    string Descricao,
    int Umidade,
    double VelocidadeVento,
    DateTime PrevisaoPara
);

public record ToolDefinition(
    string Name,
    string Description,
    JsonSchema Parameters
);

public record JsonSchema(
    string Type = "object",
    IReadOnlyDictionary<string, JsonSchemaProperty>? Properties = null,
    IReadOnlyList<string>? Required = null
);

public record JsonSchemaProperty(
    string Type,
    string? Description = null,
    IReadOnlyList<string>? Enum = null
);