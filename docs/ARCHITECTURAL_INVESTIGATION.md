# Investigação Arquitetural: Hermes Gateway como Runtime Agêntico

## Resumo Executivo

Foi avaliada a viabilidade de utilizar o **Hermes Multiplexed Gateway** (profile `cep-weather-llm`) como runtime LLM para a POC, mantendo o backend .NET como BFF e implementando tools nativas no Hermes para ViaCEP e Open-Meteo.

**Conclusão**: O Hermes Gateway **não suporta Function Calling controlado pelo cliente externo**. A arquitetura original com Function Calling controlado pelo backend .NET deve ser preservada.

---

## Testes Executados

### Configuração Experimental
- Profile dedicado: `cep-weather-llm`
- Provider: `openai-codex` / modelo `gpt-6-astra`
- Toolset customizado: `cep-weather` com 3 tools:
  - `consultar_cep` (ViaCEP)
  - `geocodificar_localidade` (Open-Meteo Geocoding)
  - `consultar_previsao_tempo` (Open-Meteo Forecast)
- Autenticação: `API_SERVER_KEY` rotacionada

### Resultados dos Testes

| Teste | Comando | Resultado |
|-------|---------|-----------|
| 1. Models endpoint | `GET /p/cep-weather-llm/v1/models` | ✅ 200 OK |
| 2. Chat sem tools | `POST /chat/completions` (sem tools) | ✅ 200 OK - Resposta textual |
| 3. Chat com tools (auto) | `POST /chat/completions` + `tools` + `tool_choice: auto` | ✅ 200 OK - **Sem tool_calls** |
| 4. Chat com tools (required) | `POST /chat/completions` + `tools` + `tool_choice: required` | ✅ 200 OK - **Sem tool_calls** |
| 5. Chat com tools (explicit) | `POST /chat/completions` + `tools` + `tool_choice: {function: ...}` | ⏱️ Timeout |

### Evidência Chave

```json
// Resposta com tool_choice: required - NENHUM tool_calls
{
  "choices": [{
    "message": {
      "role": "assistant",
      "content": "O CEP 01001-000 fica na região da Sé, em São Paulo (SP)...",
      "finish_reason": "stop"
    }
  }]
}
```

---

## Análise da Causa Raiz

### Código-Fonte: `/opt/hermes/gateway/platforms/api_server.py`

Função `_request_agent_overrides()` (linha 269) extrai apenas:
- `provider`
- `model` 
- `model_options`

**Parâmetros `tools` e `tool_choice` são IGNORADOS** - não são encaminhados ao agente.

### Comportamento do Agente
- O agente usa **apenas tools configuradas no profile** via `platform_toolsets`
- Tools executam **internamente no loop do agente** (Hermes-controlled)
- **Não retornam `tool_calls`** para execução externa pelo cliente
- O Gateway é um **runtime de agente**, não um proxy OpenAI-compatible para Function Calling

---

## Decisão Arquitetural

| Abordagem | Veredito |
|-----------|----------|
| Hermes Gateway como Function Calling provider | ❌ **Rejeitado** - Não suporta `tool_calls` client-controlled |
| Hermes native tools + Gateway agent loop | ⚠️ Possível mas foge do requisito (controle .NET) |
| **.NET backend com OpenAI API direta** | ✅ **Mantido** - Controle total do loop Tool Calling |

---

## Próximos Passos

1. Configurar OpenAI API Key no backend .NET (`LlmSettings__OpenAI__ApiKey`)
2. OpenAI como provedor primário (Function Calling garantido)
3. NVIDIA como fallback configurável (desabilitado por padrão)
4. Validar fluxo completo: CEP → geocode → weather → continuidade
5. Executar testes automatizados e aceite funcional

---

## Artefatos Preservados

- `/opt/data/profiles/.disabled-cep-weather-llm/` - Profile experimental desabilitado
- `/opt/data/profiles/.disabled-cep-weather-llm/skills/cep_weather_tools.py` - Tools nativas implementadas
- Tools registradas no Hermes registry (toolset `cep-weather`)

---

*Investigação realizada em 24/11/2026 - Concluída sem modificação dos profiles originais da Squad*
