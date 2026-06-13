# Organization — Módulo de Gestão Organizacional

Módulo BC-08 do Azim CRM. Supporting Subdomain, Tier 1.

## Responsabilidades

- Gerenciar Business Units (BU) e suas configurações de pipeline (estágios, canais de origem, motivos de perda).
- Gerenciar usuários do tenant: convite por e-mail, ativação, desativação e memberships (vínculo usuário-BU-papel).
- Implementar RBAC deny-by-default com exportação do contexto de papéis para o módulo `authentication` via Redis.
- Garantir isolamento multi-tenant via EF Core global query filter + RLS PostgreSQL (ADR-0001).
- Consumir `tenant.provisioned.v1` para provisionamento inicial de BU e Tenant Admin.

## Não responsabilidades

- Autenticação e emissão de tokens JWT — delegado ao módulo `authentication`.
- Criação de identidade no GCP Identity Platform — delegado via port `IIdentityProvisioner`.
- Envio de e-mail de convite — delegado ao módulo `notification-delivery` via Outbox.
- Controle de oportunidades e atividades — consultado via ports `IOpportunityCounter` e `IActivityCounter`.

## Estrutura

```text
services/organization/
├── src/
│   ├── Organization.Domain/          # Agregados, objetos de valor, eventos, policies, state machine
│   ├── Organization.Application/     # Commands, queries, handlers, ports, behaviors, validadores
│   ├── Organization.Infrastructure/  # EF Core, RLS, Outbox/Inbox, Redis, adapters externos
│   ├── Organization.Api/             # Controllers REST, middleware RBAC, mapeamento DTO
│   └── Organization.Contracts/       # DTOs públicos, contratos de evento, schema RBAC exportado
├── tests/
│   ├── Organization.Domain.Tests/          # Invariantes, objetos de valor, state machine, PBTs
│   ├── Organization.Application.Tests/     # Handlers, policies, idempotência
│   ├── Organization.Infrastructure.Tests/  # Repositórios, RLS, Outbox/Inbox, Redis (Testcontainers)
│   ├── Organization.Api.Tests/             # Contratos REST, RBAC papel×operação
│   └── Organization.Architecture.Tests/    # Regras de dependência entre camadas
├── docs/
├── Organization.slnx
├── Directory.Build.props
├── global.json
└── README.md
```

## Regra de dependência

```
Api → Application, Infrastructure, Contracts
Application → Domain, Contracts
Infrastructure → Application, Domain, Contracts
Domain → ∅
Contracts → ∅
```

Validada em 100% dos PRs por `Organization.Architecture.Tests`.

## Como executar localmente

```bash
cd services/organization
dotnet build Organization.slnx
dotnet test Organization.slnx
```

## Como executar os testes

```bash
# Todos os testes (exceto Infrastructure.Tests que exige Docker)
dotnet test Organization.slnx --filter "FullyQualifiedName!~Infrastructure"

# Testes de arquitetura (CI gate obrigatório)
dotnet test tests/Organization.Architecture.Tests/Organization.Architecture.Tests.csproj

# Testes de integração (requer Docker em execução)
dotnet test tests/Organization.Infrastructure.Tests/Organization.Infrastructure.Tests.csproj
```

## Variáveis de ambiente

Documentadas por onda conforme implementação em `docs/`.

## Observabilidade

- Logs estruturados via Serilog + Cloud Logging.
- Métricas: `users_invited_total`, `users_deactivated_total`, `bu_created_total`, `membership_cache_hit_ratio`, `tenant_rls_violation_count`.
- Health checks: `GET /health/live`, `GET /health/ready`.
- Alerta imediato se `tenant_rls_violation_count > 0`.

## ADRs aplicáveis

- ADR-0001: Multi-tenancy pooled DB + RLS
- ADR-0009: Propagação de `correlation_id` e `tenant_id`
