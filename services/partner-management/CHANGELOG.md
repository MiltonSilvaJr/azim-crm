# Changelog — partner-management

Todas as alterações notáveis deste módulo são documentadas aqui.

Formato: [Conventional Commits](https://www.conventionalcommits.org/pt-br/v1.0.0/) com escopo `(partner-management)`.

---

## [0.1.0] — Onda 1 Bootstrap

### Adicionado

- Solution `PartnerManagement.slnx` com 5 projetos de produção e 5 de teste (TASK-01).
- `Directory.Build.props` com net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors.
- `global.json` fixando SDK 10.0.107.
- `.editorconfig` com convenções do módulo.
- Projetos de produção: Domain, Application, Infrastructure, Api, Contracts.
- Projetos de teste: Domain.Tests, Application.Tests, Infrastructure.Tests, Api.Tests, Architecture.Tests.
- Referências entre projetos conforme regra de dependência de design §3.
- `AssemblyReference.cs` em cada projeto de produção para NetArchTest.
- Testes de dependência em `Architecture.Tests` (TASK-02).
- Pipeline CI (`.github/workflows/partner-management.yml`) executando build + test (TASK-02).
