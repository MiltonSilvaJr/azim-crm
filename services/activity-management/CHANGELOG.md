# Changelog — activity-management

Formato: [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/)
Versionamento: [Semantic Versioning](https://semver.org/lang/pt-BR/)

## [Não publicado]

### Adicionado (Onda 1 — Bootstrap)

- Solution `ActivityManagement.slnx` com 5 projetos de produção e 5 de teste.
- Projetos de produção: `ActivityManagement.Contracts`, `ActivityManagement.Domain`,
  `ActivityManagement.Application`, `ActivityManagement.Infrastructure`, `ActivityManagement.Api`.
- Projetos de teste: `ActivityManagement.Domain.Tests`, `ActivityManagement.Application.Tests`,
  `ActivityManagement.Infrastructure.Tests`, `ActivityManagement.Api.Tests`,
  `ActivityManagement.Architecture.Tests`.
- `Architecture.Tests` com NetArchTest validando 9 regras de dependência entre camadas (design §3).
- `Directory.Build.props` com configuração base: net10.0, Nullable, ImplicitUsings,
  TreatWarningsAsErrors, coverlet, xUnit, FluentAssertions 7.x, FsCheck.Xunit 3.x.
- `global.json` fixando SDK 10.0.107.
- `.editorconfig` com convenções UTF-8/LF/4-spaces.
- `AssemblyReference.cs` em cada projeto de produção para descoberta de assembly por reflexão.
- Pacotes mínimos instalados: MediatR 12.5.0, FluentValidation 11.11.0, EF Core 9.0.6,
  Npgsql 9.0.4, Polly 8.5.2, OpenTelemetry 1.16.0, NSubstitute 5.3.0, Testcontainers.PostgreSql 4.4.0.
