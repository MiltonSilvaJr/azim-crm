# Module — Partner Management

**Status:** Implementado
**Fase:** Fase 1 MVP
**Versão dos artefatos:** requirements.md v1.0.0 / design.md v0.1.0 / tasks.md v0.1.0

---

## 1. Visão Geral

Módulo responsável pela gestão de parceiros comissionados. Mantém o cadastro de parceiros com percentuais de comissão default por componente (setup e recorrente). Serve como upstream do Opportunity Pipeline para vinculação de parceiro e cálculo de comissão. No MVP, parceiros não possuem login na plataforma (RN-021).

Implementado em 6 ondas (29 TASKs), com Clean Architecture, DDD, Property-Based Testing (PBT-01..05), mascaramento de PII, resiliência Polly e observabilidade OpenTelemetry + Prometheus.

---

## 2. Classificação

| Item | Valor |
|---|---|
| Tipo de Módulo | Application Module |
| Deployable Candidato | azim-api |
| Bounded Context Relacionado | Partner Management (BC-03) |
| Subdomínio DDD | Supporting Subdomain |
| Tier / Criticidade | Tier 2 — suporte ao pipeline; parceiro é pré-requisito para comissão nativa |
| Status | **Implementado** |

---

## 3. Objetivo

Prover o cadastro de parceiros com seus percentuais de comissão default por componente. Permitir que o pipeline vincule um parceiro a uma oportunidade, herde os percentuais default e calcule a comissão de forma imutável no fechamento (snapshot). Expor visão de comissão projetada e consolidada por parceiro.

---

## 4. Responsabilidades

- CRUD de parceiros com tipo, percentuais default (pct_setup, pct_recorrente) e status ativo/inativo.
- Expor dados de parceiro (partner_id, pct_setup, pct_recorrente) para o pipeline na vinculação.
- Expor relatório de comissão consolidada por parceiro (parceiro × oportunidades ganhas × comissão calculada).
- Garantir que parceiro desativado não seja vinculado a novas oportunidades.
- Publicar AuditEvent em toda escrita via Outbox transacional.
- Mascarar PII (name, contact_email, contact_phone) em logs, traces e delta_json de auditoria.

---

## 5. Fora de Escopo

- Cálculo e snapshot de comissão por oportunidade (pertence ao opportunity-pipeline — RN-026, RN-007).
- Login de parceiro na plataforma (RN-021 — fora do MVP).
- Portal do parceiro ou autoatendimento (fora do MVP).
- Pagamento de comissão (fora do escopo do sistema).

---

## 6. Capacidades Atendidas

| Código | Capability | Descrição |
|---|---|---|
| CAP-05 | Gestão de Parceiros | CRUD de parceiros; visão de comissão projetada e consolidada |

---

## 7. Implementação — Ondas e TASKs

### Resumo das Ondas

| Onda | Objetivo | TASKs |
|------|----------|-------|
| Onda 1 | Setup e domínio (Clean Architecture, DDD) | TASK-01..05 |
| Onda 2 | Application layer (commands, queries, behaviors) | TASK-06..14 |
| Onda 3 | Testes unitários e PBT | TASK-14 |
| Onda 4 | Infrastructure (EF Core, RLS, Outbox, ReadAdapter) | TASK-15..21 |
| Onda 5 | API REST (Controllers, RBAC, integração) | TASK-22..25 |
| Onda 6 | Hardening (observabilidade, PII, resiliência, DoD) | TASK-26..29 |

### Property-Based Tests implementados

| Código | Propriedade verificada | Camada |
|--------|----------------------|--------|
| PBT-01 | Conservação da soma de comissão (projetada + consolidada) | Application |
| PBT-02 | Idempotência de inativação/reativação | Domain |
| PBT-03 | Imutabilidade de Percentage | Domain |
| PBT-04 | Isolamento multi-tenant (gate CI obrigatório, KPI-06) | Infrastructure |
| PBT-05 | Imutabilidade da comissão consolidada (snapshot) | Application |

---

## 8. Bounded Context e Linguagem Ubíqua

| Termo | Definição |
|---|---|
| Partner | Empresa ou pessoa que indica clientes e recebe comissão por vendas fechadas |
| pct_setup | Percentual de comissão sobre o valor de setup do negócio (default por parceiro) |
| pct_recorrente | Percentual de comissão sobre o valor mensal × meses comissionados (default por parceiro) |
| CommissionDefaults | Objeto de valor com pct_setup e pct_recorrente padrão do parceiro; herdado pela oportunidade na vinculação |
| comissão projetada | Soma de comissões calculadas em oportunidades abertas vinculadas ao parceiro |
| comissão consolidada | Soma de comissões em snapshots imutáveis de oportunidades ganhas |
| RN-021 | Parceiro não possui login na plataforma no MVP |

---

## 9. APIs Expostas

| Método | Endpoint | Finalidade | RBAC |
|--------|----------|------------|------|
| GET | /api/v1/partners | Lista parceiros paginada | Viewer+ |
| POST | /api/v1/partners | Criar parceiro | TAdmin, GestorBU |
| GET | /api/v1/partners/{id} | Detalhes do parceiro | Viewer+ |
| PATCH | /api/v1/partners/{id} | Atualizar parceiro | TAdmin, GestorBU |
| POST | /api/v1/partners/{id}/deactivate | Desativar parceiro | TAdmin, GestorBU |
| POST | /api/v1/partners/{id}/reactivate | Reativar parceiro | TAdmin, GestorBU |
| GET | /api/v1/partners/{id}/eligibility | Elegibilidade do parceiro | Viewer+ |
| GET | /api/v1/partners/{id}/commissions | Visão de comissão | TAdmin, GestorBU |

> **Nota RISK-PM-05 resolvido:** o endpoint de atualização usa `PATCH` (não `PUT`), conforme design.md §8 e TRD §8.4. README anterior mencionava `PUT` por erro — corrigido nesta versão.

OpenAPI disponível em `/swagger` (Development) e nos artefatos de contrato em `contracts/openapi/`.

---

## 10. Eventos Publicados

| Evento | Quando é publicado | Consumidores |
|--------|-------------------|--------------|
| partner.created.v1 | Após criação de parceiro | audit-log |
| partner.commission_percentages_updated.v1 | Após atualização de percentuais | audit-log |
| partner.deactivated.v1 | Após desativação | audit-log |
| partner.reactivated.v1 | Após reativação | audit-log |

Todos os eventos são publicados via Outbox transacional — zero PII em payload de evento (DD-008).

---

## 11. Eventos Consumidos

Este módulo não consome eventos diretamente.

---

## 12. Dados Próprios

| Entidade/Tabela | Tipo | Banco/Persistência | Observações |
|---|---|---|---|
| partners | Transacional | Cloud SQL / Postgres | pct_setup, pct_recorrente em NUMERIC(5,2); soft-delete via active=false; RLS habilitada |
| outbox_messages | Transacional | Cloud SQL / Postgres | Outbox de eventos de domínio |
| idempotency_keys | Transacional | Cloud SQL / Postgres | Chaves de idempotência de commands |

---

## 13. Integrações

| Sistema/Módulo | Tipo | Direção | Observações |
|---|---|---|---|
| opportunity-pipeline | API HTTP (read model) | Saída | PartnerCommissionReadAdapter com Polly (timeout/retry/circuit breaker) |
| audit-log | Outbox + Pub/Sub | Saída | Toda escrita gera evento de auditoria sem PII |

---

## 14. Dependências

### 14.1 Dependências de Domínio

- organization: tenant_id para isolamento por tenant.

### 14.2 Dependências Técnicas

- Cloud SQL / Postgres (tables: partners, outbox_messages, idempotency_keys)
- opportunity-pipeline API interna (read port de comissão)

### 14.3 Stack

- .NET 10 / C#, ASP.NET Core Minimal Hosting
- EF Core 9 + Npgsql 9 (migrations, global query filter, RLS interceptor ADR-0001)
- MediatR (pipeline behaviors: CorrelationLogging → TenantScope → Validation → Authorization → Transaction)
- FluentValidation, Polly 8, prometheus-net, OpenTelemetry, Swashbuckle
- xUnit, FluentAssertions, NSubstitute, FsCheck, Testcontainers

---

## 15. Observabilidade

| Item | Implementação |
|---|---|
| Logs estruturados | Serilog; campos: correlation_id, tenant_id, partner_id, action — sem PII |
| Métricas | Prometheus: partners_created_total, partners_deactivated_total, partners_reactivated_total, partner_commission_view_duration_seconds |
| Traces | OpenTelemetry ActivitySource: PartnerManagement.GetPartnerCommissionView |
| Health checks | /healthz/live (liveness), /healthz/ready (readiness: partner_sql + partner_commission_read_port) |
| Métricas endpoint | /metrics (prometheus-net) |
| Alertas | services/partner-management/observability/alerts.yaml (5 regras: PM-01..PM-05) |
| Gate anti-PII | CI: partner-management-anti-pii-gate (dotnet test --filter Category=PiiScan) |

---

## 16. Segurança e PII

| Controle | Implementação |
|---|---|
| Autenticação | JWT Bearer (produção) / TestAuthHandler (testes) |
| Autorização | RBAC via claims JWT; AuthorizationBehavior no pipeline MediatR |
| Isolamento multi-tenant | EF Core HasQueryFilter (tenant_id) + PostgreSQL RLS + RlsSessionInterceptor (ADR-0001) |
| Mascaramento de PII | PartnerPiiMasker: name → [name-masked], contact_email → [email-masked], contact_phone → [phone-masked] |
| Gate CI PiiScan | Category=PiiScan: 6 testes — PartnerPiiMasker (Infrastructure.Tests) + CreatePartnerHandler log scan (Application.Tests) |
| Secrets | Nunca em código; injetados via configuração de ambiente |

---

## 17. Resiliência

| Cenário | Comportamento |
|---|---|
| opportunity-pipeline timeout | Polly timeout por tentativa (padrão 10s); retorna commissionUnavailable=true |
| opportunity-pipeline 5xx | Retry com backoff exponencial + jitter (máx 2 tentativas); circuit breaker após 5 falhas |
| Circuit breaker aberto | Degradação parcial imediata: cadastro retornado com commissionUnavailable=true |
| Cloud SQL indisponível | Retorna 503; health check partner_sql Unhealthy → alerta ALERT-PM-04 (critical) |

---

## 18. Como Executar Localmente

```bash
# Subir dependências (PostgreSQL)
docker compose up -d postgres

# Aplicar migrations
cd services/partner-management
dotnet ef database update --project src/PartnerManagement.Infrastructure --startup-project src/PartnerManagement.Api

# Executar a API
dotnet run --project src/PartnerManagement.Api

# Swagger UI disponível em:
# https://localhost:7xxx/swagger
```

---

## 19. Como Testar

```bash
cd services/partner-management

# Suite completa (348 testes)
dotnet test PartnerManagement.slnx

# Gate de arquitetura (Clean Architecture — 10 testes)
dotnet test tests/PartnerManagement.Architecture.Tests/

# Gate anti-PII (6 testes — bloqueante no CI)
dotnet test PartnerManagement.slnx --filter Category=PiiScan

# Gate de isolamento multi-tenant PBT-04 (bloqueante no CI)
dotnet test tests/PartnerManagement.Infrastructure.Tests/ --filter Category=TenantIsolation

# Testes de resiliência (6 testes)
dotnet test tests/PartnerManagement.Infrastructure.Tests/ --filter "FullyQualifiedName~CommissionReadAdapterResilience"
```

---

## 20. Compliance

| Compliance / Norma | Aplicável? | Impacto no Módulo |
|---|---|---|
| LGPD | Marginal | partner.name pode ser nome de pessoa física — VAL-PARTNER-01 pendente (fail-safe: mascarado) |
| PCI DSS | Não aplicável | Não processa dados de cartão |

---

## 21. Pendências e Riscos

| Código | Tipo | Descrição | Status |
|--------|------|-----------|--------|
| VAL-PARTNER-01 | Aprovação humana | Classificação de partner.name como PII (pessoa física vs. PJ) | Pendente (approvals.yaml) |
| RISK-PM-02 | Risco | PII de parceiro exposta em logs sem mascaramento | Mitigado — PartnerPiiMasker + gate PiiScan CI |
| RISK-PM-05 | Divergência | README usava PUT; TRD usa PATCH | **Resolvido** — implementação usa PATCH; README corrigido |
| RISK-PM-06 | Risco | Indisponibilidade do pipeline degrada visão de comissão | Mitigado — circuit breaker + degradação parcial (TASK-28) |

---

## 22. Artefatos de Status

| Artefato | Versão | Status |
|----------|--------|--------|
| requirements.md | v1.0.0 | Aprovado para desenvolvimento |
| design.md | v0.1.0 | Aprovado para desenvolvimento |
| tasks.md | v0.1.0 | Implementado (6 ondas, 29 TASKs) |

---

## 23. Referências

| Documento | Seção |
|---|---|
| Design | docs/product/modules/partner-management/design.md |
| Requirements | docs/product/modules/partner-management/requirements.md |
| Tasks | docs/product/modules/partner-management/tasks.md |
| ADR-0001 Multi-tenant RLS | docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md |
| DDD Segmentation BC-03 | docs/product/ddd/subdomains/supporting/partner-management/README.md |
| Data Model BC-03 | docs/product/data-model/data-model.md §BC-03 |
| TRD Endpoints §8.4 | docs/product/trd/trd.md §8.4 |
| Alertas | services/partner-management/observability/alerts.yaml |
| Approvals | services/partner-management/approvals.yaml |
