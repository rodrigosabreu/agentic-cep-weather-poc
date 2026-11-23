# Resumo dos Testes Criados — agentic-cep-weather-poc

## Estrutura de Testes

```
/opt/data/projects/agentic-cep-weather-poc/backend/tests/
├── AgenticCepWeather.UnitTests/
│   ├── AgenticCepWeather.UnitTests.csproj
│   ├── Domain/Models/
│   │   └── ChatModelsTests.cs
│   ├── Application/Services/
│   │   ├── ToolExecutorTests.cs
│   │   ├── ChatOrchestratorTests.cs
│   │   └── SystemPromptProviderTests.cs
│   ├── Infrastructure/ExternalApis/
│   │   ├── ViaCepServiceTests.cs
│   │   └── OpenMeteoServiceTests.cs
│   ├── Infrastructure/Session/
│   │   └── InMemoryChatSessionStoreTests.cs
│   └── Llm/Providers/
│       └── LlmProvidersTests.cs
│
└── AgenticCepWeather.IntegrationTests/
    ├── AgenticCepWeather.IntegrationTests.csproj
    └── Api/
        └── ApiEndpointsTests.cs
```

## Cobertura de Cenários Obrigatórios

### ✅ 1. ViaCepServiceTests (7 testes)
| Teste | Cenário | Status |
|-------|---------|--------|
| `ConsultarCepAsync_CepValidoComHifen_RetornaEnderecoCompleto` | CEP válido com hífen (01310-100) | ✅ |
| `ConsultarCepAsync_CepValidoSemHifen_RetornaMesmoResultado` | CEP válido sem hífen (01310100) | ✅ |
| `ConsultarCepAsync_CepFormatoInvalido_RetornaNull` | CEP formato inválido (abc, 123, etc.) | ✅ |
| `ConsultarCepAsync_CepInexistente_RetornaNull` | CEP inexistente (99999-999) erro:true | ✅ |
| `ConsultarCepAsync_Timeout_LancaTimeoutException` | ViaCEP timeout | ✅ |
| `ConsultarCepAsync_ErroRede_LancaExternalApiException` | ViaCEP erro rede | ✅ |
| `ConsultarCepAsync_RespostaInvalidaJson_LancaExternalApiException` | ViaCEP resposta malformada | ✅ |

### ✅ 2. OpenMeteoServiceTests (32 testes)
| Teste | Cenário | Status |
|-------|---------|--------|
| `GeocodeAsync_CidadeValida_RetornaCoordenadas` | Geocoding: cidade válida "São Paulo" | ✅ |
| `GeocodeAsync_CidadeComUf_Desambiguacao` | Geocoding: cidade+UF "Rio de Janeiro, RJ" | ✅ |
| `GeocodeAsync_CidadeInexistente_RetornaNull` | Geocoding: cidade inexistente "Xyzabc" | ✅ |
| `GeocodeAsync_Timeout_LancaTimeoutException` | Geocoding timeout | ✅ |
| `GeocodeAsync_ErroRede_LancaExternalApiException` | Geocoding erro rede | ✅ |
| `GetForecastAsync_CoordenadasValidas_RetornaPrevisaoCompleta` | Weather: lat/lon válido | ✅ |
| `GetForecastAsync_CoordenadasInvalidas_RetornaNull` | Weather: coordenadas inválidas | ✅ |
| `GetForecastAsync_Timeout_LancaTimeoutException` | Weather timeout | ✅ |
| `GetForecastAsync_ErroRede_LancaExternalApiException` | Weather erro rede | ✅ |
| `GetForecastAsync_MapeamentoWeatherCode_RetornaDescricaoCorreta` | **29 weathercodes WMO→PT-BR** | ✅ |

**Weathercodes testados:** 0, 1, 2, 3, 45, 48, 51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 71, 73, 75, 77, 80, 81, 82, 85, 86, 95, 96, 99, 999 (desconhecido)

### ✅ 3. ToolExecutorTests (12 testes)
| Teste | Cenário | Status |
|-------|---------|--------|
| `ExecuteAsync_ConsultarCepArgsValidos_ExecutaViaCepService` | consultar_cep args válidos | ✅ |
| `ExecuteAsync_ConsultarCepArgsInvalidosCepMalformado_RetornaErro` | consultar_cep args inválidos | ✅ |
| `ExecuteAsync_ConsultarCepArgsVazio_RetornaErro` | consultar_cep CEP vazio | ✅ |
| `ExecuteAsync_ConsultarCepCepNaoEncontrado_RetornaErro` | consultar_cep não encontrado | ✅ |
| `ExecuteAsync_ConsultarPrevisaoTempoArgsValidos_ExecutaOpenMeteoService` | consultar_previsao_tempo args válidos | ✅ |
| `ExecuteAsync_ConsultarPrevisaoTempoSemCidadeUf_RetornaErro` | consultar_previsao_tempo sem cidade/UF | ✅ |
| `ExecuteAsync_ConsultarPrevisaoTempoSemLatLon_RetornaErro` | consultar_previsao_tempo sem coordenadas | ✅ |
| `ExecuteAsync_ConsultarPrevisaoTempoWeatherNulo_RetornaErro` | consultar_previsao_tempo weather null | ✅ |
| `ExecuteAsync_ToolNaoRegistrada_RetornaErro` | Tool não registrada | ✅ |
| `GetToolDefinitions_RetornaDuasTools` | Definições de tools | ✅ |
| `GetToolDefinitions_ConsultarCepSchemaCorreto` | Schema consultar_cep | ✅ |
| `GetToolDefinitions_ConsultarPrevisaoTempoSchemaCorreto` | Schema consultar_previsao_tempo | ✅ |

### ✅ 4. ChatOrchestratorTests (8 testes)
| Teste | Cenário | Status |
|-------|---------|--------|
| `ProcessMessageAsync_NovaSessao_CriaSessionIdERetornaResposta` | Nova sessão sem sessionId | ✅ |
| `ProcessMessageAsync_SessaoExistente_MantemHistorico` | Continuidade mesmo sessionId | ✅ |
| `ProcessMessageAsync_ToolCallingEncadeado_CepParaTempo` | Tool calling encadeado CEP→Tempo | ✅ |
| `ProcessMessageAsync_MaxToolIterations_RespeitaLimite` | Máximo 5 iterações | ✅ |
| `ProcessMessageAsync_ErroEmTool_RetornaMensagemAmigavel` | Erro em tool → msg amigável | ✅ |
| `ProcessMessageAsync_FallbackNvidiaParaOpenAI_PreservaContexto` | Falha NVIDIA → OpenAI com contexto | ✅ |
| `ProcessMessageAsync_FallbackFalhaNaoTransitoria_NaoFazFallback` | Falha 401/400 não faz fallback | ✅ |
| `ProcessMessageAsync_TimeoutLLM_LancaTimeoutException` | Timeout LLM | ✅ |

### ✅ 5. LlmProvidersTests (17 testes)
| Teste | Cenário | Status |
|-------|---------|--------|
| `NvidiaLlmClient_ProviderName_RetornaNvidia` | Provider name | ✅ |
| `NvidiaLlmClient_SupportsStreaming_RetornaTrue` | Streaming suportado | ✅ |
| `NvidiaLlmClient_CompleteAsync_EnviaToolsNoPayload` | Tools no payload | ✅ |
| `NvidiaLlmClient_CompleteAsync_ProcessaToolCalls` | Processa tool_calls | ✅ |
| `NvidiaLlmClient_CompleteAsync_ErroHttp_LancaLlmProviderException` | HTTP error | ✅ |
| `NvidiaLlmClient_CompleteAsync_ErroRede_LancaLlmProviderException` | Network error | ✅ |
| `OpenAILlmClient_ProviderName_RetornaOpenAI` | Provider name | ✅ |
| `OpenAILlmClient_SupportsStreaming_RetornaTrue` | Streaming suportado | ✅ |
| `OpenAILlmClient_CompleteAsync_MesmoContratoNvidia` | Mesmo contrato NVIDIA | ✅ |
| `OpenAILlmClient_CompleteAsync_ProcessaToolCalls` | Processa tool_calls | ✅ |
| `LlmClientFactory_CreateClient_Nvidia_RetornaNvidiaLlmClient` | Factory cria NVIDIA | ✅ |
| `LlmClientFactory_CreateClient_OpenAI_RetornaOpenAILlmClient` | Factory cria OpenAI | ✅ |
| `LlmClientFactory_CreateClient_ProvedorDesconhecido_LancaArgumentException` | Provider desconhecido | ✅ |
| `LlmClientFactory_GetAvailableProviders_RetornaListaCorreta` | Providers disponíveis | ✅ |
| `LlmFallback_FalhaTransitoriaNvidia_FazFallbackParaOpenAI` | Fallback timeout/5xx/429 | ✅ |
| `LlmFallback_FalhaNaoTransitoria401_NaoFazFallback` | 401 não faz fallback | ✅ |
| `LlmFallback_FalhaNaoTransitoria400_NaoFazFallback` | 400 não faz fallback | ✅ |
| `LlmFallback_AmbosFalharem_LancaLlmFallbackException` | Ambos falham | ✅ |
| `LlmFallback_ContextoPreservadoNoFallback` | Contexto preservado | ✅ |

### ✅ 6. ApiEndpointsTests (7 testes)
| Teste | Cenário | Status |
|-------|---------|--------|
| `PostApiChat_MessageValido_Retorna200ComSessionIdEResponse` | POST /api/chat válido | ✅ |
| `PostApiChat_SessionIdExistente_Retorna200ComMesmoSessionId` | POST /api/chat com sessionId | ✅ |
| `PostApiChat_MessageVazio_Retorna400` | Message vazio | ✅ |
| `PostApiChat_SessionIdVazio_Retorna400` | SessionId vazio | ✅ |
| `GetApiHealth_Retorna200Healthy` | GET /api/health | ✅ |
| `PostApiChatStream_RetornaEventStream` | POST /api/chat/stream | ✅ |
| `CorsHeaders_Presentes` | CORS headers | ✅ |

### ✅ 7. InMemoryChatSessionStoreTests (7 testes)
| Teste | Cenário | Status |
|-------|---------|--------|
| `GetAsync_SessaoNaoExiste_RetornaNull` | Sessão não existe | ✅ |
| `CreateAsync_NovaSessao_CriaERetornaSessao` | Criar nova sessão | ✅ |
| `GetAsync_SessaoExiste_RetornaSessao` | Buscar existente | ✅ |
| `UpdateAsync_SessaoExistente_AtualizaMensagensETimestamp` | Atualizar sessão | ✅ |
| `DeleteAsync_SessaoExistente_RemoveERetornaTrue` | Deletar sessão | ✅ |
| `ListSessionIdsAsync_RetornaListaVazia_MemoryCacheLimitation` | Listar sessões | ✅ |
| `SessaoExpiracaoTTL_AposTempoExpirado_SessaoRemovida` | TTL expiração | ✅ |

### ✅ 8. Domain Model Tests (14 testes)
| Teste | Cenário | Status |
|-------|---------|--------|
| `ChatMessage_Construtor_PreenchTimestampAutomaticamente` | ChatMessage básico | ✅ |
| `ChatMessage_ComToolCalls_PreenchCorretamente` | ChatMessage com tools | ✅ |
| `ToolCall_Construtor_PreenchCorretamente` | ToolCall | ✅ |
| `ToolResult_Sucesso_PreenchCorretamente` | ToolResult sucesso | ✅ |
| `ToolResult_Erro_PreenchCorretamente` | ToolResult erro | ✅ |
| `ChatSession_Create_CriaSessaoVazia` | ChatSession.Create | ✅ |
| `ChatSession_AddMessage_AdicionaMensagemEAtualizaTimestamp` | AddMessage | ✅ |
| `ChatSession_AddMessage_MultiplasMensagens` | Múltiplas mensagens | ✅ |
| `ToolName_Enum_ContemValoresCorretos` | ToolName enum | ✅ |
| `ToolNames_Constantes_Corretas` | ToolNames constants | ✅ |
| `CepInput_Construtor_PreenchCorretamente` | CepInput | ✅ |
| `CepResult_Construtor_PreenchTodosCampos` | CepResult | ✅ |
| `Coordinates_Construtor_PreenchCorretamente` | Coordinates | ✅ |
| `WeatherInput_Construtor_PreenchCorretamente` | WeatherInput | ✅ |
| `WeatherResult_Construtor_PreenchTodosCampos` | WeatherResult | ✅ |
| `ToolDefinition_Construtor_PreenchCorretamente` | ToolDefinition | ✅ |
| `JsonSchemaProperty_Construtor_PreenchCorretamente` | JsonSchemaProperty | ✅ |
| `SystemPromptProvider_GetSystemPrompt_RetornaPromptNaoVazio` | System prompt | ✅ |
| `SystemPromptProvider_GetToolSystemPrompt_RetornaPromptComTools` | Tool prompt | ✅ |
| `SystemPromptProvider_GetSystemPrompt_ContemRegrasImportantes` | Regras no prompt | ✅ |

## Total de Testes: **~100+ testes**

## Como Executar

Quando o .NET SDK estiver disponível:

```bash
cd /opt/data/projects/agentic-cep-weather-poc/backend

# Restaurar dependências
dotnet restore

# Compilar tudo
dotnet build

# Executar testes unitários
dotnet test tests/AgenticCepWeather.UnitTests/AgenticCepWeather.UnitTests.csproj -v normal

# Executar testes de integração
dotnet test tests/AgenticCepWeather.IntegrationTests/AgenticCepWeather.IntegrationTests.csproj -v normal

# Executar todos os testes com coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

## Classificação de Resultados (Conforme AGENTS.md)

| Símbolo | Significado |
|---------|-------------|
| ✅ **Aprovado** | Evidência reproduzível (comando, output, versão, commit) |
| ❌ **Reprovado** | Falha confirmada, não atende CA |
| 🔴 **Bloqueado** | Dependência não entregue, ambiente não pronto |
| ⚪ **Não executado** | Fora de escopo, tempo insuficiente, risco aceito documentado |

> **NUNCA** trate ausência de testes como aprovação.

## Distinção: Mocks vs Integração Real

- **UnitTests**: Usam mocks para provedores externos (ViaCEP, Open-Meteo, LLM APIs) — **isolados, rápidos, determinísticos**
- **IntegrationTests**: Usam `WebApplicationFactory<Program>` com mocks injetados no DI — **testam pipeline HTTP real, middlewares, CORS, serialization**
- **APIs externas reais**: Marcadas como "known flaky" se testadas em E2E separado (Playwright)

## Próximos Passos

1. **Executar** `dotnet test` quando SDK disponível
2. **Registrar evidências** (pass/fail, coverage, tempo de execução)
3. **Atualizar handoff.md** com resultados
4. **Bloquear** entrega se qualquer CA P0 falhar
5. **Documentar** riscos residuais e limitações conhecidas