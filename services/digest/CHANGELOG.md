# Changelog — Digest Worker

Todas as mudanças notáveis do módulo `digest` (azim-digest-worker) são documentadas aqui.

Formato: [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/).
Versionamento: [Semantic Versioning](https://semver.org/lang/pt-BR/).

---

## [Não lançado]

### Adicionado

- **Onda 1 — Bootstrap (TASK-01):** solution `Digest.slnx` com 10 projetos Clean Architecture
  (.NET 10): `Digest.Domain`, `Digest.Application`, `Digest.Infrastructure`, `Digest.Api`,
  `Digest.Contracts` e cinco projetos de teste. Referências entre camadas conforme design §3.1.
  Pacotes NuGet: NodaTime, MediatR 12, FluentValidation, EF Core 9.0.6, Npgsql 9.0.4, Polly,
  FsCheck.Xunit, NSubstitute, Testcontainers.PostgreSql, NetArchTest.
- **Onda 1 — Testes de arquitetura (TASK-02):** `Digest.Architecture.Tests` com 9 testes
  NetArchTest validando: (a) regra de dependência das 5 camadas, (b) proibição de EF Core/HTTP/
  Pub/Sub em Domain, (c) proibição de mapeamento de tabelas alheias em Infrastructure.
  CI mínimo: workflow GitHub Actions `digest-ci.yml` (build + test em todo PR).
- **Onda 6 — Purge de retenção (TASK-24):** `DigestRetentionPurgeJob` (Infrastructure) remove
  `email_digest_logs` com mais de 90 dias (RNF 9.1) e `digest_action_tokens` expirados (RNF 9.2).
  `DigestRetentionHostedService` (Api) executa diariamente às 02:00 UTC por tenant. Log sem PII.
  4 testes de integração com Testcontainers verificando remoção e preservação corretas.
- **Onda 6 — Gate de isolamento CI cross-tenant (TASK-25):** `CrossTenantIsolationGateTests` com
  4 cenários de RLS (tenant A vs B, tokens cross-tenant, RLS WITH CHECK, falha-fechada sem SET).
  Gate `Category=SecurityGate` registrado em `digest-ci.yml` (bloqueia merge se falhar) e em
  `staging.yml` (gate de promote). Atende RNF 1.1, RNF 1.3, RNF 1.4, ADR-0001, RISK-DIGEST-04.
- **Onda 6 — Observabilidade completa (TASK-26):** `DigestMetrics` com 5 métricas snake_case
  (`digest_jobs_processed_total`, `digest_emails_sent_total`, `digest_emails_failed_total`,
  `digest_processing_duration_seconds`, `digest_delivery_rate`) via `System.Diagnostics.Metrics`.
  `ActivitySource` OTEL-compatível (`azim.digest`). Definições de alertas em
  `observability/alerts.yaml` (3 alertas para Cloud Monitoring — RNF 6.3). 8 testes verificando
  métricas, delivery rate e ausência de PII em tags (DD-011, RNF 3.1).
- **Onda 6 — Documentação final e DoD (TASK-27):** README atualizado com status Implementado,
  6 ondas, 27 TASKs, 6 PBTs e seções de observabilidade/retenção/gate. Matriz de rastreabilidade
  completa no tasks.md (todos `[X]`). DoD verificado conforme design §19.
