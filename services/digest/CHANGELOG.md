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
