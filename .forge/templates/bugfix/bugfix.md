# Bugfix — <CHANGE_ID>

> Análise do bug do change `<CHANGE_ID>`. Este artefato substitui `requirements.md` no tipo `bugfix`.
> Marque incertezas com `NEEDS CLARIFICATION`. O change só atinge `requirements-ready` quando a análise estiver completa e sem pendências.

## 1. Comportamento atual (incorreto)

O que acontece hoje, com passos de reprodução determinísticos e evidência (log, screenshot, teste falhando).

## 2. Comportamento esperado

O que deveria acontecer, rastreável a requisito/spec/baseline existente quando houver.

## 3. Comportamento que deve permanecer inalterado

Fronteira explícita da correção — o que NÃO pode mudar como efeito colateral.

## 4. Root cause

Causa raiz (não o sintoma). Arquivo/função/condição; por que o defeito foi introduzido e por que não foi detectado.

## 5. Testes de regressão

- [ ] Teste que reproduz o bug (falha antes da correção, passa depois)
- [ ] Propriedades/PBT quando o domínio justificar (ex.: invariantes monetárias)
- [ ] Testes que protegem a seção 3 (comportamento preservado)

## 6. Rastreabilidade

Issue/report de origem, specs/baseline relacionados.
