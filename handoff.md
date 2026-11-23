# Task: POC agentic-cep-weather-poc — PM Requirements + QA Planning (MVP)

- **Project**: agentic-cep-weather-poc
- **Owner**: Polaris (Product Manager) + Sentinel (QA Engineer)
- **Status**: in-progress (PM done, QA planning, Dev ready to start)
- **Approved**: PRD, Cenários de Teste e Backlog criados — 2026-11-23 — Aguardando validação de Atlas (Tech Lead), Sentinel (QA) e aprovação de Rodrigo
- **Waiting**: Validação técnica (Atlas), Validação de QA (Sentinel), Aprovação final (Rodrigo)
- **Next action**: Nexus iniciar implementação P0 (Setup + Backend Core + Frontend) em paralelo após aprovação

---

## Artifacts
- /opt/data/projects/agentic-cep-weather-poc/REQUISITOS.md (PRD aprovado)
- /opt/data/projects/agentic-cep-weather-poc/CENÁRIOS_TESTE.md (54 casos de teste, matriz CA→Testes, checklist Go/No-Go)
- /opt/data/projects/agentic-cep-weather-poc/BACKLOG.md (8 épicos, 57 itens, ~53.5h, tracks paralelos, DoD, riscos)
- /opt/data/projects/agentic-cep-weather-poc/backend/AgenticCepWeather.slnx
- /opt/data/projects/agentic-cep-weather-poc/backend/src/AgenticCepWeather.Domain/ (models, interfaces, settings)
- /opt/data/projects/agentic-cep-weather-poc/frontend/ (vazio)

---

## History
- **2026-11-23 21:00** | planejamento | QA Sentinel iniciou planejamento da estratégia de testes baseada no PRD v1.0 aprovado. Projeto em estágio inicial: apenas Domain layer implementado.
- **2026-11-23 21:20** | PRD | REQUISITOS.md criado: visão, escopo MVP, exclusões, 5 jornadas, 28 RFs, 7 RNFs, 43 CAs verificáveis, arquitetura sessão memória, env vars, riscos
- **2026-11-23 21:35** | Test Scenarios | CENÁRIOS_TESTE.md criado: estratégia, 54 casos (UI, CEP, Tempo, Combinado, Sessão, API, Orquestrador, NFR, A11y), dados de teste reais, checklist Go/No-Go, template bug
- **2026-11-23 21:50** | Backlog | BACKLOG.md criado: 8 épicos, 57 stories/tasks, mapa dependências (3 tracks paralelos), ~53.5h, DoD POC, riscos por item, próximas ações
- **2026-11-23 21:55** | Handoff | Atualização deste handoff com artefatos PM (append-only)

---

## Validation
- **Checks executed**: leitura do REQUISITOS.md, inspeção da estrutura do backend (Domain apenas), verificação de ausência de testes
- **Results**: PRD completo com 43 Critérios de Aceite (CA-01 a CA-43), contratos de tools definidos (ICepService, IWeatherService, IToolExecutor), zero código executável, zero testes
- **Pending**: implementação completa (Nexus), build funcional, deploy local para validação

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
- LLM streaming não implementado (resposta completa por vez)
- Contexto multi-turno limitado a 10 pares user/agent
- Geocoding Open-Meteo pode ser impreciso para cidades homônimas

---

### Próximos Passos (QA)
1. **Aguardar** Nexus entregar primeiro build funcional (P0 completo)
2. **Criar** projeto de testes xUnit (backend) e Vitest (frontend)
3. **Implementar** testes unitários de contratos (ICepService, IWeatherService, IToolExecutor)
4. **Configurar** Playwright para E2E
5. **Executar** validação contra CAs após deploy local
6. **Registrar** evidências no handoff (append-only)
7. **Recomendar** aprovação/bloqueio por CA

---

### Regras de Engajamento
- Não bloquear desenvolvimento — preparar estratégia agora, executar quando houver código
- Diferenciar testes meus (QA) vs evidências do Developer (unit tests do Nexus)
- Não tratar ausência de testes como aprovação
- Bloquear apenas se falha crítica conhecida OU critério obrigatório (P0) não atendido
- Comunicar riscos residuais e limitações explicitamente

---

### Referências
- PRD: REQUISITOS.md (43 CAs, 8 jornadas, 9 riscos)
- Cenários de Teste: CENÁRIOS_TESTE.md (54 casos, matriz CA→Testes, checklist Go/No-Go)
- Backlog: BACKLOG.md (8 épicos, 57 itens, ~53.5h, tracks paralelos, DoD, riscos técnicos)
- ADR pendente: escolha final LLM provider (NVIDIA primary + OpenAI fallback)
- agentic-home skills: `qualidade` (validação, testes, revisão), `desenvolvimento` (padrões implementação)
- Skills carregadas: `test-driven-development`, `systematic-debugging`

---

### Validação de Checklist (para quando houver código)

#### Backend
- [ ] `dotnet build` passa sem warnings/errors
- [ ] `dotnet test` (unit) > 80% coverage core (tools, normalização, orchestration)
- [ ] POST /api/chat responde 200 com sessionId e response string
- [ ] Sessão mantém histórico (até 20 msgs)
- [ ] ViaCEP: normaliza CEP, trata erro:true, timeout 10s
- [ ] Open-Meteo: geocoding + weather, weathercode mapping, timeout 10s
- [ ] LLM: tool calling nativo, system prompt, fallback NVIDIA→OpenAI
- [ ] Health check: GET /health → 200 healthy

#### Frontend
- [ ] `npm run build` gera dist/ sem erros
- [ ] Chat carrega, input focado, botão nova conversa visível
- [ ] Enter envia, Shift+Enter nova linha
- [ ] Bolhas user/agent diferenciadas, timestamps
- [ ] Loading indicator durante resposta
- [ ] Nova conversa limpa histórico local + notifica backend
- [ ] Responsivo 320px-1920px
- [ ] Acessibilidade: axe score > 90

#### Infra
- [ ] Publish self-contained linux-x64 gera binário único
- [ ] init.sh sobe backend:5000 + nginx:80
- [ ] stop.sh para tudo limpo
- [ ] Variáveis sensíveis apenas em .env / env vars

#### E2E (Playwright)
- [ ] CA-01 a CA-43 todos passando
- [ ] Jornadas de erro (CEP inválido, localidade inexistente) tratadas graciosamente
- [ ] Fallback LLM testado (simular falha NVIDIA)
- [ ] Performance: resposta < 5s P95 (amostral)

---

**Fim do Handoff Inicial** — Atualizar conforme progresso (append-only).