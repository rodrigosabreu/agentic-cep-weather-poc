# Task: POC agentic-cep-weather-poc — PM Requirements + QA Planning (MVP)

- **Project**: agentic-cep-weather-poc
- **Owner**: Polaris (Product Manager) + Sentinel (QA Engineer)
- **Status**: implementation complete — ready for functional acceptance testing
- **Approved**: PRD, Cenários de Teste e Backlog criados — 2026-11-23 — Implementação completa pelo Nexus (backend + frontend + infra)
- **Waiting**: Configuração de .env com API keys (NVIDIA/OpenAI), execução de build.sh + start.sh, validação funcional dos CAs
- **Next action**: Configurar .env, executar build.sh, start.sh e validar todos os 43 CAs

---

## Artifacts
- /opt/data/projects/agentic-cep-weather-poc/REQUISITOS.md (PRD aprovado)
- /opt/data/projects/agentic-cep-weather-poc/CENÁRIOS_TESTE.md (54 casos de teste, matriz CA→Testes, checklist Go/No-Go)
- /opt/data/projects/agentic-cep-weather-poc/BACKLOG.md (8 épicos, 57 itens, ~53.5h, tracks paralelos, DoD, riscos)
- /opt/data/projects/agentic-cep-weather-poc/backend/AgenticCepWeather.slnx (Clean Architecture .NET 10)
- /opt/data/projects/agentic-cep-weather-poc/backend/src/AgenticCepWeather.Api/ (Minimal APIs, Program.cs)
- /opt/data/projects/agentic-cep-weather-poc/backend/src/AgenticCepWeather.Application/ (ChatOrchestrator, ToolExecutor, SystemPromptProvider)
- /opt/data/projects/agentic-cep-weather-poc/backend/src/AgenticCepWeather.Domain/ (Models, Interfaces, Settings, Exceptions)
- /opt/data/projects/agentic-cep-weather-poc/backend/src/AgenticCepWeather.Infrastructure/ (ViaCepService, OpenMeteoService, InMemoryChatSessionStore)
- /opt/data/projects/agentic-cep-weather-poc/backend/src/AgenticCepWeather.Llm/ (NvidiaLlmClient, OpenAILlmClient, LlmClientFactory, BaseLlmClient)
- /opt/data/projects/agentic-cep-weather-poc/frontend/ (React + TypeScript + Vite + Tailwind + Zustand)
- /opt/data/projects/agentic-cep-weather-poc/scripts/ (build.sh, start.sh, stop.sh, env.example)

---

## History
- **2026-11-23 21:00** | planejamento | QA Sentinel iniciou planejamento da estratégia de testes baseada no PRD v1.0 aprovado. Projeto em estágio inicial: apenas Domain layer implementado.
- **2026-11-23 22:30** | implementação testes | QA Sentinel criou estrutura completa de testes xUnit:
  - `tests/AgenticCepWeather.UnitTests` — ~85 testes unitários com mocks (ViaCEP, Open-Meteo, ToolExecutor, ChatOrchestrator, LLM Providers, SessionStore, Domain Models)
  - `tests/AgenticCepWeather.IntegrationTests` — 7 testes de integração com WebApplicationFactory (API endpoints, CORS, health check)
  - Adicionados ao solution file
  - Todos os 6 grupos de cenários obrigatórios cobertos
  - Aguardando .NET SDK para execução e validação
- **2026-11-23 21:20** | PRD | REQUISITOS.md criado: visão, escopo MVP, exclusões, 5 jornadas, 28 RFs, 7 RNFs, 43 CAs verificáveis, arquitetura sessão memória, env vars, riscos
- **2026-11-23 21:35** | Test Scenarios | CENÁRIOS_TESTE.md criado: estratégia, 54 casos (UI, CEP, Tempo, Combinado, Sessão, API, Orquestrador, NFR, A11y), dados de teste reais, checklist Go/No-Go, template bug
- **2026-11-23 21:50** | Backlog | BACKLOG.md criado: 8 épicos, 57 stories/tasks, mapa dependências (3 tracks paralelos), ~53.5h, DoD POC, riscos por item, próximas ações
- **2026-11-23 21:55** | Handoff | Atualização deste handoff com artefatos PM (append-only)
- **2026-11-23 22:30** | Implementation | Nexus concluiu implementação completa: Backend (Clean Architecture .NET 10, 5 projetos), Frontend (React TS Vite Tailwind Zustand, streaming SSE), Infra (build.sh self-contained linux-x64, start/stop scripts, env.example). Tools: consultar_cep (ViaCEP), consultar_previsao_tempo (Open-Meteo geocoding+forecast). LLM: NVIDIA primary + OpenAI fallback com tool calling nativo. Sessões em memória com TTL.

---

## Validation
- **Checks executed**: 
  - Código completo implementado (backend + frontend + scripts)
  - Build scripts prontos (build.sh, start.sh, stop.sh)
  - Clean Architecture .NET 10 com 5 projetos (Api, Application, Domain, Infrastructure, Llm)
  - Frontend React 18 + TypeScript + Vite + Tailwind + Zustand
  - 2 Tools implementadas: consultar_cep (ViaCEP), consultar_previsao_tempo (Open-Meteo)
  - LLM Providers: NVIDIA (primary) + OpenAI (fallback) com tool calling nativo e streaming SSE
  - Sessões em memória com TTL (InMemoryChatSessionStore)
  - Endpoints: /api/chat, /api/chat/stream, /api/health, /api/sessions/{id}, /api/tools
  - Variáveis de ambiente via .env (env.example documentado)
- **Results**: Implementação feature-complete para MVP. Aguardando configuração de API keys e execução de testes de aceitação.
- **Pending**: 
  1. Configurar .env com NVIDIA_API_KEY e/ou OPENAI_API_KEY
  2. Executar `scripts/build.sh` (requer .NET 10 SDK e Node.js)
  3. Executar `scripts/start.sh`
  4. Validar todos os 43 CAs (CA-01 a CA-43) via teste manual
  5. Registrar evidências: provedor usado, tools executadas, resultados, limitações

---

## Notes

### Objetivo
Definir estratégia de testes abrangente para o POC **agentic-cep-weather-poc** — chat conversacional com LLM + Tool Calling (ViaCEP + Open-Meteo), executando nativamente na VPS Hostinger (.NET 10 self-contained linux-x64 + React build estático, sem Docker/PostgreSQL).

### Escopo de Testes (baseado no PRD)

#### 1. Testes Unitários (Backend)
| Camada | Foco | Ferramenta |
|--------|------|------------|
| Domain | Validação de modelos (CepInput, WeatherInput), normalização CEP, mapeamento weathercode | xUnit |
| Application | Orquestração do agente, system prompt, histórico de conversa, tool calling flow | xUnit + Moq |
| Infrastructure | ICepService (ViaCEP), IWeatherService (Open-Meteo Geocoding + Weather), timeouts, retry, fallbacks | xUnit + Moq (HttpClient mock) |
| Llm | ILlmClient (NVIDIA + OpenAI fallback), tool definitions, streaming | xUnit + Moq |

#### 2. Testes Unitários (Frontend)
| Componente | Foco | Ferramenta |
|------------|------|------------|
| ChatInput | Enter envia, Shift+Enter nova linha, validação vazio | Vitest + RTL |
| ChatMessages | Render bolhas user/agent, timestamps, tool calls display | Vitest + RTL |
| ChatSession | Histórico local, botão "Nova conversa" limpa estado | Vitest + RTL |
| Hooks (useChat) | Integração API, loading states, error handling | Vitest + RTL + MSW |

#### 3. Testes de Contrato (Tools)
| Tool | Cenários |
|------|----------|
| `consultar_cep` | CEP válido com hífen (01310-100), sem hífen (01310100), inválido formato (abc), inexistente (99999-999), ViaCEP retorna erro: true, timeout/rede |
| `consultar_previsao_tempo` | Geocoding: cidade válida (São Paulo), cidade+UF (Rio de Janeiro, RJ), cidade inexistente (Xyzabc), ambígua; Weather: lat/lon válido, coordenadas inválidas, timeout, mapeamento weathercode WMO→PT-BR |

#### 4. Testes de Integração (Backend)
- POST /api/chat: message válido → 200 + sessionId + response
- POST /api/chat: sem sessionId → nova sessão
- POST /api/chat: com sessionId existente → continua sessão, histórico mantido (máx 20 msgs)
- POST /api/chat/new → nova sessionId
- GET /health → 200 + healthy
- LLM indisponível → 503 ou resposta graciosa
- ViaCEP indisponível → mensagem amigável
- Open-Meteo indisponível → mensagem amigável

#### 5. Testes End-to-End (Frontend + Backend)
| Jornada | Critérios de Aceite (PRD) |
|---------|---------------------------|
| CA-01 a CA-05 | Chat Interface: carregamento, envio, nova conversa, responsivo, teclado |
| CA-10 a CA-13 | Consulta CEP: válido, sem hífen, inválido, linguagem natural |
| CA-20 a CA-23 | Consulta Tempo: cidade, cidade+UF, CEP→tempo, localidade inexistente |
| CA-30 a CA-36 | API: chat, sessões, health, erros LLM/ViaCEP |

#### 6. Testes de Resiliência e Fallback
- **LLM NVIDIA falha** → fallback automático para OpenAI (gpt-4o-mini)
- **LLM ambos falham** → mensagem amigável "Serviço indisponível"
- **ViaCEP timeout/5xx** → retry 1x → mensagem amigável
- **Open-Meteo timeout/5xx** → retry 1x → mensagem amigável
- **Sessão expira (TTL 30min)** → nova sessão transparente

#### 7. Testes de Infraestrutura (Execução Local)
- `dotnet publish -c Release -r linux-x64 --self-contained` → binário único em publish/
- `npm run build` → dist/ com index.html + assets
- `init.sh` → backend porta 5000, nginx frontend 80/443
- `stop.sh` → para processos limpos
- systemd (se admin) → serviço sobe, reinicia em falha, logs journalctl

#### 8. Testes Não-Funcionais (Amostrais)
- Performance: resposta chat < 5s (P95) — teste manual com cronômetro
- Acessibilidade: axe-core/lighthouse — score > 90
- Segurança: API keys apenas em env vars, nunca no código/build
- Logs: correlation ID por request, nível configurável

---

### Matriz de Rastreabilidade (CA → Testes)

| CA | Tipo | Automação | Prioridade |
|----|------|-----------|------------|
| CA-01 a CA-05 | E2E/UI | Playwright | P0 |
| CA-10 a CA-13 | E2E/Contract | Playwright + Unit | P0 |
| CA-20 a CA-23 | E2E/Contract | Playwright + Unit | P0 |
| CA-30 a CA-36 | Integration | xUnit + TestServer | P0 |
| CA-40 a CA-43 | Infra | Manual/Script | P0 |

---

### Classificação de Resultados
- **Aprovado**: Critério verificado com evidência reproduzível
- **Reprovado**: Falha confirmada, comportamento não atende CA
- **Bloqueado**: Dependência não entregue, ambiente não pronto, falha crítica impede progresso
- **Não executado**: Fora de escopo atual, tempo insuficiente, risco aceito

---

### Evidências Requeridas (por execução)
- Comando exato executado
- Versão do código (commit hash)
- Ambiente (local/VPS, .NET version, Node version)
- Output completo (stdout/stderr)
- Screenshot/video para UI
- Logs estruturados (correlation ID)

---

### Limitações Conhecidas (Prévias)
- Sessões em memória: perdidas no restart (aceitável para POC)
- Sem autenticação: qualquer um acessa o chat
- Sem rate limiting avançado: vulnerável a abuso
- LLM streaming implementado (SSE) — resposta incremental
- Contexto multi-turno limitado a histórico da sessão (máx ~20 msgs)
- Geocoding Open-Meteo pode ser impreciso para cidades homônimas
- NVIDIA API requer conta em build.nvidia.com; OpenAI requer conta platform.openai.com
- Self-contained linux-x64 requer glibc compatível na VPS Hostinger

---

## Acceptance Test Plan (Functional Validation)

> **PRONTO PARA EXECUÇÃO** — Aguardando .env + build + start

### Checklist de Validação dos CAs (REQUISITOS.md)

#### CA-01 a CA-05: Chat Interface
- [ ] **CA-01** Carrega sem erros — abre `http://localhost:5000`, chat vazio, input focado, botão "Nova Conversa" visível, sem erros console
- [ ] **CA-02** Envia mensagem (Enter) / nova linha (Shift+Enter) — digita "Olá", Enter → bolha usuário + spinner "Gerando resposta..." + resposta assistente
- [ ] **CA-03** Botão "Nova Conversa" funciona — clica → histórico limpo, input limpo e focado, nova sessionId no backend
- [ ] **CA-04** Responsivo (320/768/1024) — Chrome DevTools device toolbar: layout adapta, input não coberto por teclado virtual, bolhas legíveis
- [ ] **CA-05** Acessível (ARIA, focus, contraste) — Tab navega input → botão → mensagens; focus visible; contraste ≥ 4.5:1; axe-core score > 90

#### CA-10 a CA-13: Consulta CEP
- [ ] **CA-10** CEP válido com hífen → "Qual o CEP 01310-100?" → resposta contém: "Avenida Paulista", "Bela Vista", "São Paulo", "SP"
- [ ] **CA-11** CEP válido sem hífen → "cep 01310100" → mesmo resultado CA-10 (normalização automática)
- [ ] **CA-12** CEP inválido → "CEP 99999-999" → "Não encontrei informações para o CEP 99999-999. Verifique se o CEP está correto."
- [ ] **CA-13** Linguagem natural → "qual o endereço do cep 01310-100?" → tool calling executado, resposta natural (não JSON cru)

#### CA-20 a CA-23: Consulta Tempo
- [ ] **CA-20** Cidade → "tempo em São Paulo" → temp atual, condição (ex: "céu limpo"), min/max hoje
- [ ] **CA-21** Cidade+UF → "como está o tempo em Rio de Janeiro, RJ?" → dados do RJ (não SP)
- [ ] **CA-22** CEP → tempo (encadeado) → "qual o tempo no CEP 01310-100?" → consultar_cep → extrai cidade/UF → consultar_previsao_tempo → resposta combinada
- [ ] **CA-23** Localidade inexistente → "tempo em Xyzabc123" → "Não consegui localizar 'Xyzabc123'. Tente informar cidade e estado."

#### CA-30 a CA-36: API
- [ ] **CA-30** POST /api/chat → sessionId + response — `curl -X POST /api/chat -d '{"sessionId":"...","message":"oi"}'` → 200 + JSON
- [ ] **CA-31** Sessão continua com mesmo sessionId — segunda request com mesmo sessionId → histórico mantido
- [ ] **CA-32** POST /api/chat/new → novo sessionId — `curl -X POST /api/chat/new` → 200 + novo GUID
- [ ] **CA-33** GET /api/health → healthy — `curl /api/health` → 200 + `{ "status": "healthy" }`
- [ ] **CA-34** Erro LLM → 503 ou resposta graciosa — simular falha (chave inválida) → "Serviço indisponível, tente novamente"
- [ ] **CA-35** Erro ViaCEP → mensagem amigável — simular timeout → "Não foi possível consultar o CEP no momento"
- [ ] **CA-36** Erro Open-Meteo → mensagem amigável — simular timeout → "Não foi possível obter a previsão do tempo"

#### CA-40 a CA-43: Não-funcionais
- [ ] **CA-40** Build self-contained linux-x64 — `scripts/build.sh` → `publish/backend/AgenticCepWeather.Api` (binário único)
- [ ] **CA-41** Scripts init/stop funcionam — `scripts/start.sh` sobe backend:5000; `scripts/stop.sh` para graciosamente
- [ ] **CA-42** Variáveis de ambiente (.env) carregadas — NVIDIA_API_KEY/OPENAI_API_KEY lidas do .env, não hardcoded
- [ ] **CA-43** Sem credenciais no código/logs — grep -r "sk-" /publish (deve falhar); logs não expõem keys

---

### Cenário Final Obrigatório (End-to-End)

**Teste 1: CEP → Tempo encadeado**
```
Usuário: "Qual a previsão do tempo para o CEP 01001-000 amanhã?"
↓
API: POST /api/chat/stream
↓
Orquestrador: 
  1. consultar_cep("01001-000") → { localidade: "São Paulo", uf: "SP" }
  2. consultar_previsao_tempo("São Paulo", "SP") → geocoding → forecast
↓
LLM: Resposta natural com tempo atual + previsão
```

**Teste 2: Continuação com contexto**
```
Usuário: "E depois de amanhã?"
↓
Orquestrador: Usa histórico da sessão (cidade/UF já conhecida)
↓
LLM: Resposta com previsão para depois de amanhã
```

---

### Registro de Evidências (Preencher durante validação)

| Item | Valor |
|------|-------|
| **Data/Hora** | |
| **Commit Hash** | `git rev-parse HEAD` |
| **Provedor LLM Usado** | NVIDIA / OpenAI (fallback) |
| **Tools Executadas** | consultar_cep, consultar_previsao_tempo |
| **CEP Testado** | 01310-100, 01001-000, 20040-002, 99999-999 |
| **Cidades Testadas** | São Paulo/SP, Rio de Janeiro/RJ, Belo Horizonte/MG, Xyzabc |
| **Resultados CA-01 a CA-43** | Todos Aprovados / Parcial / Reprovados |
| **Limitações Observadas** | |
| **Tempo Resposta Médio** | ~Xs |
| **Erros/Exceções** | |

---

### Go/No-Go Criteria (POC)

| Critério | Status |
|----------|--------|
| Todos P0 (CA-01 a CA-43) passam | ☐ Sim / ☐ Não |
| Zero bugs P0 abertos | ☐ Sim / ☐ Não |
| Build + deploy scripts funcionam | ☐ Sim / ☐ Não |
| Health check + logs OK | ☐ Sim / ☐ Não |
| Cenário final obrigatório funciona | ☐ Sim / ☐ Não |
| **DECISÃO FINAL** | **☐ GO / ☐ NO-GO** |

---

### Próximos Passos (QA)
1. **Configurar** `.env` com `NVIDIA_API_KEY` e/ou `OPENAI_API_KEY`
2. **Executar** `scripts/build.sh` (requer .NET 10 SDK + Node.js 18+ na máquina de build)
3. **Executar** `scripts/start.sh` → backend em `http://localhost:5000`
4. **Validar** todos os CAs acima via teste manual no navegador
5. **Registrar** evidências na tabela acima
6. **Atualizar** este handoff com resultados (append-only)
7. **Recomendar** GO/NO-GO para Rodrigo

---

**Fim do Handoff Inicial** — Atualizar conforme progresso (append-only).