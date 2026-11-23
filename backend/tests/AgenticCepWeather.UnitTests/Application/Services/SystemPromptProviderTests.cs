using AgenticCepWeather.Application.Services;
using AgenticCepWeather.Domain.Interfaces;
using Xunit;
using FluentAssertions;

namespace AgenticCepWeather.UnitTests.Application.Services;

public class SystemPromptProviderTests
{
    private readonly SystemPromptProvider _provider;

    public SystemPromptProviderTests()
    {
        _provider = new SystemPromptProvider();
    }

    [Fact]
    public void GetSystemPrompt_RetornaPromptNaoVazio()
    {
        // Act
        var prompt = _provider.GetSystemPrompt();

        // Assert
        prompt.Should().NotBeNullOrEmpty();
        prompt.Should().Contain("CEP");
        prompt.Should().Contain("previsão do tempo");
        prompt.Should().Contain("português brasileiro");
        prompt.Should().Contain("ferramentas");
    }

    [Fact]
    public void GetToolSystemPrompt_RetornaPromptComTools()
    {
        // Act
        var prompt = _provider.GetToolSystemPrompt();

        // Assert
        prompt.Should().NotBeNullOrEmpty();
        prompt.Should().Contain("consultar_cep");
        prompt.Should().Contain("consultar_previsao_tempo");
        prompt.Should().Contain("EXEMPLOS DE USO");
        prompt.Should().Contain("CEP 01310-100");
        prompt.Should().Contain("São Paulo/SP");
    }

    [Fact]
    public void GetSystemPrompt_ContemRegrasImportantes()
    {
        // Act
        var prompt = _provider.GetSystemPrompt();

        // Assert
        prompt.Should().Contain("Não invente dados");
        prompt.Should().Contain("use exclusivamente as ferramentas");
        prompt.Should().Contain("cidade E estado");
    }
}