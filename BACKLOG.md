# POC agentic-cep-weather-poc — Backlog Priorizado

**Versão:** 1.0
**Data:** 2026-11-23
**Autor:** Polaris (Product Manager)
**Sprint:** 0 (MVP - Proof of Concept)
**Status:** Pronto para desenvolvimento

---

## 1. Visão do Backlog

Este backlog cobre o **MVP completo** da POC. Itens P0 são **bloqueadores** para demo funcional. Itens P1 são **qualidade/robustez**. Itens P2 são **nice-to-have** para POC.

> **Regra:** Nexus (Developer) pode puxar itens P0 em paralelo. Dependências técnicas explícitas indicadas.

---

## 2. Épicos e Histórias

### Épico 1: Fundação & Setup (P0)
| ID | Título | Tipo | Estimativa | Dependências | Critério Pronto |
|----|--------|------|------------|--------------|-----------------|
| E1-S1 | Criar solution .NET 10 (Api, Domain, Application, Infrastructure) | Task | 1h | — | `dotnet build` passa |
| E1-S2 | Criar projeto React + TypeScript + Vite + Tailwind | Task | 1h | — | `npm run dev` sobe |
| E1-S3 | Configurar ESLint + Prettier + Husky (frontend) | Task | 30min | E1-S2 | `npm run lint` passa |
| E1-S4 | Configurar EditorConfig + .gitignore unificado | Task | 15min | — | Commit limpo |
| E1-S5 | README inicial com arquitetura e comandos | Doc | 30min | E1-S1, E1-S2 | README.md existe |

### Épico 2: Backend Core (P0)
| ID | Título | Tipo | Estimativa | Dependências | Critério Pronto |
|----|--------|------|------------|--------------|-----------------|
| E2-S1 | Minimal API: Program.cs + DI + Health Check | Task | 1h | E1-S1 | GET /health → 200 |
| E2-S2 | Modelo de Sessão em Memória (ChatSession, ChatMessage) | Task | 1h | E2-S1 | Classes compilam |
| E2-S3 | SessionStore: ConcurrentDictionary + TTL cleanup (BackgroundService) | Task | 1.5h | E2-S2 | Sessões expiram em 30min |
| E2-S4 | Endpoint POST /api/chat (recebe message, opcional sessionId) | Task | 1h | E2-S3 | Retorna sessionId + response |
| E2-S5 | Endpoint POST /api/chat/new | Task | 30min | E2-S3 | Retorna novo sessionId |
| E2-S6 | Validação de entrada (FluentValidation ou manual) | Task | 1h | E2-S4 | 400 para message vazio/grande |
| E2-S7 | Logging Serilog (console + file, correlationId) | Task | 1h | E2-S1 | Logs JSON estruturados |
| E2-S8 | Configuração via Environment Variables (appsettings + env) | Task | 30min | E2-S1 | `LLM_PROVIDER`, `LLM_API_KEY` lidos |

### Épico 3: Integrações Externas (Tools) (P0)
| ID | Título | Tipo | Estimativa | Dependências | Critério Pronto |
|----|--------|------|------------|--------------|-----------------|
| E3-S1 | HttpClient ViaCEP (Typed Client, timeout, retry 1x) | Task | 1h | E2-S1 | `GET /ws/01310-100/json/` funciona |
| E3-S2 | Serviço ViaCEP: `GetAddressAsync(cep)` + normalização + validação | Task | 1h | E3-S1 | Retorna DTO ou NotFound |
| E3-S3 | HttpClient Open-Meteo Geocoding (Typed Client) | Task | 1h | E2-S1 | `GET /v1/search?name=...` funciona |
| E3-S4 | HttpClient Open-Meteo Weather (Typed Client) | Task | 1h | E2-S1 | `GET /v1/forecast?...` funciona |
| E3-S5 | Serviço Open-Meteo: `GeocodeAsync(city, uf?)` → lat/lon | Task | 1h | E3-S3 | Retorna coordenadas ou NotFound |
| E3-S6 | Serviço Open-Meteo: `GetWeatherAsync(lat, lon)` → current + daily | Task | 1h | E3-S4 | Retorna DTO tipado |
| E3-S7 | Mapeamento WeatherCode WMO → PT-BR (resource/dictionary) | Task | 1h | E3-S6 | 0="Céu limpo", 61="Chuva forte", etc. |

### Épico 4: Orquestrador Agente (LLM + Tool Calling) (P0)
| ID | Título | Tipo | Estimativa | Dependências | Critério Pronto |
|----|--------|------|------------|--------------|-----------------|
| E4-S1 | Interface `ILlmClient` + implementação OpenAI (Function Calling) | Task | 2h | E2-S8 | Compila, injeta via DI |
| E4-S2 | Implementação Anthropic (opcional, mesma interface) | Task | 1.5h | E4-S1 | Compila, troca por env var |
| E4-S3 | Implementação Ollama (opcional, mesma interface) | Task | 1.5h | E4-S1 | Compila, para uso local |
| E4-S4 | Definição de Tools (JSON Schema) para LLM: get_cep, geocode, get_weather | Task | 1h | E3-S2, E3-S5, E3-S6 | Schemas válidos |
| E4-S5 | System Prompt (personalidade, regras, few-shot examples) | Task | 1h | E4-S4 | Arquivo .txt/.md versionado |
| E4-S6 | Orquestrador: `ProcessMessageAsync(sessionId, message)` | Task | 2h | E2-S4, E4-S1, E4-S4 | Chama LLM → tools → resposta |
| E4-S7 | Histórico de conversa no contexto (últimas 10 msgs) | Task | 1h | E2-S2, E4-S6 | LLM recebe histórico |
| E4-S8 | Tratamento de erros de tool (timeout, falha rede) → fallback message | Task | 1h | E4-S6 | Mensagem amigável ao usuário |
| E4-S9 | Tool calling encadeado (CEP → cidade → geocode → weather) | Task | 1h | E4-S6 | Funciona em uma mensagem |

### Épico 5: Frontend Chat UI (P0)
| ID | Título | Tipo | Estimativa | Dependências | Critério Pronto |
|----|--------|------|------------|--------------|-----------------|
| E5-S1 | Layout base: Container, Header, MessagesArea, InputArea | Task | 1.5h | E1-S2 | Render sem erros |
| E5-S2 | Componente MessageBubble (user/agent, timestamp, markdown básico) | Task | 1h | E5-S1 | Exibe bolhas diferenciadas |
| E5-S3 | Componente InputArea (textarea, auto-resize, Enter/Shift+Enter) | Task | 1h | E5-S1 | Envia, nova linha |
| E5-S4 | Estado de "digitando" (skeleton/spinner) | Task | 30min | E5-S2 | Mostra enquanto aguarda API |
| E5-S5 | Botão "Nova Conversa" + confirmação (opcional) | Task | 30min | E5-S1 | Limpa estado local + API |
| E5-S6 | Hook `useChat` (sessionId, messages, sendMessage, newChat) | Task | 1.5h | E5-S2, E5-S3 | Gerencia estado + API calls |
| E5-S7 | API Client (Axios) com baseURL, interceptors, error handling | Task | 1h | E2-S4 | POST /api/chat funciona |
| E5-S8 | Responsivo: CSS Grid/Flex, breakpoints 320/768/1024 | Task | 1h | E5-S1 | Testado mobile/desktop |
| E5-S9 | Acessibilidade: ARIA labels, roles, focus visible, contraste | Task | 1h | E5-S1 | axe-core passa básico |

### Épico 6: Integração & Deploy (P0)
| ID | Título | Tipo | Estimativa | Dependências | Critério Pronto |
|----|--------|------|------------|--------------|-----------------|
| E6-S1 | Vite proxy config (/api → localhost:5000) para dev | Task | 15min | E1-S2, E2-S4 | Dev local funciona |
| E6-S2 | Script `build.sh` (backend publish + frontend build) | Task | 1h | E1-S1, E1-S2 | Gera `publish/` + `dist/` |
| E6-S3 | Script `init.sh` (sobe backend + nginx) | Task | 1h | E6-S2 | `./init.sh` → app no ar |
| E6-S4 | Script `stop.sh` (para processos) | Task | 30min | E6-S3 | `./stop.sh` → para tudo |
| E6-S5 | Nginx config (serve dist/, proxy /api → :5000, gzip, cache static) | Task | 1h | E6-S2 | Acesso / e /api funcionam |
| E6-S6 | systemd service file (opcional, se acesso sudo) | Task | 30min | E6-S3 | `systemctl start` funciona |
| E6-S7 | .env.example documentado | Task | 15min | E2-S8 | Variáveis listadas |

### Épico 7: Qualidade & Robustez (P1)
| ID | Título | Tipo | Estimativa | Dependências | Critério Pronto |
|----|--------|------|------------|--------------|-----------------|
| E7-S1 | Testes unitários: ViaCEP normalização/validação | Test | 1h | E3-S2 | xUnit passa |
| E7-S2 | Testes unitários: Open-Meteo mapeamento weathercode | Test | 1h | E3-S7 | xUnit passa |
| E7-S3 | Testes integração: SessionStore TTL, limpeza | Test | 1h | E2-S3 | xUnit passa |
| E7-S4 | Testes integração: Orquestrador com LLM mockado | Test | 1.5h | E4-S6 | xUnit passa |
| E7-S5 | Retry policy + Circuit Breaker (Polly) para HttpClients | Task | 1h | E3-S1, E3-S3, E3-S4 | Resiliente a falhas transitórias |
| E7-S6 | Rate limiting básico (IP + session, 30 req/min) | Task | 1h | E2-S4 | 429 se exceder |
| E7-S7 | Sanitização de input (anti-XSS no frontend) | Task | 30min | E5-S6 | DOMPurify ou similar |
| E7-S8 | Validação acessibilidade automatizada (axe-core no CI) | Task | 1h | E5-S9 | `npm run test:a11y` passa |

### Épico 8: Documentação & Handoff (P1/P2)
| ID | Título | Tipo | Estimativa | Dependências | Critério Pronto |
|----|--------|------|------------|--------------|-----------------|
| E8-S1 | README completo (arquitetura, comandos, variáveis, deploy) | Doc | 1h | Todos P0 | README.md produção |
| E8-S2 | Diagrama arquitetura (Mermaid no README) | Doc | 30min | E8-S1 | Renderiza no GitHub |
| E8-S3 | Handoff.md (template agentic-home) preenchido | Doc | 30min | MVP pronto | handoff.md na raiz |
| E8-S4 | Guia de troubleshooting (logs, erros comuns) | Doc | 1h | P1 estável | TROUBLESHOOTING.md |

---

## 3. Mapa de Dependências (Crítico para Paralelismo)

```
E1-S1 ──────┬──→ E2-S1 ──────┬──→ E2-S2 ──→ E2-S3 ──→ E2-S4 ──→ E2-S5
            │                │                        │
            │                ├────────────────────────┼──→ E4-S6 (Orquestrador)
            │                │                        │
            │                ├────────────────────────┼──→ E6-S1 (Vite proxy)
            │                │                        │
E1-S2 ──────┼──→ E5-S1 ──→ E5-S2 ──→ E5-S3 ──→ E5-S4 ──→ E5-S5 ──→ E5-S6 ──→ E5-S7 ──→ E5-S8 ──→ E5-S9
            │                │                        │                        │
            │                │                        │                        └──→ E6-S1
            │                │                        │
            │                └────────────────────────┼──→ E6-S2 (build.sh)
            │                                         │
E3-S1 ──→ E3-S2 ──┐                                  │
                  ├──→ E4-S4 (Tool Schemas) ──→ E4-S5 ──→ E4-S6 ──→ E4-S7 ──→ E4-S8 ──→ E4-S9
E3-S3 ──→ E3-S4 ──┘     (E3-S5, E3-S6, E3-S7)          │
E3-S5 ──→ E3-S6 ──→ E3-S7                              │
                                                   E6-S2 ──→ E6-S3 ──→ E6-S4 ──→ E6-S5 ──→ E6-S6
                                                                         │
                                                                         └──→ E6-S7
```

**Paralelização segura:**
- **Track A (Backend):** E1-S1 → E2-* → E3-* → E4-* (sequencial dentro do track)
- **Track B (Frontend):** E1-S2 → E5-* (independente do Track A até E6-S1)
- **Track C (DevOps):** E6-* depende de E6-S2 (build) que depende de ambos tracks

---

## 4. Estimativas por Fase

| Fase | Itens | Estimativa Total | Notas |
|------|-------|------------------|-------|
| **Setup & Fundação** (E1) | 5 | 3.5h | Base para tudo |
| **Backend Core** (E2) | 8 | 7.5h | Core da API |
| **Tools/Integrações** (E3) | 7 | 7h | ViaCEP + Open-Meteo |
| **Agente/Orquestrador** (E4) | 9 | 11.5h | Coração da POC |
| **Frontend** (E5) | 9 | 8.5h | Chat UI completa |
| **Deploy/Integração** (E6) | 7 | 5h | Scripts + Nginx |
| **Qualidade** (E7) | 8 | 7.5h | Testes + resiliência |
| **Docs/Handoff** (E8) | 4 | 3h | Entregáveis finais |
| **TOTAL P0+P1** | **57** | **~53.5h** | ~7 dias úteis (1 dev) |

> **Nota:** Com 2 devs (1 backend, 1 frontend) + 1 DevOps parcial → **~3-4 dias** para MVP.

---

## 5. Definição de Pronto (Definition of Done) — POC

Um item está **Done** quando:
- [ ] Código implementado e compila (`dotnet build` / `npm run build`)
- [ ] Testes unitários/integração passam (se aplicável)
- [ ] Lint passa (`dotnet format --verify` / `npm run lint`)
- [ ] Funciona em ambiente local (dev)
- [ ] Critérios de aceite do PRD atendidos (verificados manualmente)
- [ ] Documentado no README se nova configuração/variável
- [ ] Commit convencional (`feat:`, `fix:`, `chore:`, `docs:`, `test:`)

---

## 6. Riscos Técnicos por Item (Para Nexus/Atlas)

| Item | Risco | Mitigação |
|------|-------|-----------|
| E4-S1 | Function calling schema inválido | Testar com OpenAI Playground primeiro, validar JSON Schema |
| E4-S6 | LLM não chama tools (hallucina resposta) | Few-shot examples no system prompt, temperature=0 |
| E4-S9 | Encadeamento falha (CEP → cidade) | Validar que ViaCEP retorna `localidade` + `uf` sempre |
| E3-S5 | Geocoding Open-Meteo ambíguo | Aceitar `cidade, UF` do usuário, validar `country=Brazil` |
| E2-S3 | Memory leak no SessionStore | TTL cleanup rodando a cada 5min, limite max sessões (ex: 1000) |
| E6-S5 | Nginx proxy_pass 502 | Health check backend antes de reload nginx |
| E5-S6 | Race condition sessionId | Frontend aguarda response antes de enviar próxima msg |

---

## 7. Próximas Ações Imediatas

| Ação | Responsável | Prazo | Status |
|------|-------------|-------|--------|
| Atlas validar arquitetura .NET 10 + LLM provider | Atlas | 2026-11-24 | ⏳ Pendente |
| Sentinel validar cenários de teste | Sentinel | 2026-11-24 | ⏳ Pendente |
| Rodrigo aprovar PRD + Backlog | Rodrigo | 2026-11-23 | ⏳ Pendente |
| Nexus iniciar E1-S1, E1-S2 (setup paralelo) | Nexus | 2026-11-23 | 🚀 Pronto para iniciar |
| Polaris criar handoff.md (template agentic-home) | Polaris | 2026-11-23 | 📋 Em andamento |

---

## 8. Métricas de Acompanhamento (Durante Sprint)

| Métrica | Target | Frequência |
|---------|--------|------------|
| Itens P0 concluídos / Total P0 | 100% | Diário |
| Bugs P0 abertos | 0 | Diário |
| Cobertura testes unitários (core) | ≥ 70% | Por PR |
| Build time (backend + frontend) | < 3min | Por commit |
| Deploy time (init.sh) | < 2min | Por deploy |

---

**Fim do Backlog**