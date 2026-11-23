using AgenticCepWeather.Domain.Models;
using Xunit;
using FluentAssertions;

namespace AgenticCepWeather.UnitTests.Domain.Models;

public class ChatModelsTests
{
    [Fact]
    public void ChatMessage_Construtor_PreenchTimestampAutomaticamente()
    {
        // Act
        var message = new ChatMessage("user", "Olá");

        // Assert
        message.Role.Should().Be("user");
        message.Content.Should().Be("Olá");
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        message.ToolCalls.Should().BeNull();
        message.ToolCallId.Should().BeNull();
    }

    [Fact]
    public void ChatMessage_ComToolCalls_PreenchCorretamente()
    {
        // Arrange
        var toolCalls = new List<ToolCall>
        {
            new("call-1", "consultar_cep", "{\"cep\":\"01310-100\"}")
        };

        // Act
        var message = new ChatMessage("assistant", "", DateTime.UtcNow, toolCalls, "call-1");

        // Assert
        message.ToolCalls.Should().HaveCount(1);
        #pragma warning disable CS8602
        message.ToolCalls[0].Name.Should().Be("consultar_cep");
        message.ToolCallId.Should().NotBeNull();
        var toolCallId = message.ToolCallId;
        toolCallId.Should().Be("call-1");
        #pragma warning restore CS8602
    }

    [Fact]
    public void ToolCall_Construtor_PreenchCorretamente()
    {
        // Act
        var toolCall = new ToolCall("call-1", "consultar_cep", "{\"cep\":\"01310-100\"}");

        // Assert
        toolCall.Id.Should().Be("call-1");
        toolCall.Name.Should().Be("consultar_cep");
        toolCall.Arguments.Should().Be("{\"cep\":\"01310-100\"}");
    }

    [Fact]
    public void ToolResult_Sucesso_PreenchCorretamente()
    {
        // Act
        var result = new ToolResult("call-1", "consultar_cep", "{\"cep\":\"01310-100\"}", true);

        // Assert
        result.ToolCallId.Should().Be("call-1");
        result.Name.Should().Be("consultar_cep");
        result.Result.Should().Be("{\"cep\":\"01310-100\"}");
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ToolResult_Erro_PreenchCorretamente()
    {
        // Act
        var result = new ToolResult("call-1", "consultar_cep", "", false, "CEP não encontrado");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("CEP não encontrado");
    }

    [Fact]
    public void ChatSession_Create_CriaSessaoVazia()
    {
        // Act
        var session = ChatSession.Create("session-123");

        // Assert
        session.Id.Should().Be("session-123");
        session.Messages.Should().BeEmpty();
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        session.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ChatSession_AddMessage_AdicionaMensagemEAtualizaTimestamp()
    {
        // Arrange
        var session = ChatSession.Create("session-123");
        var originalUpdatedAt = session.UpdatedAt;

        // Act
        var updated = session.AddMessage(new ChatMessage("user", "Nova mensagem"));

        // Assert
        updated.Messages.Should().HaveCount(1);
        updated.Messages[0].Content.Should().Be("Nova mensagem");
        updated.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        updated.Id.Should().Be(session.Id); // Mesmo ID
        updated.CreatedAt.Should().Be(session.CreatedAt); // CreatedAt não muda
    }

    [Fact]
    public void ChatSession_AddMessage_MultiplasMensagens()
    {
        // Arrange
        var session = ChatSession.Create("session-123");

        // Act
        var withMsg1 = session.AddMessage(new ChatMessage("user", "Msg 1"));
        var withMsg2 = withMsg1.AddMessage(new ChatMessage("assistant", "Resp 1"));
        var withMsg3 = withMsg2.AddMessage(new ChatMessage("user", "Msg 2"));

        // Assert
        withMsg3.Messages.Should().HaveCount(3);
        withMsg3.Messages[0].Content.Should().Be("Msg 1");
        withMsg3.Messages[1].Content.Should().Be("Resp 1");
        withMsg3.Messages[2].Content.Should().Be("Msg 2");
    }

    [Fact]
    public void ToolName_Enum_ContemValoresCorretos()
    {
        // Assert
        Enum.GetNames(typeof(ToolName)).Should().BeEquivalentTo(new[] { "ConsultarCep", "ConsultarPrevisaoTempo" });
    }

    [Fact]
    public void ToolNames_Constantes_Corretas()
    {
        // Assert
        ToolNames.ConsultarCep.Should().Be("consultar_cep");
        ToolNames.ConsultarPrevisaoTempo.Should().Be("consultar_previsao_tempo");
        ToolNames.All.Should().HaveCount(2);
        ToolNames.All.Should().Contain("consultar_cep");
        ToolNames.All.Should().Contain("consultar_previsao_tempo");
    }
}

public class ToolModelsTests
{
    [Fact]
    public void CepInput_Construtor_PreenchCorretamente()
    {
        // Act
        var input = new CepInput("01310-100");

        // Assert
        input.Cep.Should().Be("01310-100");
    }

    [Fact]
    public void CepResult_Construtor_PreenchTodosCampos()
    {
        // Act
        var result = new CepResult("01310-100", "Avenida Paulista", "lado ímpar", "Bela Vista", "São Paulo", "SP", "3550308", "1004", "11", "7107");

        // Assert
        result.Cep.Should().Be("01310-100");
        result.Logradouro.Should().Be("Avenida Paulista");
        result.Complemento.Should().Be("lado ímpar");
        result.Bairro.Should().Be("Bela Vista");
        result.Localidade.Should().Be("São Paulo");
        result.Uf.Should().Be("SP");
        result.Ibge.Should().Be("3550308");
        result.Gia.Should().Be("1004");
        result.Ddd.Should().Be("11");
        result.Siafi.Should().Be("7107");
    }

    [Fact]
    public void Coordinates_Construtor_PreenchCorretamente()
    {
        // Act
        var coords = new Coordinates(-23.5505, -46.6333);

        // Assert
        coords.Latitude.Should().Be(-23.5505);
        coords.Longitude.Should().Be(-46.6333);
    }

    [Fact]
    public void WeatherInput_Construtor_PreenchCorretamente()
    {
        // Act
        var input = new WeatherInput("São Paulo", "SP");

        // Assert
        input.Cidade.Should().Be("São Paulo");
        input.Uf.Should().Be("SP");
    }

    [Fact]
    public void WeatherResult_Construtor_PreenchTodosCampos()
    {
        // Arrange
        var time = DateTime.UtcNow;

        // Act
        var result = new WeatherResult("São Paulo", "SP", 24.5, 18.0, 28.0, "Céu limpo", 65, 12.3, time);

        // Assert
        result.Cidade.Should().Be("São Paulo");
        result.Uf.Should().Be("SP");
        result.TemperaturaAtual.Should().Be(24.5);
        result.TemperaturaMinima.Should().Be(18.0);
        result.TemperaturaMaxima.Should().Be(28.0);
        result.Descricao.Should().Be("Céu limpo");
        result.Umidade.Should().Be(65);
        result.VelocidadeVento.Should().Be(12.3);
        result.PrevisaoPara.Should().Be(time);
    }

    [Fact]
    public void ToolDefinition_Construtor_PreenchCorretamente()
    {
        // Arrange
        var properties = new Dictionary<string, JsonSchemaProperty>
        {
            ["cep"] = new JsonSchemaProperty("string", "CEP brasileiro")
        };

        // Act
        var tool = new ToolDefinition("consultar_cep", "Consulta CEP", new JsonSchema("object", properties, ["cep"]));

        // Assert
        tool.Name.Should().Be("consultar_cep");
        tool.Description.Should().Be("Consulta CEP");
        tool.Parameters.Type.Should().Be("object");
        tool.Parameters.Properties.Should().ContainKey("cep");
        tool.Parameters.Required.Should().Contain("cep");
    }

    [Fact]
    public void JsonSchemaProperty_Construtor_PreenchCorretamente()
    {
        // Act
        var prop = new JsonSchemaProperty("string", "Descrição", new[] { "a", "b" });

        // Assert
        prop.Type.Should().Be("string");
        prop.Description.Should().Be("Descrição");
        prop.Enum.Should().BeEquivalentTo(new[] { "a", "b" });
    }
}