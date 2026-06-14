# partner-management

Módulo BC-03 — Gestão de Parceiros Comissionados (Supporting Subdomain).

Gere os **parceiros comissionados** do tenant: cadastro com nome, papel tipado (`partner_type`) e percentuais padrão de comissão por componente do contrato (`pct_setup`, `pct_recorrente`). Upstream do `opportunity-pipeline`, que consome `partner_id` e os percentuais padrão ao vincular um parceiro a uma oportunidade.

## Objetivo

Entregar cinco capacidades técnicas centrais:

1. CRUD de parceiros com papel tipado canônico e percentuais padrão (Req 1, Req 2, Req 5, Req 6).
2. Inativação/reativação lógica (soft-delete via `active`), idempotente, sem exclusão física (Req 3, PBT-02).
3. Listagem de parceiros ativos para seleção/vinculação consumida pelo pipeline, e exposição de status para bloquear vínculo de parceiro inativo (Req 4, Req 8).
4. Visão de comissão do parceiro (projetada × consolidada) e relatório por período, consumindo o read model `opportunity_partner_commissions` do pipeline via porta de leitura — este módulo **nunca** recalcula nem persiste comissão (Req 9, Req 10, PBT-01, PBT-05).
5. Marcação de pendência de triagem de percentuais pós-importação (Req 11).

## Responsabilidades

- Cadastro, edição, inativação e reativação de parceiros.
- Exposição de lista e elegibilidade para o `opportunity-pipeline`.
- Visão e relatório de comissão via leitura do read model do pipeline.
- Auditoria append-only de toda escrita.
- Isolamento multi-tenant em profundidade (tenant_id + EF Global Query Filter + RLS).

## Não responsabilidades

- Cálculo de comissão (pertence ao `opportunity-pipeline`).
- Persistência de valores monetários de comissão (centavos inteiros no pipeline).
- Credencial/autenticação do parceiro (sem login no MVP, Req 12).
- Gerenciamento de papéis canônicos do tenant (consumidos via `ICanonicalRoleProvider`).

## Estrutura

```text
src/
  PartnerManagement.Domain/          # Domínio puro (agregado, VOs, eventos, specs, IRepository)
  PartnerManagement.Application/     # Casos de uso (commands, queries, handlers, behaviors, ports)
  PartnerManagement.Infrastructure/  # Adapters (EF Core, Outbox, PiiMasker, ReadAdapter, RLS)
  PartnerManagement.Api/             # Controllers REST, middleware, health checks
  PartnerManagement.Contracts/       # DTOs públicos e contratos de eventos de integração
tests/
  PartnerManagement.Domain.Tests/          # Testes unitários do domínio; PBT-02, PBT-03
  PartnerManagement.Application.Tests/     # Testes unitários de Application; PBT-01, PBT-05
  PartnerManagement.Infrastructure.Tests/  # Testes de integração (Testcontainers); PBT-04
  PartnerManagement.Api.Tests/             # Testes de API, RBAC, contratos Pact
  PartnerManagement.Architecture.Tests/    # Regras de dependência (NetArchTest)
```

## Regra de dependência (design §3)

```
Api            -> Application, Infrastructure, Contracts
Application    -> Domain, Contracts (NUNCA Infrastructure)
Infrastructure -> Application, Domain, Contracts
Domain         -> (nenhum projeto interno)
Contracts      -> (nenhum projeto interno)
```

Enforçada por `Architecture.Tests` (TASK-02) — falha o CI em caso de violação.

## APIs expostas

Base: `/api/v1/partners`. Documentação OpenAPI gerada em `/swagger` (TASK-29).

| Método | Caminho | Papel mínimo | Descrição |
|--------|---------|--------------|-----------|
| GET | `/api/v1/partners` | Viewer | Listar parceiros do tenant |
| POST | `/api/v1/partners` | Tenant Admin / Gestor de BU | Criar parceiro |
| GET | `/api/v1/partners/{id}` | Viewer | Detalhe do parceiro |
| PATCH | `/api/v1/partners/{id}` | Tenant Admin / Gestor de BU | Atualizar parceiro |
| POST | `/api/v1/partners/{id}/deactivate` | Tenant Admin | Inativar parceiro |
| POST | `/api/v1/partners/{id}/reactivate` | Tenant Admin | Reativar parceiro |
| GET | `/api/v1/partners/{id}/eligibility` | Sistema (pipeline, mTLS) | Elegibilidade |
| GET | `/api/v1/partners/{id}/commissions` | Gestor de BU / Tenant Admin | Visão de comissão |

## Eventos publicados

Via Outbox + Cloud Pub/Sub (sem PII em claro):

- `partner.created.v1`
- `partner.commission_percentages_updated.v1`
- `partner.deactivated.v1`
- `partner.reactivated.v1`

## Eventos consumidos

Nenhum nesta versão.

## Dependências

- `opportunity-pipeline`: leitura do read model `opportunity_partner_commissions` via `IPartnerCommissionReadPort` (mTLS interno).
- `audit-log`: publicação de eventos via Outbox + Cloud Pub/Sub.
- `data-migration`: cria parceiros via `POST /api/v1/partners` com `Idempotency-Key`.

## Configurações e variáveis de ambiente

Configuradas nas Ondas 4 e 6 (TASK-15, TASK-26). Variáveis críticas:

| Variável | Descrição |
|----------|-----------|
| `ConnectionStrings__PartnerManagement` | Connection string do Cloud SQL |
| `PartnerManagement__CommissionReadPort__BaseUrl` | URL do read model do pipeline |
| `PartnerManagement__CommissionReadPort__TimeoutSeconds` | Timeout do read port |

## Observabilidade

- Logs estruturados JSON com `correlation_id`, `tenant_id`, `partner_id` (sem PII).
- Métricas: `partners_created_total`, `partners_deactivated_total`.
- Tracing OpenTelemetry.
- Health: `/health`, readiness verifica Cloud SQL e read port.

## Como executar localmente

```sh
# A partir da raiz do serviço
dotnet run --project src/PartnerManagement.Api
```

## Como testar

```sh
# Todos os testes
dotnet test PartnerManagement.slnx

# Apenas Architecture.Tests
dotnet test tests/PartnerManagement.Architecture.Tests

# Apenas Domain.Tests
dotnet test tests/PartnerManagement.Domain.Tests

# PBTs (requerem Domain e Application implementados — Ondas 2 e 3)
dotnet test --filter Category=PBT
```

## Ondas de implementação

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap — solution, projetos, testes de arquitetura | TASK-01..02 | Em andamento |
| 2 | Domain — objetos de valor, agregado, eventos, state machine, PBTs | TASK-03..08 | Aguardando |
| 3 | Application — commands, queries, handlers, behaviors, PBTs | TASK-09..14 | Aguardando |
| 4 | Infrastructure — EF Core, migrations, RLS, repositório, outbox, adapters | TASK-15..21 | Aguardando |
| 5 | API + Contratos — controllers, DTOs, RBAC, erros, contratos Pact | TASK-22..25 | Aguardando |
| 6 | Hardening — observabilidade, PII masking, resiliência, DoD | TASK-26..29 | Aguardando |

## ADRs aplicáveis

- ADR-0001: isolamento multi-tenant em defesa em profundidade.
- ADR-0002: snapshot imutável de comissão (pertence ao opportunity-pipeline).
- ADR-0003: retenção indefinida de logs/auditoria.

## Troubleshooting

- **Build falha em Architecture.Tests**: verifique se algum `ProjectReference` proibido foi adicionado (design §3).
- **Testes de integração falham**: verificar se Docker está em execução (Testcontainers exige daemon Docker).
- **Erro de tenant não encontrado**: garantir que `TenantScopeBehavior` está registrado e o JWT contém `tenant_id`.
