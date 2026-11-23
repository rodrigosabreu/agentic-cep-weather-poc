# Test Scenarios — agentic-cep-weather-poc

**Versão:** 1.0
**Data:** 2026-11-23
**Autor:** Sentinel (QA Engineer)
**Base:** REQUISITOS.md v1.0 (PRD aprovado)

---

## 1. Tool: `consultar_cep` (ViaCEP)

### Contrato Esperado
```json
{
  "name": "consultar_cep",
  "description": "Consulta endereço completo via CEP brasileiro",
  "parameters": {
    "type": "object",
    "properties": {
      "cep": { "type": "string", "description": "CEP no formato 00000-000 ou 00000000" }
    },
    "required": ["cep"]
  }
}
```

### Cenários de Teste

| ID | Cenário | Entrada | Resultado Esperado | Classificação |
|----|---------|---------|-------------------|---------------|
| CEP-01 | CEP válido com hífen | `"01310-100"` | CepResult com logradouro="Avenida Paulista", bairro="Bela Vista", localidade="São Paulo", uf="SP" | Sucesso |
| CEP-02 | CEP válido sem hífen | `"01310100"` | Mesmo resultado do CEP-01 (normalização automática) | Sucesso |
| CEP-03 | CEP válido com zeros à esquerda | `"01001-000"` | Endereço válido (Praça da Sé, São Paulo) | Sucesso |
| CEP-04 | CEP inexistente (ViaCEP retorna erro:true) | `"99999-999"` | `CepResult? = null` ou exceção tratada → mensagem amigável "Não encontrei informações para o CEP 99999-999" | Erro esperado |
| CEP-05 | Formato inválido (letras) | `"abcde-fgh"` | Validação falha antes da chamada HTTP → mensagem "CEP inválido. Use formato 00000-000" | Validação |
| CEP-06 | Formato inválido (curto) | `"12345"` | Validação falha → mensagem amigável | Validação |
| CEP-07 | Formato inválido (longo) | `"1234567890"` | Validação falha → mensagem amigável | Validação |
| CEP-08 | CEP vazio/nulo | `""` ou `null` | Validação falha → mensagem amigável | Validação |
| CEP-09 | ViaCEP timeout (10s) | `"01310-100"` | Retry 1x → se persistir, mensagem "Não foi possível consultar o CEP no momento" | Resiliência |
| CEP-10 | ViaCEP HTTP 5xx | `"01310-100"` | Retry 1x → se persistir, mensagem amigável | Resiliência |
| CEP-11 | ViaCEP HTTP 404 | `"01310-100"` | Tratar como inexistente → mensagem amigável | Erro esperado |
| CEP-12 | ViaCEP resposta malformada | `"01310-100"` | Try-catch JSON → mensagem amigável | Resiliência |
| CEP-13 | CEP de outra UF | `"30140-000"` | Belo Horizonte, MG — validar uf="MG" | Sucesso |
| CEP-14 | CEP com complemento | `"01310-100"` | Campo `complemento` preenchido se houver | Sucesso |

### Validações de Entrada (Regex)
- `^\d{5}-?\d{3}$` — 5 dígitos, hífen opcional, 3 dígitos
- Normalizar: remover hífen se houver, inserir após 5º dígito para chamada

### Evidências Necessárias
- Request/Response HTTP capturado (logs)
- Tempo de resposta (< 10s)
- Correlation ID no log

---

## 2. Tool: `consultar_previsao_tempo` (Open-Meteo)

### Contrato Esperado (Duas Sub-Tools)

#### 2.1 Geocoding: `geocode`
```json
{
  "name": "geocode",
  "description": "Obtém latitude/longitude para uma cidade",
  "parameters": {
    "type": "object",
    "properties": {
      "cidade": { "type": "string", "description": "Nome da cidade" },
      "uf": { "type": "string", "description": "UF opcional (ex: SP, RJ)" }
    },
    "required": ["cidade"]
  }
}
```

#### 2.2 Weather: `get_weather`
```json
{
  "name": "get_weather",
  "description": "Obtém tempo atual e previsão para coordenadas",
  "parameters": {
    "type": "object",
    "properties": {
      "latitude": { "type": "number" },
      "longitude": { "type": "number" }
    },
    "required": ["latitude", "longitude"]
  }
}
```

### Cenários de Teste — Geocoding

| ID | Cenário | Entrada | Resultado Esperado | Classificação |
|----|---------|---------|-------------------|---------------|
| GEO-01 | Cidade capital clara | `cidade="São Paulo"` | Coordinates(lat≈-23.55, lon≈-46.63) | Sucesso |
| GEO-02 | Cidade + UF (desambiguação) | `cidade="Rio de Janeiro", uf="RJ"` | Coordinates do RJ (não do Rio de Janeiro do Norte) | Sucesso |
| GEO-03 | Cidade com acento | `cidade="Brasília"` | Coordinates válidas | Sucesso |
| GEO-04 | Cidade minúscula/maiúscula | `cidade="sao paulo"` | Case-insensitive → mesmo resultado GEO-01 | Sucesso |
| GEO-05 | Cidade inexistente | `cidade="Xyzabc123"` | `Coordinates? = null` → mensagem "Não consegui localizar 'Xyzabc123'" | Erro esperado |
| GEO-06 | Cidade ambígua sem UF | `cidade="Porto Alegre"` | Retornar primeira resultado (RS) — aceitável para POC | Sucesso |
| GEO-07 | Cidade ambígua com UF errado | `cidade="Porto Alegre", uf="SC"` | Pode retornar SC ou falhar — documentar comportamento | Borda |
| GEO-08 | UF inválida | `cidade="São Paulo", uf="XX"` | Ignorar UF ou validar — definir comportamento | Borda |
| GEO-09 | Timeout geocoding (10s) | `cidade="São Paulo"` | Retry 1x → fallback mensagem amigável | Resiliência |
| GEO-10 | HTTP 5xx geocoding | `cidade="São Paulo"` | Retry 1x → fallback | Resiliência |
| GEO-11 | Resposta vazia (results=[]) | `cidade="CidadeQueNaoExiste"` | Tratar como não encontrado | Erro esperado |

### Cenários de Teste — Weather

| ID | Cenário | Entrada | Resultado Esperado | Classificação |
|----|---------|---------|-------------------|---------------|
| WEA-01 | Coordenadas válidas (SP) | `lat=-23.55, lon=-46.63` | WeatherResult com temperatura, condição, min/max hoje | Sucesso |
| WEA-02 | Coordenadas válidas (RJ) | `lat=-22.91, lon=-43.20` | Dados do RJ (diferente de SP) | Sucesso |
| WEA-03 | Coordenadas inválidas (oceano) | `lat=0, lon=0` | Open-Meteo retorna dados válidos (oceano) — aceitar | Borda |
| WEA-04 | Coordenadas fora do range | `lat=100, lon=200` | Validação prévia → erro amigável "Coordenadas inválidas" | Validação |
| WEA-05 | Mapeamento weathercode 0 | `current_weather.weathercode=0` | Descrição="Céu limpo" | Sucesso |
| WEA-06 | Mapeamento weathercode 61 | `weathercode=61` | Descrição="Chuva leve" | Sucesso |
| WEA-07 | Mapeamento weathercode 95 | `weathercode=95` | Descrição="Tempestade" | Sucesso |
| WEA-08 | Weathercode não mapeado | `weathercode=999` | Fallback: "Condição desconhecida (código 999)" | Borda |
| WEA-09 | Forecast diário (7 dias) | `daily.temperature_2m_max/min` | Previsão min/max para hoje + próximos dias | Sucesso |
| WEA-10 | Timeout weather (10s) | `lat=-23.55, lon=-46.63` | Retry 1x → fallback amigável | Resiliência |
| WEA-11 | HTTP 5xx weather | `lat=-23.55, lon=-46.63` | Retry 1x → fallback | Resiliência |
| WEA-12 | Resposta sem current_weather | `lat=-23.55, lon=-46.63` | Tratar graciosamente → "Dados de tempo indisponíveis" | Erro esperado |

### Mapeamento Weathercode WMO → PT-BR (Obrigatório)

| Código | Descrição PT-BR | Testar |
|--------|-----------------|--------|
| 0 | Céu limpo | ✅ |
| 1 | Principalmente limpo | ✅ |
| 2 | Parcialmente nublado | ✅ |
| 3 | Nublado | ✅ |
| 45 | Neblina | ✅ |
| 48 | Neblina depositando geada | ✅ |
| 51 | Chuvisco leve | ✅ |
| 53 | Chuvisco moderado | ✅ |
| 55 | Chuvisco forte | ✅ |
| 61 | Chuva leve | ✅ |
| 63 | Chuva moderada | ✅ |
| 65 | Chuva forte | ✅ |
| 71 | Neve leve | ✅ |
| 73 | Neve moderada | ✅ |
| 75 | Neve forte | ✅ |
| 80 | Pancadas de chuva leve | ✅ |
| 81 | Pancadas de chuva moderada | ✅ |
| 82 | Pancadas de chuva forte | ✅ |
| 95 | Tempestade | ✅ |
| 96 | Tempestade com granizo leve | ✅ |
| 99 | Tempestade com granizo forte | ✅ |

### Evidências Necessárias
- Request/Response HTTP (geocoding + weather)
- Weathercode recebido vs descrição mapeada
- Tempo de resposta (< 10s cada chamada)
- Correlation ID

---

## 3. Orquestrador do Agente (LLM + Tool Calling)

### System Prompt Base (PRD Anexo A)
```
Você é um assistente que consulta CEPs (ViaCEP) e previsão do tempo (Open-Meteo).
Use as tools disponíveis para obter dados reais. Não invente informações.

Tools:
- get_cep(cep): retorna endereço completo para CEP brasileiro válido
- geocode(cidade, uf?): retorna lat/lon para uma cidade
- get_weather(lat, lon): retorna tempo atual + previsão 7 dias

Regras:
- Sempre use tools para dados factuais
- Responda em PT-BR, linguagem natural, amigável
- Se tool falhar, informe o usuário e sugira alternativa
- Para tempo: mostre temp atual, condição, min/max hoje
- Para CEP: mostre logradouro, bairro, cidade, UF
- Combine informações quando relevante (ex: CEP → cidade → tempo)
```

### Cenários de Teste — Orquestração

| ID | Cenário | Entrada do Usuário | Tools Esperadas | Validação |
|----|---------|-------------------|-----------------|-----------|
| ORQ-01 | Consulta CEP simples | "Qual o endereço do CEP 01310-100?" | `consultar_cep` | Resposta contém "Avenida Paulista", "Bela Vista", "São Paulo", "SP" |
| ORQ-02 | Consulta CEP sem hífen | "cep 01310100" | `consultar_cep` | Mesmo resultado ORQ-01 |
| ORQ-03 | Consulta CEP linguagem natural | "me diga o endereço do cep 01310-100 por favor" | `consultar_cep` | Resposta natural, não JSON |
| ORQ-04 | Consulta tempo cidade | "Como está o tempo em São Paulo hoje?" | `geocode` → `get_weather` | Resposta: temp atual, condição, min/max hoje |
| ORQ-05 | Consulta tempo cidade+UF | "tempo em Rio de Janeiro, RJ" | `geocode(cidade="Rio de Janeiro", uf="RJ")` → `get_weather` | Dados do RJ (não SP) |
| ORQ-06 | Consulta tempo via CEP | "qual o tempo no CEP 01310-100?" | `consultar_cep` → `geocode` → `get_weather` | Resposta combina endereço + tempo |
| ORQ-07 | CEP inválido | "CEP 99999-999" | `consultar_cep` | Resposta: "Não encontrei informações para o CEP 99999-999..." |
| ORQ-08 | Localidade inexistente | "tempo em Xyzabc" | `geocode` | Resposta: "Não consegui localizar 'Xyzabc'..." |
| ORQ-09 | Contexto multi-turno | 1. "CEP 01310-100" 2. "e o tempo lá?" | 1. `consultar_cep` 2. `geocode`+`get_weather` (usar cidade da resposta anterior) | Segunda resposta usa contexto da primeira |
| ORQ-10 | Tool falha (ViaCEP down) | "CEP 01310-100" | `consultar_cep` (falha) | Resposta: "Não foi possível consultar o CEP no momento" |
| ORQ-11 | Tool falha (Open-Meteo down) | "tempo em São Paulo" | `geocode` → `get_weather` (falha) | Resposta: "Não foi possível consultar a previsão do tempo" |
| ORQ-12 | LLM não chama tool | "olá" | (nenhuma) | Resposta conversacional amigável |
| ORQ-13 | LLM alucina tool | "qual a capital da França?" | (nenhuma — não tem tool) | Resposta: "Não tenho acesso a essa informação" ou resposta baseada em conhecimento (definir) |
| ORQ-14 | Múltiplas tools mesma resposta | "CEP 01310-100 e tempo em SP" | `consultar_cep` + `geocode`+`get_weather` | Ambas respondidas |
| ORQ-15 | Max tool iterations | Prompt que causa loop | Respeitar `MaxToolIterations=5` | Não loop infinito |

### Validações de Contrato LLM

| Aspecto | Critério |
|---------|----------|
| Tool Calling nativo | Usar function calling do provedor (não parsing manual) |
| Tool definitions | Schema JSON correto para cada tool |
| Histórico | Enviar últimas N mensagens (padrão 10) |
| Temperature | 0.3 (determinístico para tool calling) |
| Max tokens | Configurável |
| Streaming | Opcional (POC = resposta completa) |

---

## 4. API Endpoints (Backend)

### POST /api/chat
```json
// Request
{ "sessionId": "guid-opcional", "message": "string" }

// Response 200
{ "sessionId": "guid", "response": "string" }

// Response 503 (LLM indisponível)
{ "error": "Serviço indisponível, tente novamente" }
```

### POST /api/chat/new
```json
// Response 200
{ "sessionId": "guid" }
```

### GET /health
```json
// Response 200
{ "status": "healthy" }
```

### Cenários de Teste — API

| ID | Cenário | Request | Expected Response | Classificação |
|----|---------|---------|-------------------|---------------|
| API-01 | Chat com message válido | `{ "message": "CEP 01310-100" }` | 200, sessionId, response string | Sucesso |
| API-02 | Chat sem sessionId | `{ "message": "oi" }` | 200, **novo** sessionId retornado | Sucesso |
| API-03 | Chat com sessionId existente | `{ "sessionId": "existente", "message": "e o tempo?" }` | 200, **mesmo** sessionId, resposta com contexto | Sucesso |
| API-04 | Chat message vazia | `{ "message": "" }` | 400 ou resposta amigável "Digite algo" | Validação |
| API-05 | Chat message nula | `{ }` | 400 | Validação |
| API-06 | Nova sessão | `POST /api/chat/new` | 200, `{ "sessionId": "novo-guid" }` | Sucesso |
| API-07 | Health check | `GET /health` | 200, `{ "status": "healthy" }` | Sucesso |
| API-08 | Sessão expira (TTL 30min) | Usar sessionId antigo | Nova sessão criada transparentemente | Resiliência |
| API-09 | Histórico limite (20 msgs) | 25 mensagens na sessão | Apenas últimas 20 mantidas | Limite |
| API-10 | CORS headers | Qualquer request | `Access-Control-Allow-Origin` configurado | Infra |
| API-11 | Correlation ID | Qualquer request | Header `X-Correlation-ID` ou log com ID | Observabilidade |

---

## 5. Frontend (Chat Interface)

### Componentes e Props

| Componente | Props | Responsabilidade |
|------------|-------|------------------|
| `ChatApp` | - | Root, gerencia estado sessão, histórico |
| `ChatMessages` | `messages: ChatMessage[]` | Render lista de bolhas |
| `ChatInput` | `onSend: (text) => void`, `disabled: boolean` | Input + botão enviar |
| `ChatHeader` | `onNewChat: () => void` | Título + botão "Nova conversa" |
| `LoadingIndicator` | - | Spinner/skeleton durante resposta |

### Cenários de Teste — Frontend (Vitest + RTL)

| ID | Cenário | Ação | Expected |
|----|---------|------|----------|
| FE-01 | Carregamento inicial | Abrir página | Chat vazio, input focado, botão "Nova conversa" visível |
| FE-02 | Enviar mensagem | Digitar "oi" + Enter | Bolha user aparece, loading indicator aparece |
| FE-03 | Resposta recebida | Mock API retorna | Bolha agent aparece, loading some |
| FE-04 | Shift+Enter | Digitar "linha1" + Shift+Enter + "linha2" | Nova linha no input (não envia) |
| FE-05 | Enter sozinho | Digitar "msg" + Enter | Envia (não nova linha) |
| FE-06 | Botão Nova Conversa | Clicar botão | Histórico limpo, input limpo e focado, nova sessionId |
| FE-07 | Responsivo 320px | Redimensionar para 320px | Layout adapta, mensagens legíveis, input acessível |
| FE-08 | Responsivo 1920px | Redimensionar para 1920px | Layout centralizado, legível |
| FE-09 | Navegação teclado | Tab/Shift+Tab | Foco move: input → botão nova conversa → mensagens (ARIA) |
| FE-10 | ARIA labels | Inspecionar DOM | `aria-label` no input, `role="log"` nas mensagens |
| FE-11 | Contraste | Lighthouse/axe | Score acessibilidade > 90 |
| FE-12 | Erro API | Mock 500 | Mensagem amigável "Erro ao enviar, tente novamente" |
| FE-13 | Loading state | Durante request | Spinner visível, input disabled |
| FE-14 | Timestamp | Mensagem recebida | Timestamp visível (formato local) |

### Cenários E2E (Playwright)

| ID | Jornada | Passos | Critério de Aceite (PRD) |
|----|---------|--------|--------------------------|
| E2E-01 | CA-01 | Abrir página | Chat vazio, input focado, botão visível |
| E2E-02 | CA-02 | Digitar "CEP 01310-100" + Enter | Bolha user, loading, resposta agent com endereço |
| E2E-03 | CA-03 | Clicar "Nova conversa" | Histórico limpo, nova sessionId |
| E2E-04 | CA-04 | Redimensionar 320→1920px | Layout adapta |
| E2E-05 | CA-05 | Tab navigation | Foco move corretamente |
| E2E-06 | CA-10 | "CEP 01310-100" | Resposta: Avenida Paulista, Bela Vista, São Paulo, SP |
| E2E-07 | CA-11 | "cep 01310100" | Mesmo resultado CA-10 |
| E2E-08 | CA-12 | "CEP 99999-999" | "Não encontrei informações..." |
| E2E-09 | CA-13 | "qual o endereço do cep 01310-100?" | Linguagem natural |
| E2E-10 | CA-20 | "tempo em São Paulo" | Temp atual, condição, min/max hoje |
| E2E-11 | CA-21 | "tempo em Rio de Janeiro, RJ" | Dados RJ |
| E2E-12 | CA-22 | "tempo no CEP 01310-100" | Endereço + tempo combinados |
| E2E-13 | CA-23 | "tempo em Xyzabc" | "Não consegui localizar..." |
| E2E-14 | CA-30 | POST /api/chat | 200 + sessionId + response |
| E2E-15 | CA-31 | POST /api/chat sem sessionId | Nova sessão |
| E2E-16 | CA-32 | POST /api/chat com sessionId | Continua sessão |
| E2E-17 | CA-33 | POST /api/chat/new | Nova sessionId |
| E2E-18 | CA-34 | GET /health | 200 healthy |
| E2E-19 | CA-35 | Simular LLM down | 503 ou resposta graciosa |
| E2E-20 | CA-36 | Simular ViaCEP down | "Não foi possível consultar o CEP" |

---

## 6. Fallback LLM: NVIDIA → OpenAI

### Configuração (Settings.cs)
```csharp
LlmSettings:
  Provider: "Nvidia"  // Primary
  Nvidia: { ApiKey, BaseUrl, Model: "nvidia/llama-3.1-nemotron-70b-instruct" }
  OpenAI: { ApiKey, BaseUrl, Model: "gpt-4o-mini" }  // Fallback
```

### Cenários de Teste

| ID | Cenário | Simulação | Expected |
|----|---------|-----------|----------|
| LLM-01 | NVIDIA sucesso | Normal | Resposta via NVIDIA, `ProviderName="Nvidia"` |
| LLM-02 | NVIDIA timeout (30s) | Mock delay > 30s | Fallback para OpenAI, resposta via OpenAI |
| LLM-03 | NVIDIA HTTP 5xx | Mock 500 | Fallback para OpenAI |
| LLM-04 | NVIDIA HTTP 401 | Mock 401 | Fallback para OpenAI (não retry) |
| LLM-05 | NVIDIA HTTP 429 | Mock 429 | Fallback para OpenAI |
| LLM-06 | Ambos falham | Mock ambos 500 | Resposta graciosa "Serviço indisponível, tente novamente" |
| LLM-07 | OpenAI sucesso direto | Provider="OpenAI" | Resposta via OpenAI |
| LLM-08 | Tool calling funciona no fallback | NVIDIA falha → OpenAI | Tools chamadas corretamente no fallback |

### Evidências
- Log: `ProviderName` usado
- Tempo de resposta total
- Tokens consumidos (Usage)

---

## 7. Infraestrutura e Deploy Local

### Scripts Obrigatórios

| Script | Função | Validação |
|--------|--------|-----------|
| `build.sh` | `dotnet publish -c Release -r linux-x64 --self-contained` + `npm run build` | Binário único em `publish/`, `dist/` gerado |
| `init.sh` | Inicia backend (porta 5000) + nginx (80/443) | Processos rodando, health check OK |
| `stop.sh` | Para processos limpos | Portas liberadas, sem órfãos |
| `agentic-cep-weather.service` | systemd unit (opcional) | `systemctl start` sobe, reinicia em falha |

### Cenários de Teste

| ID | Cenário | Comando | Expected |
|----|---------|---------|----------|
| INF-01 | Build backend | `./build.sh` | `publish/AgenticCepWeather.Api` executável, self-contained |
| INF-02 | Build frontend | `./build.sh` | `frontend/dist/index.html` + `assets/` |
| INF-03 | Init completo | `./init.sh` | Backend :5000 responde, nginx :80 serve frontend |
| INF-04 | Health check via nginx | `curl http://localhost/health` | 200 healthy (proxy_pass) |
| INF-05 | Chat via nginx | `curl -X POST http://localhost/api/chat -d '{"message":"oi"}'` | 200 response |
| INF-06 | Stop limpo | `./stop.sh` | Processos terminados, portas livres |
| INF-07 | Restart após stop | `./stop.sh && ./init.sh` | Sobe novamente sem erro |
| INF-08 | Variáveis sensíveis | Inspecionar build | Nenhuma API key no binário ou dist/ |
| INF-09 | Logs estruturados | `journalctl -u agentic-cep-weather` | JSON com correlation ID, nível configurável |
| INF-10 | systemd restart | `kill -9 <pid>` → `systemctl status` | Reinicia automaticamente |

---

## 8. Critérios de Qualidade e Classificação

### Definições

| Classificação | Definição | Ação |
|---------------|-----------|------|
| **Aprovado** | Critério verificado com evidência reproduzível (comando, output, versão) | Registrar evidência, prosseguir |
| **Reprovado** | Falha confirmada, comportamento não atende CA | Registrar defeito, bloquear até correção |
| **Bloqueado** | Dependência não entregue, ambiente não pronto, falha crítica impede progresso | Registrar bloqueio, aguardar desbloqueio |
| **Não executado** | Fora de escopo atual, tempo insuficiente, risco aceito documentado | Registrar justificativa, não conta como aprovação |

### Regras de Bloqueio (Hard Stops)
- Qualquer CA P0 (CA-01 a CA-43) **Reprovado** → Bloqueio de entrega
- Falha crítica de segurança (API keys expostas) → Bloqueio imediato
- Build não gera artefato executável → Bloqueio
- Health check falha em produção local → Bloqueio

### Evidências Mínimas por Execução
```
- Commit hash: `git rev-parse HEAD`
- Data/hora: ISO 8601
- Ambiente: local | VPS | CI
- Comando exato executado
- Output completo (stdout/stderr)
- Versões: .NET, Node, nginx
- Screenshots/vídeo (UI)
- Logs correlacionados (correlation ID)
```

---

## 9. Rastreabilidade: CA → Testes

| CA | Testes Relacionados | Tipo | Automação |
|----|---------------------|------|-----------|
| CA-01 | FE-01, E2E-01 | UI/E2E | Playwright |
| CA-02 | FE-02, FE-03, E2E-02 | UI/E2E | Playwright |
| CA-03 | FE-06, E2E-03 | UI/E2E | Playwright |
| CA-04 | FE-07, FE-08, E2E-04 | UI/E2E | Playwright |
| CA-05 | FE-09, FE-10, E2E-05 | UI/E2E | Playwright |
| CA-10 | ORQ-01, CEP-01, E2E-06 | Contrato/E2E | Unit + Playwright |
| CA-11 | ORQ-02, CEP-02, E2E-07 | Contrato/E2E | Unit + Playwright |
| CA-12 | ORQ-07, CEP-04, E2E-08 | Contrato/E2E | Unit + Playwright |
| CA-13 | ORQ-03, E2E-09 | E2E | Playwright |
| CA-20 | ORQ-04, GEO-01, WEA-01, E2E-10 | Contrato/E2E | Unit + Playwright |
| CA-21 | ORQ-05, GEO-02, WEA-02, E2E-11 | Contrato/E2E | Unit + Playwright |
| CA-22 | ORQ-06, CEP-01, GEO-01, WEA-01, E2E-12 | Integração/E2E | Unit + Playwright |
| CA-23 | ORQ-08, GEO-05, E2E-13 | Contrato/E2E | Unit + Playwright |
| CA-30 | API-01, E2E-14 | API | xUnit + Playwright |
| CA-31 | API-02, E2E-15 | API | xUnit + Playwright |
| CA-32 | API-03, E2E-16 | API | xUnit + Playwright |
| CA-33 | API-06, E2E-17 | API | xUnit + Playwright |
| CA-34 | API-07, E2E-18 | API | xUnit + Playwright |
| CA-35 | ORQ-10, ORQ-11, API-08, E2E-19 | Resiliência/E2E | Unit + Playwright |
| CA-36 | ORQ-10, CEP-09, E2E-20 | Resiliência/E2E | Unit + Playwright |
| CA-40 | INF-01 | Infra | Script + Manual |
| CA-41 | INF-02 | Infra | Script + Manual |
| CA-42 | INF-03, INF-04, INF-05 | Infra | Script + Manual |
| CA-43 | INF-06, INF-07, INF-09, INF-10 | Infra | Script + Manual |

---

## 10. Pendências e Riscos de QA

| Item | Risco | Mitigação |
|------|-------|-----------|
| LLM non-determinismo | Mesma entrada → respostas diferentes | Testar estrutura da resposta (contém campos), não texto exato |
| Tool calling falha silenciosa | LLM não chama tool | Validar `ToolCalls` no response, testar system prompt iterativo |
| Geocoding ambíguo | "Porto Alegre" → RS ou SC | Aceitar primeiro resultado, documentar limitação |
| ViaCEP/Open-Meteo instáveis | Flaky tests externos | Mock em unit tests; E2E marcar como "known flaky", retry |
| Sessão em memória | Perdida no restart | Documentar como limitação conhecida (POC) |
| Sem autenticação | Qualquer um usa | Aceitável para POC, não testar auth |
| Streaming não implementado | Resposta completa apenas | Não testar streaming, validar resposta completa |
| Contexto limitado (10 pares) | Histórico trunca | Testar limite explícito (API-09) |

---

## 11. Checklist de Execução (Quando Houver Código)

### Fase 1: Setup de Testes
- [ ] Criar projeto `AgenticCepWeather.Tests` (xUnit)
- [ ] Configurar `Microsoft.AspNetCore.Mvc.Testing` para TestServer
- [ ] Criar projeto `frontend/tests` (Vitest + RTL + MSW)
- [ ] Configurar Playwright (`npm init playwright`)
- [ ] Adicionar `test-driven-development` skill ao workflow

### Fase 2: Testes Unitários (Backend)
- [ ] Domain: CepInput normalização, WeatherInput, Weathercode mapping
- [ ] Application: Orquestrador, system prompt, histórico, tool calling flow
- [ ] Infrastructure: ICepService (ViaCEP), IWeatherService (Open-Meteo), timeouts, retry
- [ ] Llm: ILlmClient NVIDIA, fallback OpenAI, tool definitions

### Fase 3: Testes Unitários (Frontend)
- [ ] ChatInput: Enter/Shift+Enter, validação
- [ ] ChatMessages: render, timestamps, tool calls display
- [ ] ChatSession: histórico, nova conversa
- [ ] Hooks useChat: API integration, loading, errors

### Fase 4: Testes de Integração (Backend)
- [ ] POST /api/chat (sessionId, message, response)
- [ ] POST /api/chat/new
- [ ] GET /health
- [ ] Sessão TTL, limite histórico
- [ ] Correlation ID, logs

### Fase 5: Testes E2E (Playwright)
- [ ] Todos CA-01 a CA-43
- [ ] Jornadas de erro
- [ ] Fallback LLM
- [ ] Performance amostral (< 5s)

### Fase 6: Validação Infra
- [ ] build.sh, init.sh, stop.sh
- [ ] systemd (se aplicável)
- [ ] Variáveis sensíveis
- [ ] Logs estruturados

### Fase 7: Relatório Final
- [ ] Evidências por CA
- [ ] Classificação final por CA
- [ ] Riscos residuais documentados
- [ ] Recomendação: Aprovar | Bloquear | Condicional

---

**Fim do Documento de Cenários de Teste**
**Próxima atualização:** Quando houver primeiro build funcional (Nexus entrega P0)