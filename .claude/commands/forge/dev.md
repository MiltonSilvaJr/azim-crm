---
description: Sobe e sincroniza o ambiente local Docker (up/sync/smoke) conforme §20.3. Use antes de abrir PR para validar que o ambiente sobe limpo. Stub do MVP1 - completado na W5.1.
---

# /forge:dev

Operação do ambiente local (Docker Desktop, §20.3). Argumento: `up` | `sync` | `smoke`.

Pré-requisito: `docker-compose.yml` ou `compose.yaml` na raiz ou em `platform/docker/compose/`.
Se não existir, informe que o projeto ainda não define ambiente local e pare.

- **up:** `docker compose up -d` (arquivo canônico do projeto); aguarde healthchecks; reporte serviços e portas em poucas linhas.
- **sync:** reconcilie o estado local com a branch atual — aplique migrations/seeds pendentes conforme a stack do projeto (comandos em `FORGE.md runtime:`/docs do repo). Liste o que foi aplicado.
- **smoke:** execute o smoke local definido pelo projeto (script ou healthcheck dos serviços). Saída de uma linha por serviço: `OK`/`FAIL`.

Disciplina: output bruto de compose/migrations vai para `/tmp/forge-dev-*.log`; leia apenas `tail -20` em caso de erro. Antes de PR para `develop`, `up` limpo + `smoke` verde substituem a CI cara (§20.3).

> Stub do MVP1: a versão completa (integração com waves e gates por story) chega na W5.1.
