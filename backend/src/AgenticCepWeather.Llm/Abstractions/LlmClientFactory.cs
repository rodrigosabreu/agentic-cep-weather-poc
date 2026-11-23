namespace AgenticCepWeather.Llm.Abstractions;

using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Domain.Models;
using AgenticCepWeather.Domain.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

public interface ILlmClientFactory
{
    ILlmClient CreateClient(string providerName);
    IReadOnlyList<string> GetAvailableProviders();
}

public class LlmClientFactory : ILlmClientFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LlmClientFactory> _logger;
    private readonly LlmSettings _settings;

    public LlmClientFactory(
        IServiceProvider serviceProvider,
        ILogger<LlmClientFactory> logger,
        IOptions<LlmSettings> settings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = settings.Value;
    }

    public ILlmClient CreateClient(string providerName)
    {
        return providerName switch
        {
            "Nvidia" => _serviceProvider.GetRequiredService<NvidiaLlmClient>(),
            "OpenAI" => _serviceProvider.GetRequiredService<OpenAILlmClient>(),
            _ => throw new ArgumentException($"Provedor LLM desconhecido: {providerName}")
        };
    }

    public IReadOnlyList<string> GetAvailableProviders() => ["Nvidia", "OpenAI"];
}