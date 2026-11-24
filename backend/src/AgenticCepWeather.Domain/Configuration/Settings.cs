namespace AgenticCepWeather.Domain.Configuration;

public record LlmSettings
{
    public required string Provider { get; init; } // "Nvidia" ou "OpenAI"
    public required NvidiaSettings Nvidia { get; init; }
    public required OpenAISettings OpenAI { get; init; }
    public int MaxToolIterations { get; init; } = 5;
    public int RequestTimeoutSeconds { get; init; } = 30;
    public int InitTimeoutSeconds { get; init; } = 30;
    public int StopTimeoutSeconds { get; init; } = 15;

    public ProviderSettings? GetProviderSettings(string provider)
    {
        return provider switch
        {
            "Nvidia" => Nvidia,
            "OpenAI" => OpenAI,
            _ => null
        };
    }
}

public abstract record ProviderSettings
{
    public required string ApiKey { get; init; }
    public required string BaseUrl { get; init; }
    public required string Model { get; init; }
    public bool EnableStreaming { get; init; } = true;
    public bool Enabled { get; init; } = true;
}

public record NvidiaSettings : ProviderSettings
{
    public NvidiaSettings()
    {
        BaseUrl = "https://integrate.api.nvidia.com/v1";
        Model = "nvidia/llama-3.1-nemotron-70b-instruct";
    }
}

public record OpenAISettings : ProviderSettings
{
    public OpenAISettings()
    {
        BaseUrl = "https://api.openai.com/v1";
        Model = "gpt-4o-mini";
    }
}

public record ExternalApiSettings
{
    public required ViaCepSettings ViaCep { get; init; }
    public required OpenMeteoSettings OpenMeteo { get; init; }
}

public record ViaCepSettings
{
    public required string BaseUrl { get; init; } = "https://viacep.com.br/ws";
    public int TimeoutSeconds { get; init; } = 10;
}

public record OpenMeteoSettings
{
    public required string GeocodingUrl { get; init; } = "https://geocoding-api.open-meteo.com/v1/search";
    public required string ForecastUrl { get; init; } = "https://api.open-meteo.com/v1/forecast";
    public int TimeoutSeconds { get; init; } = 10;
}

public record ServerSettings
{
    public required string Urls { get; init; } = "http://0.0.0.0:5000";
    public required CorsSettings Cors { get; init; }
}

public record CorsSettings
{
    public required string[] AllowedOrigins { get; init; }
    public required string[] AllowedMethods { get; init; } = ["GET", "POST", "OPTIONS"];
    public required string[] AllowedHeaders { get; init; } = ["Content-Type", "Authorization"];
    public bool AllowCredentials { get; init; } = true;
}
