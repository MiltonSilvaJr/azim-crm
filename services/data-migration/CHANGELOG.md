# Changelog — data-migration

Todas as mudanças relevantes deste módulo são documentadas aqui.
Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/).

## [Não lançado]

### Adicionado (Onda 1 — Bootstrap)

- Solution `DataMigration.slnx` com 5 projetos de produção e 5 de teste (TASK-01)
- Projetos de produção: `DataMigration.Domain`, `.Application`, `.Infrastructure`, `.Api`, `.Contracts`
- Projetos de teste: `.Domain.Tests`, `.Application.Tests`, `.Infrastructure.Tests`, `.Api.Tests`, `.Architecture.Tests`
- `Directory.Build.props` com net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, coverlet, xUnit, FluentAssertions 7.x, FsCheck.Xunit
- `global.json` fixando .NET SDK 10.0.107
- `.editorconfig` com convenções de estilo C#
- 13 testes de arquitetura NetArchTest validando regras de dependência Clean Architecture (TASK-02)
- Regras D (namespaces proibidos em Domain) e E (ClosedXML confinado a Infrastructure) (TASK-02, ST-03)
- Teste de sanidade de carregamento do assembly Domain (TASK-01, ST-01)
- EF Core 9.0.6 e Npgsql 9.0.4 adicionados em Infrastructure (Onda 1 Bootstrap)
