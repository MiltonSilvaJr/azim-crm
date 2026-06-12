---
title: Snapshot imutável de comissão de parceiro no Ganho da oportunidade
status: Aceito
date: 2026-06-11
deciders:
  - "@MiltonSilvaJr"
consulted:
  - opportunity-pipeline (DD-002, RN-007)
  - Modelo de dados (opportunity_partner_commissions)
informed:
  - Times dos bounded contexts de oportunidade e financeiro
---

# ADR-0002 — Snapshot imutável de comissão de parceiro no Ganho da oportunidade

- **Status:** Aceito
- **Data:** 2026-06-11
- **Autores:** @MiltonSilvaJr
- **Supersede:** —
- **Substituído por:** —

## Contexto e Problema

A comissão devida a um parceiro depende de variáveis que **mudam ao longo do tempo**: regra de
comissão vigente, percentual do parceiro, valor da oportunidade. Quando a oportunidade é fechada
como **Ganho**, o valor da comissão passa a ter consequência financeira (pagamento, contabilidade,
auditoria). Se o cálculo permanecer dinâmico após o fechamento, qualquer alteração posterior em
regras ou cadastros **reescreveria retroativamente** valores já comprometidos — corrompendo
histórico financeiro e quebrando a rastreabilidade exigida em auditoria.

O problema: **como congelar o cálculo de comissão no momento do Ganho de forma que ele não possa
ser alterado por evolução de regras nem por edição acidental/maliciosa, preservando o histórico?**

## Drivers da Decisão

- **Integridade financeira:** valor comissionado no Ganho é compromisso; não pode mudar sozinho.
- **Auditabilidade:** é preciso provar qual valor foi congelado, quando e por quê.
- **Defesa em profundidade:** garantia não pode depender só da aplicação (bug/query ad-hoc vaza).
- **Reabertura controlada:** reabrir oportunidade é exceção legítima, mas exige autorização e trilha.

## Decisão

Ao mover a oportunidade para **Ganho**, o cálculo de comissão é **congelado em um snapshot
imutável**: a linha em `opportunity_partner_commissions` recebe `is_snapshot = true` e
`snapshot_at` (timestamp do congelamento). A imutabilidade é garantida **no banco**, não só na
aplicação:

1. **Trigger `trg_block_snapshot_mutation`** — bloqueia `UPDATE`/`DELETE` em qualquer linha com
   `is_snapshot = true`, levantando exceção. A aplicação não consegue contornar.
2. **Constraint de unicidade do snapshot ativo** — garante um único snapshot ativo por
   oportunidade/parceiro, evitando snapshots duplicados ou ambíguos.

**Reabertura** de oportunidade exige **permissão de gestor**, **re-audita** a mudança (trilha) e
**preserva o snapshot anterior** (não é apagado); um novo cálculo gera um novo snapshot quando a
oportunidade for fechada novamente.

## Opções Consideradas

### Opção A — Imutabilidade apenas na aplicação
Bloqueio por regra de negócio no serviço, sem trigger no banco.
- **Bom:** simples; sem objeto de banco adicional.
- **Ruim:** **ponto único de falha** — bug, query ad-hoc, script ou migration altera o snapshot
  sem rede de segurança; incompatível com integridade financeira auditável.
- **Veredito:** **Rejeitada** — sem garantia no banco.

### Opção B — Event sourcing completo da comissão
Reconstruir o valor a partir do log de eventos.
- **Bom:** histórico completo e reconstrução temporal nativa.
- **Ruim:** **custo desproporcional** ao problema (novo modelo de persistência, projeções,
  versionamento) para um requisito que um snapshot imutável já resolve.
- **Veredito:** **Rejeitada** — complexidade sem retorno proporcional neste estágio.

### Opção C (escolhida) — Snapshot imutável com trigger + constraint
- **Bom:** garantia no banco (defesa em profundidade); barato; auditável (`snapshot_at`);
  reabertura controlada preservando histórico.
- **Ruim:** lógica de imutabilidade vive em trigger (acoplamento ao PostgreSQL) — ver abaixo.
- **Veredito:** **Aceita.**

## Consequências

### Positivas
- Valor de comissão no Ganho é **estável e auditável**; evolução de regras não reescreve o passado.
- Garantia no **nível do banco**, cobrindo acessos fora da aplicação.
- Reabertura possível, porém **autorizada, auditada e não destrutiva**.

### Negativas e Mitigações
- **Lógica em trigger (acoplamento ao PostgreSQL).** *Mitigação:* aceitável — PostgreSQL é o SGBD
  do Azim; trigger versionada em migration e coberta por teste.
- **Caminho de reabertura precisa de exceção controlada ao bloqueio.** *Mitigação:* reabertura não
  altera o snapshot antigo (que permanece), apenas cria novo ciclo; permissão de gestor + trilha.
- **Risco de snapshot incorreto congelado por bug de cálculo.** *Mitigação:* validação do cálculo
  antes do congelamento; correção via reabertura auditada, nunca por UPDATE direto.

## Conformidade

1. Linha de comissão no Ganho tem `is_snapshot = true` e `snapshot_at` preenchidos.
2. `UPDATE`/`DELETE` em linha com `is_snapshot = true` é rejeitado pelo banco (teste de trigger).
3. Constraint de unicidade do snapshot ativo presente e testada.
4. Reabertura exige permissão de gestor, gera trilha de auditoria e preserva o snapshot anterior.

## Referências

- Design — opportunity-pipeline **DD-002**, **RN-007** — `docs/product/modules/opportunity-pipeline/design.md`
- Modelo de dados — `opportunity_partner_commissions` — `docs/product/data-model/data-model.md`
- ADR-0001 — Isolamento multi-tenant (defesa em profundidade no banco)
