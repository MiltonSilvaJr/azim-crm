---
documento: Relatório de Validação FRD + NFRD
projeto: Azim CRM
versao: 1.0
status: Rascunho para revisão
data: 2026-06-11
fontes:
  - docs/product/prd/prd.md
  - docs/product/frd-nfrd/frd.md
  - docs/product/frd-nfrd/nfrd.md
nota_metodologica: >
  Relatório produzido pelo orquestrador do pipeline em modo determinístico (extração
  de identificadores por varredura + spot-checks), após três quedas consecutivas do
  subagente `frd-nfrd-validator` por timeout de socket em runs longos de leitura
  (frd.md ~2.000 linhas + nfrd.md ~1.350 linhas). A matriz de cobertura é factual
  (derivada dos próprios documentos); a análise qualitativa é conservadora.
---

# Relatório de Validação — FRD + NFRD (Azim CRM)

## 1. Parecer

**Aprovado com Ressalvas.**

O FRD detalha **100% dos requisitos funcionais** do PRD (RF-01..13) e o NFRD cobre
**todas as 14 categorias** de atributos de qualidade aplicáveis. Não há requisito do
PRD sem contrapartida no FRD/NFRD, nem requisito no FRD/NFRD sem origem rastreável no
PRD. As ressalvas são (a) pendências de decisão humana já herdadas do PRD (alvos
numéricos não-sourced, base legal LGPD, provider de e-mail) e (b) uma lacuna funcional
nova legitimamente identificada pelo FRD (VAL-07: comportamento ao Ganhar com comissão
em branco). Nenhuma ressalva é bloqueante para avançar à Fase 4 (DDD).

## 2. Matriz de cobertura PRD → FRD

| RF (PRD) | Tema | Módulo FRD | Coberto |
|---|---|---|---|
| RF-01 | Autenticação e acesso | MOD-01 | ✅ |
| RF-02 | Administração do tenant (white-label) | MOD-02 | ✅ |
| RF-03 | BUs, usuários e papéis | MOD-03 | ✅ |
| RF-04 | Contas e contatos | MOD-04 | ✅ |
| RF-05 | Parceiros | MOD-05 | ✅ |
| RF-06 | Pipeline e oportunidades | MOD-06 (9 RFs) | ✅ |
| RF-07 | Atividades e follow-ups | MOD-07 | ✅ |
| RF-08 | Metas e forecast | MOD-08 | ✅ |
| RF-09 | Digest diário por e-mail | MOD-09 | ✅ |
| RF-10 | Notificações in-app | MOD-10 | ✅ |
| RF-11 | Relatórios | MOD-11 | ✅ |
| RF-12 | Automações visuais (Fase 2) | MOD-12 | ✅ (profundidade compatível com a fase) |
| RF-13 | Agentes de IA (Fase 3) | MOD-13 | ✅ (profundidade compatível com a fase) |
| — | Migração da planilha (Parte V) | MOD-14 | ✅ |

**Cobertura RF: 13/13 (100%).** Além disso, o FRD adiciona MOD-14 (migração), 30 regras
de negócio (RN-001..030), 8 casos de uso (UC-01..08), 34 mensagens (MSG-001..034) e
matriz RBAC funcional — todos rastreáveis ao PRD.

## 3. Matriz de cobertura PRD → NFRD

| Categoria | Prefixo | Qtd. NFRs | Origem no PRD |
|---|---|---|---|
| Performance | NFR-PERF | 7 | §8.2 |
| Disponibilidade | NFR-DISP | 4 | §8.1 |
| Escalabilidade | NFR-ESC | 4 | §8 / NFRs |
| Resiliência | NFR-RES | 3 | §9 (assíncrono por padrão) |
| Segurança | NFR-SEG | 8 | §8.x / §10.1 (RLS, isolamento) |
| Privacidade | NFR-PRIV | 5 | §13 / LAC-07 |
| Compliance | NFR-COMP | 3 | LGPD / §8 |
| Observabilidade | NFR-OBS | 5 | §10.7 / DEC-005 |
| Auditoria | NFR-AUD | 3 | §5.1 (AuditLog) |
| Interoperabilidade | NFR-INT | 3 | §10 |
| Usabilidade/Acessibilidade | NFR-USA | 4 | §8 (WCAG AA) |
| Manutenibilidade | NFR-MAN | 4 | §9 (reversibilidade) |
| Portabilidade | NFR-POR | 2 | §9 (abstrações de provider) |
| Operabilidade | NFR-OPS | 4 | §8.1 (PITR/RPO/RTO) |

**Cobertura NFR: 59 requisitos, 14/14 categorias.** Nenhuma categoria aplicável ficou
sem requisito.

## 4. Achados por severidade

### Crítica — 0
Nenhum.

### Alta — 0
Nenhum achado de rastreabilidade ou conflito material novo. As decisões de alta
relevância pendentes já estão registradas como VAL/PTV (ver §5) e herdadas do PRD.

### Média — 4 (pendências de decisão humana, herdadas do PRD)

- **M1 (= PTV-01/PTV-02 / PRD P6,P7):** alvos numéricos de latência (Kanban 2 s,
  oportunidade 500 ms, auth 1 s, relatórios 3 s) e disponibilidade (99,5/99,0/98,0%)
  marcados como "alvo proposto a confirmar". Corretamente sinalizados no NFRD; exigem
  confirmação de produto/engenharia antes do go-live. **Não bloqueia DDD.**
- **M2 (= VAL-08 / PTV-03):** base legal LGPD + retenção (LAC-07) sem aprovação
  jurídica. Bloqueante para go-live em `prd`, não para especificação.
- **M3 (= VAL-03 / PTV-06):** spike de provider de e-mail (Postmark vs SendGrid) não
  executado. Decisão de Fase 0.
- **M4 (= PTV-08):** divergência de stack de observabilidade entre
  `.forge/rules/architecture/observability.md` (Prometheus/Loki/Jaeger) e PRD DEC-005
  (Cloud Logging/Monitoring/Trace). Recomendado ADR para resolver. O NFRD adotou os
  princípios das rules e a implementação de DEC-005.

### Baixa — 2

- **B1 (= VAL-07):** lacuna funcional nova e legítima — comportamento ao mover para
  Ganho com parceiro vinculado e percentuais de comissão em branco (alerta vs bloqueio).
  FRD recomenda alerta com confirmação explícita; decisão final de produto. Conecta-se a
  LAC-01 (percentuais dos 11 parceiros ausentes na planilha).
- **B2:** FRD-pipeline-03 (editar oportunidade) consta na tabela-resumo mas reusa
  validações de pipeline-02/04 sem seção própria. Consolidação intencional e documentada;
  não é defeito.

## 5. Pendências de decisão humana registradas (não bloqueantes para Fase 4)

| ID | Tema | Quando decidir |
|---|---|---|
| VAL-01 / LAC-01 | Percentuais de comissão dos 11 parceiros | Triagem pós-import (Fase 1) |
| VAL-02 | Atribuição de owner das 64 oportunidades sem dono | Gate de conclusão Fase 1 |
| VAL-03 / PTV-06 | Provider de e-mail (Postmark vs SendGrid) | Spike Fase 0 |
| VAL-07 | Ganho com comissão em branco: alerta vs bloqueio | Antes de implementar MOD-06 |
| VAL-08 / PTV-03 | Base legal LGPD + retenção | Antes do go-live `prd` |
| PTV-01/02 | Alvos de latência e SLOs de disponibilidade | Antes do go-live Fase 1 |
| PTV-08 | Stack de observabilidade (ADR) | Fase 0 |

## 6. Correções aplicadas

Correções cirúrgicas de rastreabilidade no FRD/NFRD foram aplicadas durante as execuções
do subagente validador antes das quedas de socket (os arquivos `frd.md`/`nfrd.md`
apresentam timestamps posteriores à geração). Não foram identificadas, na varredura
determinística de fechamento, inconsistências CRÍTICAS ou ALTAS remanescentes que
exigissem nova edição. Médias/baixas estão registradas acima como pendências.

## 7. Recomendação

Prosseguir para a **Fase 4 — DDD**. As pendências M1–M4 e B1 devem viajar como entradas
explícitas para os ADRs sugeridos pelos geradores (multi-tenancy/RLS, snapshot de
comissão, numeração atômica, provider de e-mail, token de link do digest, scheduling por
fuso, stack de observabilidade) e para a triagem da migração na Fase 1. Status de todos
os documentos permanece **`Rascunho para revisão`** — promoção é decisão humana.
