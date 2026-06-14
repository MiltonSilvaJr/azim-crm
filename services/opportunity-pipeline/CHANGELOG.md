# CHANGELOG — opportunity-pipeline

Formato: [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/)
Versionamento semântico: [SemVer](https://semver.org/lang/pt-BR/)

## [Não lançado]

## [0.1.0] — 2026-06-14

### Adicionado

- TASK-01 (Onda 1): solution `OpportunityPipeline.slnx` com 5 projetos de produção e 5 de teste (Clean Architecture + DDD).
- Projetos de produção: `OpportunityPipeline.Domain`, `OpportunityPipeline.Application`, `OpportunityPipeline.Infrastructure`, `OpportunityPipeline.Api`, `OpportunityPipeline.Contracts`.
- Projetos de teste: `OpportunityPipeline.Domain.Tests`, `OpportunityPipeline.Application.Tests`, `OpportunityPipeline.Infrastructure.Tests`, `OpportunityPipeline.Api.Tests`, `OpportunityPipeline.Architecture.Tests`.
- Regra de dependência entre camadas validada por `Architecture.Tests` (10 testes, 100% verde — NetArchTest.Rules 1.3.2).
- `Directory.Build.props`: `net10.0`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`, `coverlet.collector 6.0.4`, `xunit 2.9.3`, `FluentAssertions 7.0.0`, `FsCheck.Xunit 3.2.0`.
- `global.json`: SDK 10.0.107 com rollForward `latestPatch`.
- `.editorconfig`: formatação C# moderna, `file_scoped` namespaces, nullable diagnostics como erro.
- `AssemblyReference.cs` em todos os projetos de produção (marcadores para resolução via reflexão).
- Pacotes: `MediatR 12.5.0`, `FluentValidation 11.11.0`, `EF Core 9.0.6`, `Npgsql 9.0.4`, `Polly 8.5.2`, `NSubstitute 5.3.0`, `Testcontainers.PostgreSql 4.4.0`, `NetArchTest.Rules 1.3.2`.
