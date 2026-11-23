# POC agentic-cep-weather-poc — Cenários de Teste para QA

**Versão:** 1.0
**Data:** 2026-11-23
**Autor:** Polaris (Product Manager)
**Validador:** Sentinel (QA Lead)
**Status:** Pendente validação

---

## 1. Estratégia de Testes

### 1.1 Tipos de Teste
| Tipo | Ferramenta | Responsável | Cobertura |
|------|------------|-------------|-----------|
| Testes Unitários | xUnit | Nexus (Dev) | Tools (ViaCEP, Open-Meteo), normalização, validadores |
| Testes de Integração | xUnit + TestServer | Nexus (Dev) | Endpoints API, orquestrador LLM (mockado) |
| Testes E2E Manuais | Navegador | Sentinel (QA) | Critérios de aceite (CA-01 a CA-43) |
| Testes de Acessibilidade | axe-core / Lighthouse | Sentinel (QA) | WCAG 2.1 AA básico |
| Testes de Carga (smoke) | k6 / hey | Opcional | 10 usuários simultâneos, 1 min |

### 1.2 Ambientes
| Ambiente | URL | Dados | Uso |
|----------|-----|-------|-----|
| Dev Local | http://localhost:5173 (Vite proxy) | Mock/Real | Desenvolvimento |
| Staging VPS | http://staging.cepweather.local | Real | Validação QA |
| Produção | https://cepweather.exemplo.com | Real | Uso real (pós-POC) |

---

## 2. Casos de Teste — Funcionais

### 2.1 Chat Interface (Frontend)

| ID | Título | Pré-condição | Passos | Resultado Esperado | Prioridade |
|----|--------|--------------|--------|-------------------|------------|
| TC-UI-01 | Carregamento inicial | Página aberta | 1. Acessar URL | Chat vazio, input focado, botão "Nova conversa" visível, sem erros console | P0 |
| TC-UI-02 | Enviar mensagem (Enter) | Chat carregado | 1. Digitar "Olá" 2. Pressionar Enter | Bolha usuário aparece, spinner "digitando" aparece, resposta agente aparece, spinner some | P0 |
| TC-UI-03 | Nova linha (Shift+Enter) | Chat carregado | 1. Digitar "Linha 1" 2. Shift+Enter 3. Digitar "Linha 2" 4. Enter | Duas linhas no input, envia como uma mensagem | P1 |
| TC-UI-04 | Botão "Nova conversa" | Conversa com histórico | 1. Clicar "Nova conversa" | Histórico limpo, input limpo e focado, nova sessionId no backend | P0 |
| TC-UI-05 | Responsividade mobile | Chrome DevTools device toolbar | 1. Testar 320px, 375px, 414px 2. Rotacionar landscape | Layout adapta, input não coberto por teclado virtual, bolhas legíveis | P0 |
| TC-UI-06 | Navegação por teclado | Página carregada | 1. Tab navega: input → botão nova conversa → mensagens (ARIA) 2. Enter no botão = nova conversa | Foco visível, ordem lógica, ações funcionam | P1 |
| TC-UI-07 | Mensagens longas | Chat carregado | 1. Enviar mensagem 500+ chars 2. Receber resposta 500+ chars | Scroll interno na bolha, não quebra layout | P1 |
| TC-UI-08 | Reconexão após erro de rede | Backend parado | 1. Enviar mensagem 2. Ver erro 3. Subir backend 4. Enviar novamente | Erro amigável, recupera automaticamente ou com retry manual | P1 |

### 2.2 Consulta CEP (ViaCEP)

| ID | Título | Entrada | Resultado Esperado | Prioridade |
|----|--------|---------|-------------------|------------|
| TC-CEP-01 | CEP válido com hífen | "CEP 01310-100" | Resposta contém: "Avenida Paulista", "Bela Vista", "São Paulo", "SP" | P0 |
| TC-CEP-02 | CEP válido sem hífen | "cep 01310100" | Mesmo resultado TC-CEP-01 (normalização) | P0 |
| TC-CEP-03 | CEP inexistente | "CEP 99999-999" | "Não encontrei informações para o CEP 99999-999. Verifique se o CEP está correto." | P0 |
| TC-CEP-04 | CEP formato inválido | "CEP 123" | "Formato de CEP inválido. Use 00000-000 ou 00000000." | P0 |
| TC-CEP-05 | CEP com letras | "CEP abcde-fgh" | "Formato de CEP inválido..." | P1 |
| TC-CEP-06 | Pergunta natural | "Qual o endereço do CEP 01310-100?" | Resposta natural com dados completos | P0 |
| TC-CEP-07 | CEP de outra UF | "CEP 20040-002" | "Praça Mauá, Centro, Rio de Janeiro, RJ" | P1 |
| TC-CEP-08 | CEP rural/caixa postal | "CEP 89999-999" | Dados retornados pelo ViaCEP (se existirem) | P2 |

### 2.3 Consulta Tempo (Open-Meteo)

| ID | Título | Entrada | Resultado Esperado | Prioridade |
|----|--------|---------|-------------------|------------|
| TC-WEA-01 | Cidade principal | "tempo em São Paulo" | Temp atual, condição (ex: "céu limpo"), min/max hoje | P0 |
| TC-WEA-02 | Cidade + UF | "como está o tempo em Rio de Janeiro, RJ?" | Dados do RJ (não SP) | P0 |
| TC-WEA-03 | Cidade ambígua | "tempo em Porto Alegre" | Dados de Porto Alegre, RS (capital) | P1 |
| TC-WEA-04 | Localidade inexistente | "tempo em Xyzabc123" | "Não consegui localizar 'Xyzabc123'. Tente informar cidade e estado." | P0 |
| TC-WEA-05 | Pergunta natural completa | "Qual a previsão do tempo para Belo Horizonte hoje?" | Temp atual + previsão hoje (min/max, chuva prob) | P0 |
| TC-WEA-06 | Apenas "tempo" (contexto) | 1. "tempo em SP" 2. "e amanhã?" | Mantém contexto da cidade, responde previsão amanhã | P1 |
| TC-WEA-07 | Cidade com acento | "tempo em São José dos Campos" | Funciona corretamente (encoding UTF-8) | P1 |

### 2.4 Consulta Combinada (CEP → Tempo)

| ID | Título | Entrada | Resultado Esperado | Prioridade |
|----|--------|---------|-------------------|------------|
| TC-CMB-01 | CEP → Tempo | "qual o tempo no CEP 01310-100?" | Endereço (Av Paulista, SP) + tempo atual de SP | P0 |
| TC-CMB-02 | CEP inválido → Tempo | "tempo no CEP 99999-999" | Erro do CEP, não tenta tempo | P0 |
| TC-CMB-03 | CEP válido, cidade sem tempo | "tempo no CEP 99999-999" (se existisse mas sem weather) | Endereço OK + "não consegui obter tempo" | P1 |

### 2.5 Histórico e Contexto de Sessão

| ID | Título | Passos | Resultado Esperado | Prioridade |
|----|--------|--------|-------------------|------------|
| TC-SES-01 | Continuidade de sessão | 1. "CEP 01310-100" 2. "tempo lá" | 2ª resposta usa contexto (São Paulo) | P0 |
| TC-SES-02 | Nova sessão limpa histórico | 1. Conversa com 3 msgs 2. "Nova conversa" 3. "tempo lá" | Não sabe contexto anterior, pede cidade | P0 |
| TC-SES-03 | Múltiplas abas | Abrir 2 abas, conversar em ambas | Sessões independentes (sessionIds diferentes) | P1 |
| TC-SES-04 | Expiração TTL | Aguardar 30+ min inativo, enviar msg | Nova sessão criada automaticamente | P2 |

---

## 3. Casos de Teste — API (Backend)

### 3.1 Endpoints

| ID | Título | Request | Expected Response | Prioridade |
|----|--------|---------|-------------------|------------|
| TC-API-01 | POST /api/chat sem sessionId | `{ "message": "oi" }` | 200, `{ "sessionId": "guid", "response": "..." }` | P0 |
| TC-API-02 | POST /api/chat com sessionId | `{ "sessionId": "guid", "message": "oi" }` | 200, mesmo sessionId, resposta contextual | P0 |
| TC-API-03 | POST /api/chat sessionId inválido | `{ "sessionId": "xxx", "message": "oi" }` | 200, nova sessão criada (graceful) | P1 |
| TC-API-04 | POST /api/chat message vazio | `{ "message": "" }` | 400, erro validação | P0 |
| TC-API-05 | POST /api/chat message > 2000 chars | `{ "message": "a..." }` | 400, erro validação | P1 |
| TC-API-06 | POST /api/chat/new | `{}` | 200, `{ "sessionId": "guid" }` | P0 |
| TC-API-07 | GET /health | — | 200, `{ "status": "healthy" }` | P0 |

### 3.2 Orquestrador LLM + Tools

| ID | Título | Cenário | Verificação | Prioridade |
|----|--------|---------|-------------|------------|
| TC-ORC-01 | Tool calling ViaCEP | Message "CEP 01310-100" | LLM chama get_cep, processa resposta, responde natural | P0 |
| TC-ORC-02 | Tool calling Open-Meteo (geocode + weather) | Message "tempo em SP" | LLM chama geocode → get_weather, responde natural | P0 |
| TC-ORC-03 | Tool calling encadeado | Message "tempo no CEP 01310-100" | LLM: get_cep → extrai cidade → geocode → get_weather | P0 |
| TC-ORC-04 | Tool falha (ViaCEP timeout) | Mock ViaCEP lento/erro | Resposta: "Não foi possível consultar o CEP no momento" | P0 |
| TC-ORC-05 | Tool falha (Open-Meteo erro) | Mock Open-Meteo 500 | Resposta: "Não foi possível obter a previsão do tempo" | P0 |
| TC-ORC-06 | LLM não usa tool (conversa fiada) | Message "oi, tudo bem?" | Resposta social sem tool calls | P1 |
| TC-ORC-07 | Múltiplas tools mesma msg | Message "CEP 01310-100 e tempo em RJ" | Duas tool calls independentes, resposta combinada | P1 |
| TC-ORC-08 | Histórico enviado ao LLM | 3 mensagens prévias | Últimas 10 mensagens no contexto do LLM | P1 |

---

## 4. Casos de Teste — Não-Funcionais

| ID | Título | Critério | Como Testar | Prioridade |
|----|--------|----------|-------------|------------|
| TC-NF-01 | Tempo resposta chat | < 5s (P95) | hey -n 100 -c 10 POST /api/chat | P1 |
| TC-NF-02 | Memória backend | < 200MB estável | Monitorar 1h com carga leve | P1 |
| TC-NF-03 | CPU backend | < 50% em idle | top/htop | P2 |
| TC-NF-04 | Build self-contained | Binário único linux-x64 | `dotnet publish -r linux-x64 --self-contained` | P0 |
| TC-NF-05 | Frontend build estático | Apenas arquivos em dist/ | `npm run build`, verificar dist/ | P0 |
| TC-NF-06 | Nginx serve frontend | Acesso / retorna index.html | curl -I http://localhost/ | P0 |
| TC-NF-07 | Proxy API nginx | /api/* → backend:5000 | curl http://localhost/api/health | P0 |
| TC-NF-08 | Variáveis de ambiente | Secrets não no código | grep -r "sk-" /app (deve falhar) | P0 |
| TC-NF-09 | Logs estruturados | JSON com correlationId | tail -f logs/app.log | P1 |
| TC-NF-10 | Health check k8s/docker | GET /health 200 | curl -f http://localhost:5000/health | P1 |

---

## 5. Casos de Teste — Acessibilidade (WCAG 2.1 AA)

| ID | Critério | Teste | Ferramenta |
|----|----------|-------|------------|
| TC-A11Y-01 | 1.1.1 Non-text Content | Ícones têm aria-label | axe-core |
| TC-A11Y-02 | 1.3.1 Info and Relationships | Heading structure (h1 → h2) | axe-core |
| TC-A11Y-03 | 2.1.1 Keyboard | Todos elementos interativos acessíveis por Tab | Manual |
| TC-A11Y-04 | 2.4.3 Focus Order | Ordem lógica de foco | Manual |
| TC-A11Y-05 | 2.4.7 Focus Visible | Outline visível em foco | Manual |
| TC-A11Y-06 | 4.1.2 Name, Role, Value | Botões/inputs têm role/name corretos | axe-core |
| TC-A11Y-07 | 1.4.3 Contrast | Ratio ≥ 4.5:1 texto normal | Lighthouse |
| TC-A11Y-08 | 1.4.4 Resize Text | Zoom 200% sem perda função | Manual |

---

## 6. Dados de Teste

### 6.1 CEPs Válidos para Testes
| CEP | Endereço Esperado | Cidade/UF |
|-----|-------------------|-----------|
| 01310-100 | Avenida Paulista | São Paulo/SP |
| 20040-002 | Praça Mauá | Rio de Janeiro/RJ |
| 30112-000 | Praça Sete | Belo Horizonte/MG |
| 40010-000 | Praça Municipal | Salvador/BA |
| 80010-000 | Praça Nossa Senhora | Curitiba/PR |
| 70010-000 | Setor de Diversões | Brasília/DF |

### 6.2 Cidades para Teste de Tempo
| Cidade, UF | Coordenadas Aprox. | Notas |
|------------|-------------------|-------|
| São Paulo, SP | -23.55, -46.63 | Capital, geocoding preciso |
| Rio de Janeiro, RJ | -22.91, -43.20 | Capital |
| Porto Alegre, RS | -30.03, -51.23 | Capital sul |
| Manaus, AM | -3.12, -60.02 | Norte, clima tropical |
| Florianópolis, SC | -27.59, -48.55 | Ilha, costa |

---

## 7. Checklist de Execução (QA)

### 7.1 Pré-Deploy (Local/Staging)
- [ ] Backend compila sem warnings (`dotnet build`)
- [ ] Frontend builda sem erros (`npm run build`)
- [ ] Testes unitários passam (`dotnet test`)
- [ ] Variáveis de ambiente configuradas (.env / env vars)
- [ ] Health check responde 200
- [ ] Logs aparecem no console/arquivo

### 7.2 Pós-Deploy (Staging)
- [ ] TC-UI-01 a TC-UI-08 (Chat Interface)
- [ ] TC-CEP-01 a TC-CEP-08 (CEP)
- [ ] TC-WEA-01 a TC-WEA-07 (Tempo)
- [ ] TC-CMB-01 a TC-CMB-03 (Combinado)
- [ ] TC-SES-01 a TC-SES-04 (Sessão)
- [ ] TC-API-01 a TC-API-07 (API)
- [ ] TC-ORC-01 a TC-ORC-08 (Orquestrador)
- [ ] TC-NF-01 a TC-NF-10 (Não-funcionais)
- [ ] TC-A11Y-01 a TC-A11Y-08 (Acessibilidade)

### 7.3 Critérios de Go/No-Go para Produção (POC)
| Critério | Obrigatório? |
|----------|--------------|
| Todos P0 passam | ✅ Sim |
| Zero bugs P0 abertos | ✅ Sim |
| Build + deploy scripts funcionam | ✅ Sim |
| Health check + logs OK | ✅ Sim |
| P1 ≥ 80% passam | ⚠️ Desejável |
| Acessibilidade ≥ 90% axe | ⚠️ Desejável |

---

## 8. Relatório de Defeitos (Template)

```markdown
## Bug: [Título curto]

**ID:** BUG-XXX
**Severidade:** Crítica | Alta | Média | Baixa
**Prioridade:** P0 | P1 | P2 | P3
**Ambiente:** Local / Staging / Prod
**Browser/OS:** Chrome 119 / Ubuntu 22.04

### Passos para Reproduzir
1. ...
2. ...
3. ...

### Resultado Atual
...

### Resultado Esperado
...

### Evidência
- Screenshot:
- Log excerpt:
- Network payload:

### Contexto Adicional
...
```

---

## 9. Aprovação

| Papel | Nome | Assinatura | Data |
|-------|------|------------|------|
| Product Manager | Polaris | | |
| Tech Lead | Atlas | | |
| QA Lead | Sentinel | | |
| Aprovador | Rodrigo | | |

---

**Fim do Documento**