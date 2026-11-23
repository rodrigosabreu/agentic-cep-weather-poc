using AgenticCepWeather.Application.Services;
using AgenticCepWeather.Domain.Interfaces;
using AgenticCepWeather.Infrastructure.ExternalApis;
using AgenticCepWeather.Infrastructure.Session;
using AgenticCepWeather.Llm.Abstractions;
using AgenticCepWeather.Llm.Providers;
using AgenticCepWeather.Domain.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LlmSettings>(builder.Configuration.GetSection("LlmSettings"));
builder.Services.Configure<ExternalApiSettings>(builder.Configuration.GetSection("ExternalApiSettings"));
builder.Services.Configure<ServerSettings>(builder.Configuration.GetSection("ServerSettings"));

builder.Services.AddHttpClient<ViaCepService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<OpenMeteoService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<NvidiaLlmClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<OpenAILlmClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IChatSessionStore, InMemoryChatSessionStore>();
builder.Services.AddScoped<ICepService, ViaCepService>();
builder.Services.AddScoped<IWeatherService, OpenMeteoService>();
builder.Services.AddScoped<IToolExecutor, ToolExecutor>();
builder.Services.AddScoped<ISystemPromptProvider, SystemPromptProvider>();
builder.Services.AddScoped<IChatOrchestrator, ChatOrchestrator>();
builder.Services.AddScoped<ILlmClientFactory, LlmClientFactory>();
builder.Services.AddScoped<NvidiaLlmClient>();
builder.Services.AddScoped<OpenAILlmClient>();

builder.Services.AddCors(options =>
{
    var corsSettings = builder.Configuration.GetSection("ServerSettings:Cors").Get<CorsSettings>()
        ?? new CorsSettings { AllowedOrigins = ["*"] };

    options.AddDefaultPolicy(policy =>
    {
        if (corsSettings.AllowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(corsSettings.AllowedOrigins);
        }
        policy.WithMethods(corsSettings.AllowedMethods);
        policy.WithHeaders(corsSettings.AllowedHeaders);
        if (corsSettings.AllowCredentials)
            policy.AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapPost("/api/chat", async (ChatRequest request, IChatOrchestrator orchestrator, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.SessionId))
    {
        return Results.BadRequest(new { error = "SessionId é obrigatório" });
    }

    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Mensagem é obrigatória" });
    }

    try
    {
        var response = await orchestrator.ProcessMessageAsync(request.SessionId, request.Message, ct);
        return Results.Ok(new ChatResponseDto(response.Message, response.ToolResults, response.SessionId));
    }
    catch (AgenticCepWeather.Domain.Exceptions.AgenticCepWeatherException ex)
    {
        return Results.Problem(ex.Message, statusCode: 500, title: ex.ErrorCode);
    }
    catch (Exception ex)
    {
        return Results.Problem("Erro interno do servidor", statusCode: 500);
    }
});

app.MapPost("/api/chat/stream", async (ChatRequest request, IChatOrchestrator orchestrator, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.SessionId))
    {
        return Results.BadRequest(new { error = "SessionId é obrigatório" });
    }

    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Mensagem é obrigatória" });
    }

    var responseStream = await orchestrator.ProcessMessageStreamAsync(request.SessionId, request.Message, ct);

    return Results.Ok(new
    {
        stream = true,
        sessionId = request.SessionId
    });
});

app.MapGet("/api/sessions/{sessionId}", async (string sessionId, IChatSessionStore store, CancellationToken ct) =>
{
    var session = await store.GetAsync(sessionId, ct);
    return session != null
        ? Results.Ok(new SessionDto(session.Id, session.Messages, session.CreatedAt, session.UpdatedAt))
        : Results.NotFound(new { error = "Sessão não encontrada" });
});

app.MapDelete("/api/sessions/{sessionId}", async (string sessionId, IChatSessionStore store, CancellationToken ct) =>
{
    var deleted = await store.DeleteAsync(sessionId, ct);
    return deleted ? Results.NoContent() : Results.NotFound(new { error = "Sessão não encontrada" });
});

app.MapGet("/api/tools", (IToolExecutor toolExecutor) =>
{
    var tools = toolExecutor.GetToolDefinitions();
    return Results.Ok(tools);
});

app.Run();

record ChatRequest(string SessionId, string Message);
record ChatResponseDto(string Message, IReadOnlyList<AgenticCepWeather.Domain.Models.ToolResult> ToolResults, string SessionId);
record SessionDto(string Id, IReadOnlyList<AgenticCepWeather.Domain.Models.ChatMessage> Messages, DateTime CreatedAt, DateTime UpdatedAt);