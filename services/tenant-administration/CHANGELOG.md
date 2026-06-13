# Changelog — TenantAdministration

## [Não Lançado]

### Adicionado

- Onda 1: bootstrap da solution `.slnx` com 10 projetos (5 produção + 5 teste) conforme design.md §3
- `Directory.Build.props` com net10.0, Nullable enable, ImplicitUsings, TreatWarningsAsErrors
- `global.json` pinado ao .NET 10.0.107
- `.editorconfig` com convenções do projeto
- `AssemblyMarker` em cada projeto de produção para suporte a reflexão e testes de arquitetura
- `TenantAdministration.Architecture.Tests` com 15 testes de dependência via NetArchTest (Domain sem deps; Api não acessa Domain direto; Contracts sem deps internas; Application sem Infrastructure/Api; Infrastructure sem Api)
- Placeholders de compilação nos projetos de teste de Ondas 2+
