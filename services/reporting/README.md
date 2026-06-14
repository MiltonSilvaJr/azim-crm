# Reporting

Módulo de relatórios analíticos do Azim CRM (BC-07 — Supporting Subdomain, Tier 2).

## Visão Geral

Read side puro (CQRS de leitura). Expõe cinco relatórios analíticos — funil por estágio,
forecast por BU/mês, ranking por responsável, oportunidades por canal e comissões por
parceiro (projetado × consolidado) — além de export CSV de qualquer relatório.

Sem escrita de negócio. Sem aggregates transacionais. Lê views de read model derivadas
dos bounded contexts autoritativos (BC-01, BC-03, BC-05) via Dapper/EF read-only.

## Estrutura

```
services/reporting/
├── Reporting.slnx                     # Solution .NET 10 (formato XML nativo)
├── global.json                        # SDK .NET 10.0.x
├── Directory.Build.props              # Configurações comuns (net10.0, Nullable, TreatWarningsAsErrors)
├── .editorconfig
├── src/
│   ├── Reporting.Contracts/           # DTOs request/response, enums (ReportType). ∅ deps.
│   ├── Reporting.Domain/              # Objetos de valor de leitura (Money, Period, ChannelShare, StageBucket, ReportScope). ∅ deps.
│   ├── Reporting.Application/         # Queries, handlers, portas, pipeline behaviors. → Domain, Contracts.
│   ├── Reporting.Infrastructure/      # Repositório Dapper/EF, views RLS, GCS client. → Application, Domain, Contracts.
│   └── Reporting.Api/                 # ReportingController, endpoints REST. → Application, Infrastructure, Contracts.
├── tests/
│   ├── Reporting.Domain.Tests/        # Objetos de valor, PBT-02, PBT-04.
│   ├── Reporting.Application.Tests/   # Handlers, RBAC, PBT-01, PBT-04, PBT-05.
│   ├── Reporting.Infrastructure.Tests/# Views RLS, PBT-03 (gate CI), GCS client.
│   ├── Reporting.Api.Tests/           # Contratos REST, catálogo de erros, anti-enumeração.
│   └── Reporting.Architecture.Tests/  # Regras de dependência Clean Architecture (gate CI).
└── docs/
```

## Regra de Dependência (design §3)

```
Api            → Application, Infrastructure, Contracts
Application    → Domain, Contracts
Infrastructure → Application, Domain, Contracts
Domain         → ∅
Contracts      → ∅
```

Validada automaticamente por `Reporting.Architecture.Tests` em todo PR.

## Princípios

- **Read side puro**: sem aggregates transacionais (DD-003).
- **Dinheiro em centavos inteiros**: todo `*_cents` é `long` — sem `float`/`double` (DD-007).
- **Isolamento multi-tenant**: `security_invoker` nas views + RLS + Global Query Filter (ADR-0001).
- **RBAC no servidor**: escopo resolvido pelo servidor, nunca pelo cliente (DD-006, Req 7).
- **Minimização de PII**: `display_name` só no ranking e fora dos logs (DD-008, RNF 4).

## Como executar localmente

```sh
cd services/reporting
dotnet build Reporting.slnx
dotnet test Reporting.slnx
```

## Referências

- `docs/product/modules/reporting/design.md` v0.1.0
- `docs/product/modules/reporting/requirements.md` v0.1.0
- `docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md`
