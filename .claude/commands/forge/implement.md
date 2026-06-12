---
description: Executa as tasks do change ativo com checkpoints — TASK a TASK, commit atômico por task, gates baratos via skill gate-runner. Transiciona implementing → implemented.
argument-hint: "[<change-id>]"
---

# /forge:implement — execução das tasks do change

Argumentos: `$ARGUMENTS` (change-id opcional; sem argumento, use o único change ativo).

## Pré-flight (obrigatório)

1. Status `tasks-ready` (ou `implementing` — retomada idempotente: continue da primeira task não-`[X]`).
2. **Scale ≥ 3:** `analysis.md` deve existir sem achados BLOCKER abertos (`/forge:analyze` é obrigatório antes — pare e indique se ausente).
3. **Scale ≥ 3:** trabalhe em worktree dedicado — carregue a skill `using-git-worktrees` (localização canônica `.forge/worktrees/<change-id>`).
4. Primeira execução: `bash .forge/scripts/spec-transition.sh <change-id> implementing`.

## Loop de execução (por TASK, na ordem das waves)

Para cada task `[ ]` cuja(s) dependência(s) estejam `[X]`:

1. Marque `[-]` no `tasks.md` do change.
2. Implemente **somente** o escopo da task (paths declarados; TDD-first quando há lógica verificável; rules do projeto em `.forge/rules/` valem integralmente).
3. **Gates baratos** (skill `gate-runner`): parseabilidade dos arquivos gerados, grep negativo (sem `TODO`/`FIXME`/`not implemented` residuais, sem debug), anti-empty; rode build/teste da stack quando declarados no `FORGE.md runtime:`. Uma linha por gate; output bruto em `/tmp`.
4. Commit atômico: `<type>(<scope>): TASK-NN — <título conciso>`. **Nunca** co-autoria de IA (constitution). Não faça push — quem publica é o operador ou o fluxo orquestrador.
5. Marque `[X]` e emita **uma linha** de progresso (`TASK-NN ✓ <título> (<sha curto>)`) — sem resumos entre tasks (§17.6).

**Falha irrecuperável** (gate falha após 2 tentativas de correção, dependência externa indisponível, decisão de produto necessária): marque `[!]`, **pare a execução** e reporte o que travou — humano decide. Não pule para a próxima task de outra wave.

## Encerramento

Quando não restar `[ ]`/`[-]`/`[!]`:

```bash
bash .forge/scripts/spec-transition.sh <change-id> implemented
```

Reporte: tasks concluídas, commits, e o próximo comando — `/forge:verify`.

## Regras

- Escopo é lei: mudança fora dos `paths:` da task exige nova task (registre e pergunte).
- Sem `*-summary.md`, sem dumps de diff no chat.
- Manifest `affected_paths` desatualizado em relação ao que foi tocado → atualize ao encerrar.
