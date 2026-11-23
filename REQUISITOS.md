# POC agentic-cep-weather-poc — Documento de Requisitos (PRD)

**Versão:** 1.0
**Data:** 2026-11-23
**Autor:** Polaris (Product Manager)
**Status:** Aprovado para desenvolvimento
**Aprovador:** Rodrigo
**Data aprovação:** 2026-11-23

---

## 1. Visão Geral

### 1.1 Objetivo da POC
Validar a viabilidade técnica e a experiência de usuário de um **chat conversacional agente** que utiliza **LLM + Tool Calling** para consultar:
- **ViaCEP** — Dados de endereço a partir de CEP brasileiro
- **Open-Meteo** — Previsão do tempo e condições atuais para coordenadas geográficas

### 1.2 Escopo do Produto (MVP)
Um chat web onde o usuário digita em linguagem natural (ex: "qual o tempo em São Paulo?" ou "me diga o endereço do CEP 01310-100") e o agente:
1. Interpreta a intenção
2. Chama a tool apropriada (ViaCEP ou Open-Meteo)
3. Processa a resposta
4. Responde em linguagem natural com os dados obtidos

### 1.3 Fora de Escopo (Explícito)
- Autenticação/autorização de usuários
- Persistência de histórico (sessões em memória apenas)
- Múltiplas sessões simultâneas por usuário
- Cache de respostas das APIs externas
- Rate limiting avançado (apenas básico)
- Interface de administração/monitoramento
- Testes de carga/performance
- Deploy automatizado em produção (apenas scripts manuais)
- Internacionalização (apenas PT-BR)
- Streaming de resposta do LLM (resposta completa por vez)
- Ferramentas além de ViaCEP e Open-Meteo
- Contexto de conversa multi-turno complexo (apenas histórico simples da sessão)

---

## 2. Cenários de Utilização (User Journeys)

### 2.1 Jornada Principal: Consulta de CEP
```
Usuário abre o chat
    ↓
Digita: "Qual o endereço do CEP 01310-100?"
    ↓
Agente identifica intenção → chama ViaCEP
    ↓
ViaCEP retorna: logradouro, bairro, cidade, UF
    ↓
Agente formula resposta natural: "O CEP 01310-100 corresponde à Avenida Paulista, 
Bela Vista, São Paulo - SP."
    ↓
Exibe resposta no chat
```

### 2.2 Jornada Principal: Consulta de Tempo
```
Usuário abre o chat
    ↓
Digita: "Como está o tempo em São Paulo hoje?"
    ↓
Agente identifica intenção → precisa de coordenadas
    ↓
Agente resolve "São Paulo" → lat/lon (geocoding simples via Open-Meteo ou fallback)
    ↓
Chama Open-Meteo (current_weather + forecast)
    ↓
Open-Meteo retorna: temperatura, vento, condição, previsão
    ↓
Agente formula resposta natural: "Em São Paulo agora faz 24°C, céu limpo, vento 
de 12 km/h. Previsão para hoje: mínima 18°C, máxima 28°C."
    ↓
Exibe resposta no chat
```

### 2.3 Jornada Combinada: CEP → Tempo
```
Usuário: "Qual o tempo no CEP 01310-100?"
    ↓
Agente chama ViaCEP → obtém cidade/UF
    ↓
Agente usa cidade/UF para buscar coordenadas
    ↓
Agente chama Open-Meteo
    ↓
Resposta combinada: endereço + tempo atual
```

### 2.4 Jornada de Erro: CEP Inválido
```
Usuário: "CEP 99999-999"
    ↓
ViaCEP retorna erro/empty
    ↓
Agente: "Não encontrei informações para o CEP 99999-999. Verifique se o CEP 
está correto (formato: 00000-000)."
```

### 2.5 Jornada de Erro: Localidade Não Encontrada (Tempo)
```
Usuário: "Tempo em Xyzabc123"
    ↓
Geocoding falha
    ↓
Agente: "Não consegui localizar 'Xyzabc123'. Tente informar cidade e estado 
(ex: São Paulo, SP)."
```

---

## 3. Requisitos Funcionais

### 3.1 Chat Interface (Frontend)
| ID | Requisito | Detalhes |
|----|-----------|----------|
| RF-01 | Interface de chat single-page | React + TypeScript, build estático |
| RF-02 | Campo de entrada de texto | Enter para enviar, Shift+Enter para nova linha |
| RF-03 | Exibição de mensagens | Bolhas diferenciadas (usuário vs agente), timestamp |
| RF-04 | Indicador de "digitando" | Spinner/skeleton enquanto aguarda resposta do backend |
| RF-05 | Histórico da sessão | Mantido em memória no frontend durante a sessão |
| RF-06 | Botão "Nova conversa" | Limpa histórico local e notifica backend para nova sessão |
| RF-07 | Responsivo | Mobile-first, funciona em 320px+ |
| RF-08 | Acessibilidade básica | ARIA labels, contraste, navegação por teclado |

### 3.2 Backend API (ASP.NET Core Minimal APIs)
| ID | Requisito | Detalhes |
|----|-----------|----------|
| RF-10 | Endpoint POST /api/chat | Recebe `{ sessionId?, message: string }`, retorna `{ sessionId, response: string }` |
| RF-11 | Endpoint POST /api/chat/new | Cria nova sessão, retorna `{ sessionId }` |
| RF-12 | Endpoint GET /health | Health check para nginx/load balancer |
| RF-13 | Sessões em memória | Dictionary<Guid, ChatSession> com TTL (ex: 30 min inatividade) |
| RF-14 | Injeção de dependências | HttpClient para ViaCEP/Open-Meteo, serviço de LLM |
| RF-15 | Logs estruturados | Serilog (console + arquivo), correlation ID por request |

### 3.3 Orquestração do Agente (LLM + Tools)
| ID | Requisito | Detalhes |
|----|-----------|----------|
| RF-20 | Provedor LLM | Configurável: OpenAI (GPT-4o-mini), Anthropic (Claude Haiku), ou local (Ollama) |
| RF-21 | Tool Calling nativo | Usar function calling do provedor (não parsing manual) |
| RF-22 | Tool: ViaCEP | `get_cep(cep: string)` → busca endereço, valida formato 00000-000 |
| RF-23 | Tool: Open-Meteo Geocoding | `geocode(city: string, state?: string)` → lat/lon |
| RF-24 | Tool: Open-Meteo Weather | `get_weather(lat: number, lon: number)` → current + daily forecast |
| RF-25 | System Prompt | Define personalidade, instruções de uso das tools, formato de resposta |
| RF-26 | Histórico de conversa | Enviar últimas N mensagens (ex: 10) para contexto do LLM |
| RF-27 | Tratamento de erros de tool | Capturar falhas de rede/API, retornar mensagem amigável ao usuário |
| RF-28 | Timeout de tool | 10s por chamada, fallback gracioso |

### 3.4 Integração ViaCEP
| ID | Requisito | Detalhes |
|----|-----------|----------|
| RF-30 | Endpoint | `https://viacep.com.br/ws/{cep}/json/` |
| RF-31 | Validação de entrada | Regex `^\d{5}-?\d{3}$`, normalizar para `00000-000` |
| RF-32 | Campos retornados | cep, logradouro, complemento, bairro, localidade, uf, ibge, gia, ddd, siafi |
| RF-33 | Tratamento "erro": true | ViaCEP retorna `{ "erro": true }` para CEP inexistente |

### 3.5 Integração Open-Meteo
| ID | Requisito | Detalhes |
|----|-----------|----------|
| RF-40 | Geocoding | `https://geocoding-api.open-meteo.com/v1/search?name={city}&count=1&language=pt&format=json` |
| RF-41 | Weather Atual | `https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true&timezone=America/Sao_Paulo` |
| RF-42 | Forecast Diário | `daily=temperature_2m_max,temperature_2m_min,weathercode,precipitation_probability_max&timezone=America/Sao_Paulo` |
| RF-43 | Mapeamento weathercode | Converter código WMO para descrição PT-BR (ex: 0="Céu limpo", 61="Chuva forte") |

---

## 4. Requisitos Não-Funcionais

| ID | Categoria | Requisito |
|----|-----------|-----------|
| RNF-01 | Performance | Resposta do chat < 5s (P95) em condições normais |
| RNF-02 | Disponibilidade | Backend reinicia automaticamente via systemd (se configurado) |
| RNF-03 | Segurança | Variáveis sensíveis (API keys) apenas em env vars, nunca no código |
| RNF-04 | Observabilidade | Logs com nível configurável, health check endpoint |
| RNF-05 | Manutenibilidade | Código limpo, separação de camadas, testes unitários core |
| RNF-06 | Portabilidade | Build self-contained linux-x64, roda em qualquer VPS Linux |
| RNF-07 | Custo | Zero custo de infra além da VPS (APIs gratuitas, LLM pago por uso) |

---

## 5. Critérios de Aceite (Verificáveis)

### 5.1 Critérios de Aceite — Chat Interface
| Cenário | Dado que | Quando | Então |
|---------|----------|--------|-------|
| CA-01 | Usuário abre a página | Carrega | Chat vazio, campo de input focado, botão "Nova conversa" visível |
| CA-02 | Usuário digita mensagem válida e envia | Pressiona Enter | Mensagem aparece na bolha do usuário, indicador "digitando" aparece, resposta do agente aparece após processamento |
| CA-03 | Usuário clica "Nova conversa" | Clica botão | Histórico limpo, nova sessionId gerada, input limpo e focado |
| CA-04 | Usuário redimensiona janela | 320px a 1920px | Layout adapta, mensagens legíveis, input acessível |
| CA-05 | Usuário navega por teclado | Tab/Shift+Tab | Foco move entre input, botão nova conversa, mensagens (ARIA) |

### 5.2 Critérios de Aceite — Consulta CEP
| Cenário | Dado que | Quando | Então |
|---------|----------|--------|-------|
| CA-10 | Usuário pergunta "CEP 01310-100" | Envia mensagem | Resposta contém: "Avenida Paulista", "Bela Vista", "São Paulo", "SP" |
| CA-11 | Usuário pergunta "cep 01310100" (sem hífen) | Envia mensagem | Resposta igual ao CA-10 (normalização automática) |
| CA-12 | Usuário pergunta "CEP 99999-999" | Envia mensagem | Resposta: "Não encontrei informações para o CEP 99999-999..." |
| CA-13 | Usuário pergunta "qual o endereço do cep 01310-100?" | Envia mensagem | Resposta em linguagem natural, não JSON cru |

### 5.3 Critérios de Aceite — Consulta Tempo
| Cenário | Dado que | Quando | Então |
|---------|----------|--------|-------|
| CA-20 | Usuário pergunta "tempo em São Paulo" | Envia mensagem | Resposta contém: temperatura atual, condição (ex: "céu limpo"), previsão min/max hoje |
| CA-21 | Usuário pergunta "como está o tempo em Rio de Janeiro, RJ?" | Envia mensagem | Resposta com dados do RJ (não SP) |
| CA-22 | Usuário pergunta "tempo no CEP 01310-100" | Envia mensagem | Resposta combina endereço (via ViaCEP) + tempo (via Open-Meteo) |
| CA-23 | Usuário pergunta "tempo em Xyzabc" | Envia mensagem | Resposta: "Não consegui localizar 'Xyzabc'..." |

### 5.4 Critérios de Aceite — Backend/API
| Cenário | Dado que | Quando | Então |
|---------|----------|--------|-------|
| CA-30 | POST /api/chat com message válido | Request | HTTP 200, JSON `{ sessionId, response }` |
| CA-31 | POST /api/chat sem sessionId | Request | Nova sessão criada, sessionId retornado |
| CA-32 | POST /api/chat com sessionId existente | Request | Mesma sessão continua, histórico mantido |
| CA-33 | POST /api/chat/new | Request | HTTP 200, JSON `{ sessionId }` novo |
| CA-34 | GET /health | Request | HTTP 200, `{ status: "healthy" }` |
| CA-35 | LLM indisponível | Request | HTTP 503 ou resposta graciosa "Serviço indisponível, tente novamente" |
| CA-36 | ViaCEP indisponível | Consulta CEP | Resposta: "Não foi possível consultar o CEP no momento" |

### 5.5 Critérios de Aceite — Infraestrutura
| Cenário | Dado que | Quando | Então |
|---------|----------|--------|-------|
| CA-40 | Build backend | `dotnet publish -c Release -r linux-x64 --self-contained` | Binário único em `bin/Release/net10.0/linux-x64/publish/` |
| CA-41 | Build frontend | `npm run build` | Arquivos estáticos em `dist/` (index.html, assets/) |
| CA-42 | Script init.sh | Executa | Backend roda na porta 5000, nginx serve frontend na 80/443 |
| CA-43 | systemd (se admin) | `systemctl start agentic-cep-weather` | Serviço sobe, reinicia em falha, logs no journalctl |

---

## 6. Arquitetura de Dados (Memória)

```csharp
// Sessão de chat em memória
class ChatSession {
    Guid Id { get; }
    DateTime CreatedAt { get; }
    DateTime LastActivityAt { get; }
    List<ChatMessage> Messages { get; }  // máx 20 mensagens (10 pares user/agent)
}

class ChatMessage {
    string Role { get; }  // "user" | "assistant" | "system"
    string Content { get; }
    DateTime Timestamp { get; }
    List<ToolCall>? ToolCalls { get; }  // para debug/auditoria
}

// Armazenamento: ConcurrentDictionary<Guid, ChatSession>
// Limpeza: Background service remove sessões > 30min inativas
```

---

## 7. Configuração (Variáveis de Ambiente)

```bash
# Backend
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5000

# LLM Provider (escolher um)
LLM_PROVIDER=OpenAI              # OpenAI | Anthropic | Ollama
LLM_API_KEY=sk-...               # Obrigatório para OpenAI/Anthropic
LLM_MODEL=gpt-4o-mini            # ou claude-3-haiku, llama3.1
LLM_BASE_URL=                    # Opcional: para Ollama/local

# Timeouts
HTTP_TIMEOUT_SECONDS=10
LLM_TIMEOUT_SECONDS=30

# Session
SESSION_TTL_MINUTES=30
MAX_HISTORY_MESSAGES=20

# Logging
SERILOG_MINIMUM_LEVEL=Information
```

---

## 8. Backlog Priorizado (MVP)

| Prioridade | Item | Tipo | Estimativa | Dependências |
|------------|------|------|------------|--------------|
| P0 | Setup projeto .NET 10 + React TS | Setup | 2h | — |
| P0 | Endpoint POST /api/chat + sessões memória | Backend | 4h | Setup |
| P0 | Integração ViaCEP (tool) | Backend | 2h | Backend base |
| P0 | Integração Open-Meteo Geocoding + Weather (tools) | Backend | 3h | Backend base |
| P0 | Orquestrador LLM + Tool Calling (System Prompt) | Backend | 4h | Tools prontas |
| P0 | Frontend: Chat UI (input, bolhas, loading, nova conversa) | Frontend | 6h | Setup |
| P0 | Conexão Frontend → Backend (proxy Vite/dev, nginx/prod) | Integração | 2h | Ambos prontos |
| P0 | Scripts build.sh, init.sh, stop.sh | DevOps | 3h | Código pronto |
| P0 | Testes manuais dos CAs (CA-01 a CA-43) | QA | 4h | Deploy local |
| P1 | Health check + logs estruturados | Backend | 2h | Backend base |
| P1 | Tratamento robusto de erros (timeouts, fallbacks) | Backend | 3h | Orquestrador |
| P1 | systemd service file (opcional) | DevOps | 1h | Scripts prontos |
| P2 | Testes unitários core (tools, normalização CEP) | Testes | 4h | Código estável |
| P2 | Validação acessibilidade básica (axe/lighthouse) | QA | 2h | Frontend pronto |
| P3 | Documentação README + arquitetura | Docs | 2h | MVP completo |

**Total estimado MVP (P0): ~30h**
**Total com P1+P2: ~41h**

---

## 9. Riscos e Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| LLM não chama tools corretamente | Média | Alto | System prompt iterativo, few-shot examples, validação de schema |
| ViaCEP/Open-Meteo instáveis | Baixa | Médio | Timeouts, retry (1x), fallback message amigável |
| Geocoding Open-Meteo impreciso | Média | Médio | Aceitar "cidade, UF" do usuário, validar resultado |
| Custo LLM em uso contínuo | Baixa | Baixo | POC = uso controlado, modelo barato (gpt-4o-mini/haiku) |
| Sessões em memória perdidas no restart | Certa | Baixo | Aceitável para POC, documentar limitação |
| Conflito de porta na VPS | Baixa | Médio | Configurar ASPNETCORE_URLS, nginx proxy_pass |

---

## 10. Validação com Stakeholders

| Papel | Nome | Validação | Status |
|-------|------|-----------|--------|
| Product Manager | Polaris | Escopo, CAs, Priorização | ✅ Autor |
| Tech Lead | Atlas | Arquitetura, Stack, Viabilidade | ⏳ Pendente |
| QA Lead | Sentinel | Cenários de teste, Critérios de aceite | ⏳ Pendente |
| Aprovador | Rodrigo | Aprovação final para iniciar | ⏳ Pendente |

---

## 11. Próximos Passos

1. **[Atlas]** Revisar arquitetura técnica, confirmar provedor LLM, validar ferramentas
2. **[Sentinel]** Revisar cenários de teste, sugerir casos de borda adicionais
3. **[Rodrigo]** Aprovar documento (este PRD) para início do desenvolvimento
4. **[Nexus]** Iniciar implementação seguindo backlog priorizado (P0 primeiro)
5. **[Polaris]** Acompanhar progresso, validar entregas incrementais contra CAs

---

## 12. Anexos

### A. System Prompt Base (Rascunho)
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

### B. Mapeamento Weathercode WMO → PT-BR (Resumo)
| Código | Descrição |
|--------|-----------|
| 0 | Céu limpo |
| 1, 2, 3 | Principalmente limpo, parcialmente nublado, nublado |
| 45, 48 | Neblina, neblina depositando geada |
| 51, 53, 55 | Chuvisco leve, moderado, forte |
| 61, 63, 65 | Chuva leve, moderada, forte |
| 71, 73, 75 | Neve leve, moderada, forte |
| 80, 81, 82 | Pancadas de chuva leve, moderada, forte |
| 95, 96, 99 | Tempestade, tempestade com granizo |

---

**Fim do Documento**