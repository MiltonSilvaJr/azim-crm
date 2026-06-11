# PRD — Azim CRM

**Plataforma de gestão comercial multi-tenant para substituição de operação em planilha e crescimento em automação e inteligência**

- **Produto:** Azim CRM
- **Versão:** 0.1
- **Data:** 2026-06-10
- **Status:** Rascunho para revisão
- **Owner:** Milton Antonio da Silva Jr
- **Stakeholders:** Vellus (primeiro tenant e operador da plataforma), Time de Produto e Engenharia Azim

- **Histórico:**
  - v0.1 — 2026-06-10 — versão inicial gerada a partir de `docs/discovery/discovery-notes.md`; 13 decisões aprovadas (DEC-001 a DEC-013) e 6 pontos a validar (LAC-01 a LAC-06, correspondentes a VAL-001 a VAL-006 do discovery) incorporados

---

## Sumário

1. [Resumo Executivo](#1-resumo-executivo)
2. [Contexto e Problema](#2-contexto-e-problema)
3. [Personas](#3-personas)
4. [Visão do Produto e Objetivos](#4-visão-do-produto-e-objetivos)
5. [Escopo do Produto](#5-escopo-do-produto)
6. [Jornadas de Usuário](#6-jornadas-de-usuário)
7. [Requisitos Funcionais](#7-requisitos-funcionais)
8. [Requisitos Não Funcionais](#8-requisitos-não-funcionais)
9. [Restrições, Premissas e Lacunas](#9-restrições-premissas-e-lacunas)
10. [Riscos e Mitigações](#10-riscos-e-mitigações)
11. [Métricas de Sucesso](#11-métricas-de-sucesso)
12. [Documentos Relacionados](#12-documentos-relacionados)
13. [Anexos](#13-anexos)

> **Detalhamento em documentos filhos:**
>
> - `frd.md` — regras de negócio, fluxos funcionais, validações, mensagens, exceções e critérios de aceite por funcionalidade
> - `nfrd.md` — performance, disponibilidade, segurança, compliance, observabilidade, escalabilidade e capacidade
> - `TRD.md` — arquitetura técnica, integrações, APIs, contratos, infraestrutura, persistência, mensageria e padrões técnicos
> - `ADR.md` — decisões arquiteturais relevantes (DEC-001 a DEC-013 são candidatos a ADRs formais)
> - `UXD.md` — fluxos de experiência, Kanban, automação visual e diretrizes de interface

---

## 1. Resumo Executivo

### 1.1 Descrição do Produto

**Azim CRM** é uma plataforma de gestão comercial SaaS multi-tenant voltada para empresas B2B, com o objetivo de transformar pipeline em direção de vendas. A solução integra funil de oportunidades com dono obrigatório, comissão de parceiro nativa, digest diário automatizado, metas e relatórios em um único ecossistema, permitindo que equipes comerciais operem com rastreabilidade, responsabilidade e visibilidade contínua — sem depender de planilhas compartilhadas.

O produto é construído sobre um problema real e quantificado: a operação comercial da Vellus — primeiro tenant e operador da plataforma — roda hoje numa planilha Excel com 108 oportunidades ativas, R$ 9.013.250 em forecast ponderado e 59% das oportunidades sem responsável definido. Valores monetários são armazenados internamente como centavos inteiros (integer cents) em todas as entidades do modelo de dados.

### 1.2 Proposta de Valor

O produto entrega valor ao permitir:

- **Responsabilidade obrigatória sobre o pipeline:** toda oportunidade tem um dono; o digest diário cobra o vendedor automaticamente às 07:00, sem que o gestor precise perseguir atualizações.
- **Comissão de parceiro nativa na oportunidade:** o vínculo entre oportunidade e parceiro comissionado é estruturado no sistema, com snapshot imutável ao fechar negócio — gap confirmado em Salesforce e Zoho out-of-the-box.
- **Alcance proativo ao vendedor:** o digest leva o CRM ao vendedor em vez de esperar o vendedor abrir o sistema; inclui o "azimute da semana" às segundas-feiras com visão de pipeline vs metas.
- **Forecast estruturado com modelo Setup + Recorrente:** nativo para contratos B2B de implantação + mensalidade; forecast ponderado por probabilidade de estágio.
- **Multi-tenancy e white-label:** plataforma projetada para múltiplos tenants desde a fundação, com branding por tenant e isolamento de dados por RLS.

### 1.3 Justificativa Estratégica

Este produto é estratégico porque:

- A Vellus possui uma operação comercial real com R$ 9.013.250 em forecast sem rastreabilidade, responsabilidade ou controle de acesso — o risco operacional e regulatório (LGPD) é imediato.
- O gap de comissão de parceiro nativa foi confirmado adversarialmente: Salesforce e Zoho não entregam esse diferencial out-of-the-box — o que posiciona o Azim em mercado sub-servido.
- A Vellus atua simultaneamente como primeiro cliente (validação de produto) e como operador da plataforma (canal de distribuição) — o que reduz o custo de aquisição do primeiro cliente a zero e acelera o product-market fit.
- A arquitetura multi-tenant GCP com pool + RLS está projetada para crescer em tenants sem retrabalho estrutural.

### 1.4 Resultado Esperado

Ao final da Fase 1, espera-se que a Vellus seja capaz de operar as 3 BUs integralmente no Azim por no mínimo 14 dias consecutivos sem retornar à planilha, com 100% das 108 oportunidades migradas com owner definido, digest entregue às 07:00 BRT de segunda a sexta-feira e rastreabilidade completa de alterações via trilha de auditoria.

---

## 2. Contexto e Problema

### 2.1 Contexto Atual

A operação comercial da Vellus é conduzida num arquivo Excel compartilhado no OneDrive (`Pipeline Vellus.xlsx`). O arquivo concentra três unidades de negócio: Vellus (71 oportunidades), Axis (20) e Vellus Tech (17), totalizando 108 oportunidades ativas com forecast ponderado de R$ 9.013.250.

Não existe sistema de CRM instalado. O controle de acesso é inexistente: qualquer colaborador com acesso ao OneDrive lê e edita toda a planilha, incluindo dados pessoais de contatos (nome, e-mail, celular) sujeitos à LGPD. Não há trilha de auditoria, metas cadastradas ou mecanismo de alerta para oportunidades estagnadas ou datas de fechamento vencidas.

A Vellus é simultaneamente o primeiro tenant e o operador da plataforma Azim: usa o produto como CRM da própria operação comercial (Fase 1) e o operará como produto SaaS para outros tenants (Fases 2+).

### 2.2 Problema a Ser Resolvido

Atualmente, a equipe comercial da Vellus enfrenta os seguintes problemas:

- **Sem donos:** 64 de 108 oportunidades (59%) não têm responsável — ninguém é cobrado, nada anda; digest diário de follow-up é inviável.
- **Sem prazos:** apenas 6 de 108 (6%) têm data de fechamento — forecast frágil e datas vencidas sem alerta.
- **Comissão de parceiro fora do processo:** 24 oportunidades têm parceiro citado (11 parceiros), mas os percentuais de comissão são inexistentes na planilha — cálculo manual, opaco e sem rastreabilidade.
- **Sem metas:** nenhuma meta de BU ou responsável está cadastrada — impossível comparar pipeline com objetivo de negócio.
- **Sem auditoria:** alterações não são rastreadas — "nada profissional e confiável" (relato do usuário).
- **Exposição de dados pessoais:** contatos de negociação acessíveis a todos os colaboradores com acesso ao OneDrive — risco LGPD direto.

### 2.3 Dores Atuais

| Dor | Impacto | Público Afetado | Evidência |
|---|---|---|---|
| 59% das oportunidades sem responsável | Pipeline estagna; ninguém é cobrado; digest impossível | Gestor de BU, Executivo do tenant | `Pipeline Vellus.xlsx`: 64/108 sem owner |
| Forecast frágil (6% com data de fechamento) | Decisões baseadas em dados incompletos; R$ 9,01 mi em risco | Executivo do tenant, Gestor de BU | `Pipeline Vellus.xlsx`: 6/108 com data preenchida |
| Comissão de parceiro manual e opaca | 24 oportunidades com parceiro, 11 parceiros, 0% definidos; sem cálculo nem rastreabilidade | Gestor de BU | `Pipeline Vellus.xlsx`: coluna de parceiro sem percentuais |
| Zero metas cadastradas | Impossível medir performance vs objetivo | Gestor de BU, Executivo do tenant | `Pipeline Vellus.xlsx`: ausência de aba ou campo de metas |
| Sem trilha de auditoria | "Nada profissional e confiável" — sem responsabilização | Executivo do tenant | Relato direto do usuário (discovery) |
| Dados pessoais expostos sem controle | Risco LGPD: nome, e-mail, celular visíveis a todos | Todos com acesso ao OneDrive | Arquivo compartilhado sem RBAC |
| Vínculos manuais quebráveis | "Relacionada ao Pipeline #43" — renumerou, perdeu o vínculo | Vendedor | `Pipeline Vellus.xlsx`: campo de referência manual |
| Duplicatas de contas entre BUs | Pag.ai, Ethoca em Vellus e Axis como linhas independentes; sem visão 360° | Gestor de BU, Vendedor | `Pipeline Vellus.xlsx`: contas duplicadas identificadas |
| Typos em campos livres | "Miilton", "Sertão " (espaço), "Consulroria" — relatórios e filtros inválidos | Gestor de BU | `Pipeline Vellus.xlsx`: listagem de erros de digitação |

### 2.4 Oportunidade

A oportunidade consiste em substituir completamente a planilha por uma plataforma com donos obrigatórios, prazos, comissão nativa, metas, auditoria e digest diário automatizado, permitindo à Vellus profissionalizar a operação comercial e, em seguida, operar a mesma plataforma como produto SaaS para outros tenants B2B — com diferencial competitivo confirmado sobre Salesforce e Zoho.

### 2.5 Sistemas, Processos ou Soluções Existentes

| Sistema/Processo Atual | Responsável | Limitação | Estratégia |
|---|---|---|---|
| `Pipeline Vellus.xlsx` (OneDrive compartilhado) | Equipe comercial da Vellus | Sem RBAC, auditoria, metas, comissão estruturada, responsabilidade obrigatória ou alertas | Substituir completamente; congelar após migração (read-only com banner apontando para o Azim) |
| Fórmula `LARGE(B4:B500,1)+1` para numeração de propostas | Planilha | Dependente de estrutura frágil; próximo número: 95 | Substituir por numeração sequencial automática e imutável por tenant (`AZ-0095`) |
| Controle manual de comissão de parceiro (fora da planilha) | Equipe comercial | Sem rastreabilidade, sem cálculo, sem snapshot | Substituir por `OpportunityPartnerCommission` nativo no modelo de domínio |

---

## 3. Personas

> Personas representam usuários, operadores e atores relevantes para o produto. O parceiro comissionado não possui login no MVP (DEC-012) e não é representado aqui como persona.

### P-01 — Vendedor / Executivo de Contas

Profissional comercial que conduz oportunidades em uma ou mais BUs. Representa os vendedores da Vellus (ex.: Eduardo, Milton). Usuário primário do Kanban diário e principal destinatário do digest.

- **Perfil:** executa o ciclo completo de vendas — prospecção, proposta, negociação, fechamento; gerencia carteira de clientes e parceiros; atua dentro de uma ou mais BUs do tenant.
- **Objetivo:** fechar oportunidades dentro do prazo; ter visibilidade clara do funil pessoal e das atividades pendentes sem precisar construir filtros em planilha.
- **Dores:** não tem to-do estruturado; perde contexto de oportunidades antigas; não sabe o que fazer com quais clientes hoje sem abrir a planilha e aplicar filtros manualmente; comissão de parceiro calculada fora do processo.
- **Necessidades:** Kanban por BU com drag-and-drop; to-do diário por e-mail às 07:00 com atividades vencidas e de hoje; conclusão de atividade em 1 clique a partir do digest; histórico da conta num só lugar; numeração automática de oportunidades.
- **Canais de interação:** portal web (`app.azim.com.br/{slug}`); e-mail digest diário com link autenticado de ação.

### P-02 — Gestor de BU

Líder responsável pelo resultado de uma unidade de negócio. Acompanha performance da equipe, pipeline consolidado e aderência às metas. Pode gerenciar múltiplas BUs.

- **Perfil:** define metas da BU, distribui oportunidades entre vendedores, acompanha estagnações, gerencia parceiros e percentuais de comissão da BU.
- **Objetivo:** ter visibilidade do pipeline da BU vs metas; identificar oportunidades estagnadas; balancear carga entre vendedores; entender performance por canal e parceiro.
- **Dores:** hoje não tem visão consolidada da BU sem exportar e agregar manualmente a planilha; sem metas cadastradas, comparativo realizado vs objetivo é impossível.
- **Necessidades:** painel de pipeline por estágio e responsável; forecast da BU vs meta mensal; alertas de oportunidades estagnadas (> 14 dias); relatório de comissão por parceiro; digest de segunda-feira com visão da semana; permissão para reabrir oportunidades ganhas/perdidas da BU.
- **Canais de interação:** portal web; e-mail digest semanal de segunda-feira.

### P-03 — Executivo do Tenant

Sócio ou diretor da empresa (ex.: sócios da Vellus). Não opera o CRM diariamente, mas precisa de visão executiva consolidada para decisões estratégicas.

- **Perfil:** tomador de decisão estratégica; acompanha o negócio de forma macro; não insere oportunidades manualmente.
- **Objetivo:** ter visão consolidada do pipeline global por BU vs metas; identificar tendências; acompanhar top oportunidades e fechamentos esperados da semana.
- **Dores:** hoje depende de exportar a planilha e agregar manualmente para qualquer visão macro; sem metas, comparativo é impossível.
- **Necessidades:** digest de segunda-feira com "azimute da semana" (pipeline global, variação vs semana anterior, top oportunidades, fechamentos esperados, bloco de metas); relatório consolidado do tenant por BU.
- **Canais de interação:** e-mail digest de segunda-feira; portal web (relatórios e painel executivo).

### P-04 — Administrador do Tenant (Tenant Admin)

Responsável técnico/operacional pela configuração da empresa no Azim. Pode ser o próprio sócio ou um profissional de operações.

- **Perfil:** configura e mantém a plataforma para o tenant; gerencia usuários, papéis, BUs, estágios, parceiros e metas; não é necessariamente um vendedor ativo.
- **Objetivo:** configurar a empresa no Azim de forma correta e auditável; garantir que RBAC, branding e estágios reflitam a operação real.
- **Dores:** na planilha, qualquer configuração é manual e propensa a erros; sem controle de quem tem acesso a quê.
- **Necessidades:** interface de administração para branding (logo, favicon, cores, slug), gestão de usuários e papéis por BU, configuração de estágios e canais de origem, cadastro de parceiros e percentuais default, cadastro de metas mensais, visualização da trilha de auditoria.
- **Canais de interação:** portal web (módulo de administração do tenant).

### P-05 — Operador da Plataforma (Platform Operator)

Equipe da Vellus que opera o Azim como produto SaaS. Responsável por provisionar tenants, monitorar a plataforma e oferecer suporte de primeiro nível.

- **Perfil:** perfil técnico/operacional; não participa da operação comercial dos tenants; acessa exclusivamente o backoffice de plataforma.
- **Objetivo:** provisionar novos tenants de forma rápida e rastreável; monitorar saúde da plataforma; responder a incidentes sem expor dados comerciais dos tenants.
- **Dores:** sem plataforma, o provisionamento de tenants seria manual e propenso a erros de configuração de isolamento.
- **Necessidades:** interface de provisionamento de tenants (slug, branding inicial, usuário admin); acesso a logs de plataforma e alertas operacionais; bloqueio explícito de acesso a dados comerciais dos tenants por padrão.
- **Canais de interação:** backoffice de plataforma; Cloud Logging / Monitoring (GCP).

---

## 4. Visão do Produto e Objetivos

### 4.1 Visão

Ser a plataforma de gestão comercial de referência para empresas B2B brasileiras que operam com funil de vendas complexo, parceiros e contratos de implantação + recorrência — oferecendo pipeline estruturado com comissão nativa e digest diário como primeiro CRM que vai ao vendedor em vez de esperar o vendedor chegar ao CRM.

> Tagline oficial: **"Direção para vender melhor."**

### 4.2 Objetivos Estratégicos

### OBJ-01 — Eliminar a operação comercial em planilha e migrar 100% do pipeline da Vellus

A Vellus deve deixar de usar `Pipeline Vellus.xlsx` como sistema de gestão comercial e operar integralmente no Azim.

- **Resultado esperado:** 108 oportunidades migradas com owner definido; planilha congelada (read-only); 3 BUs operando no Azim por >= 14 dias consecutivos sem retorno à planilha.
- **Indicador associado:** KPI-01 (taxa de oportunidades com owner), KPI-02 (volume migrado), KPI-09 (14 dias sem retorno)
- **Prazo alvo:** critério de conclusão da Fase 1

### OBJ-02 — Tornar a comissão de parceiro rastreável e auditável no processo comercial

Toda oportunidade com parceiro associado deve ter percentuais de comissão definidos e snapshot imutável gerado ao fechar negócio.

- **Resultado esperado:** >= 80% das oportunidades com parceiro têm percentuais definidos ao término da Fase 1; snapshot imutável gerado automaticamente ao mover para "Ganho".
- **Indicador associado:** KPI-07 (cobertura de comissão)
- **Prazo alvo:** Fase 1

### OBJ-03 — Automatizar o alcance diário ao vendedor e eliminar dependência de lembretes manuais

O digest diário deve ser o principal mecanismo de cobrança e orientação comercial, substituindo mensagens e cobranças manuais de gestores.

- **Resultado esperado:** digest entregue às 07:00 BRT de seg–sex; taxa de entrega >= 98%; taxa de abertura >= 30% nos primeiros 30 dias.
- **Indicador associado:** KPI-03 (taxa de entrega), KPI-04 (taxa de abertura)
- **Prazo alvo:** Fase 1

### OBJ-04 — Prover visibilidade contínua de forecast e metas para gestores e executivos

Gestores de BU e executivos do tenant devem conseguir acessar painel de pipeline vs meta sem exportar dados.

- **Resultado esperado:** metas mensais cadastradas por BU e por responsável; painel acessível a gestores sem geração manual de relatório; >= 80% dos gestores e executivos acessam o painel ao menos 1 vez por semana nos 30 dias pós-go-live.
- **Indicador associado:** KPI-08 (WAU gestores e executivos)
- **Prazo alvo:** Fase 1 (metas básicas); Fase 2 (projeções avançadas e painel Direção)

### OBJ-05 — Construir base multi-tenant verificada para crescimento do Azim como produto SaaS

A arquitetura de isolamento deve ser comprovada em CI antes de qualquer tenant em produção.

- **Resultado esperado:** dois tenants de teste com brandings distintos logam isolados em prd ao final da Fase 0; testes de isolamento em CI como gate obrigatório de build.
- **Indicador associado:** KPI-06 (100% de aprovação dos testes de isolamento em CI)
- **Prazo alvo:** critério de conclusão da Fase 0

### 4.3 Objetivos Não Atendidos

Este PRD não tem como objetivo, nesta versão:

- Billing, cobrança, assinatura ou faturamento do Azim como produto SaaS (DEC-013)
- Portal de acesso externo para parceiros comissionados — deal registration (DEC-012)
- App mobile nativo ou PWA (horizonte do roadmap; decisão após Fase 1 validada)
- Suporte a multimoeda — todos os valores assumem BRL no MVP (LAC-05)
- Lead como entidade separada do funil (DEC-007; reservado para revisão futura)
- Integração com WhatsApp ou outros canais de comunicação externos
- Módulo de recursos humanos, folha de pagamento ou ERP

---

## 5. Escopo do Produto

### 5.1 Dentro do Escopo

#### Fase 0 — Fundação (pré-MVP)

- Provisionamento GCP 100% Terraform para os três ambientes (dev/stg/prd) na região `southamerica-east1`
- GCP Identity Platform multi-tenant configurado via Terraform (um tenant de identidade por tenant Azim)
- Esqueleto .NET 10 + React SPA com resolução de slug e theming por tenant via CSS variables
- RLS ativa no Postgres com testes de isolamento em CI (gate obrigatório: tenant A não lê dados do tenant B)
- E-mail transacional configurado com SPF/DKIM/DMARC validados no ambiente prd
- CI/CD via GitHub Actions + Workload Identity Federation (sem chave de service account em repositório)

#### Fase 1 — MVP Comercial (substitui a planilha)

- Autenticação e acesso por e-mail/senha e Google via Identity Platform (RF-01)
- Administração do tenant com white-label estrito — logo, favicon, cor primária, cor secundária, slug (RF-02)
- BUs, usuários e papéis com RBAC por BU (RF-03)
- Contas e contatos com dedupe por nome normalizado e visão 360° (RF-04)
- Parceiros com papel tipado, percentuais default e visão de comissão projetada e consolidada (RF-05)
- Pipeline Kanban com estágios configuráveis por BU, modelo de valor Setup + Recorrente × Duração, comissão nativa e owner obrigatório (RF-06)
- Atividades e follow-ups com conclusão em 1 clique e to-do diário (RF-07)
- Metas básicas mensais por BU e por responsável com graceful degradation (RF-08, parcial)
- Digest diário por e-mail às 07:00 BRT seg–sex — terça a sexta com pendências; segunda com "azimute da semana" (RF-09)
- Relatórios enxutos: funil, forecast, ranking, canais, comissões por parceiro, export CSV (RF-11)
- Trilha de auditoria imutável (`AuditLog`) para toda escrita em entidades de negócio
- Migração completa da planilha `Pipeline Vellus.xlsx`: dry-run → triagem assistida → import transacional → planilha congelada (requisito obrigatório da Fase 1 — DEC-010)

#### Fase 2 — Direção (automação e gestão)

- Automações visuais com React Flow: editor de workflows por BU com nós de gatilho, condição, ação e fim; execução assíncrona via Pub/Sub com log por nó (RF-12)
- RF-08 completo: projeções avançadas, painel Direção com tendências e gap analysis
- Notificações in-app para eventos de domínio relevantes (RF-10)
- Dashboards avançados e webhooks de saída
- Calendário de feriados no digest (integração com fonte a definir — LAC-06)

#### Fase 3 — Inteligência (IA)

- Agentes de IA em Python/FastAPI + LangGraph + Langfuse (RF-13):
  - Lead/opportunity scoring em batch noturno via Pub/Sub
  - Resumo 360° de conta sob demanda (briefing pré-reunião)
  - Próxima melhor ação por oportunidade com justificativa
  - Copilot conversacional de vendas com tracing completo no Langfuse
- Rate-limit de IA por tenant via Redis; dados jamais cruzam tenants
- Custo de IA por tenant visível no painel de operações

### 5.2 Fora do Escopo

Este produto não contempla, nesta versão:

- Billing, cobrança, assinatura ou faturamento do Azim como produto SaaS (DEC-013)
- Portal de acesso externo para parceiros — deal registration e visibilidade de comissão pelo parceiro (DEC-012)
- Múltiplos parceiros por oportunidade (horizonte do roadmap)
- Lead como entidade separada do funil (DEC-007; reservado para revisão futura)
- Suporte a multimoeda — todos os valores assumem BRL (LAC-05)
- App mobile nativo ou PWA (horizonte do roadmap)
- Integração com WhatsApp ou canais de comunicação externos
- Customização de CSS, fontes ou layout por tenant — white-label estrito (DEC-004)
- Módulo de ERP, RH, financeiro ou contabilidade

### 5.3 Escopo Futuro / Roadmap Evolutivo

Funcionalidades candidatas para versões futuras (horizonte não comprometido):

| Item | Descrição | Justificativa | Prioridade |
|---|---|---|---|
| Portal do parceiro | Login externo, deal registration, visibilidade de comissão pelo parceiro | Demanda natural após Fase 1 validada; parceiro hoje é dado, não usuário | Alta |
| Múltiplos parceiros por oportunidade | Splits de comissão entre parceiros numa mesma oportunidade | Modelo comercial de alguns tenants exige; não está na operação atual da Vellus | Média |
| Lead como entidade separada | Padrão HubSpot; pré-requisito para inbound marketing e automação de lead nurturing | DEC-007 registrado para revisão futura | Média |
| Integração WhatsApp | Registro de interações via WhatsApp vinculadas à oportunidade | Higiene de mercado B2B Brasil 2026 | Média |
| Multimoeda | Suporte a USD, EUR e outras moedas além de BRL | Vellus pode fechar negócios em moeda estrangeira antes da Fase 2 (LAC-05) | Média |
| App mobile | Acesso ao digest e Kanban em dispositivo móvel | Conveniência para vendedores em campo | Baixa |
| Billing SaaS | Planos, faturamento, integração com gateway de pagamento | Monetização do Azim para novos tenants | Alta (pós-MVP) |

---

## 6. Jornadas de Usuário

> As jornadas descrevem fluxos de alto nível do ponto de vista do usuário. O detalhamento de regras, validações, mensagens de erro e critérios de aceite por fluxo deve ser tratado no `frd.md`.

### Jornada J-01 — Vendedor gerencia o pipeline do dia

O vendedor acessa o Azim via `app.azim.com.br/{slug}` e visualiza o Kanban da sua BU. As colunas exibem soma de valor total e forecast ponderado por estágio. Ao identificar uma oportunidade para avançar, arrasta o card para o próximo estágio — o sistema valida as regras do estágio (data de fechamento obrigatória a partir de "Proposta Enviada"; motivo de perda obrigatório ao mover para "Perdido") e registra a transição na linha do tempo. O vendedor abre o card, registra uma atividade como concluída e cria a próxima com data e tipo. O sistema sugere a próxima atividade ao concluir. Todas as alterações são registradas no `AuditLog`.

**Resultado esperado:** oportunidade avançada no funil, atividade registrada, próxima ação agendada, histórico auditado.

**Variantes:**
- Criação rápida de oportunidade com conta nova inline
- Visão lista com filtros salvos (owner, canal, parceiro, estágio, estagnadas)
- Edição de comissão de parceiro antes de mover para "Ganho"

**Exceções principais:**
- Tentativa de mover para "Proposta Enviada" sem data de fechamento → bloqueado com mensagem orientativa
- Tentativa de mover para "Perdido" sem motivo de perda → bloqueado com lista de motivos configuráveis

---

### Jornada J-02 — Vendedor age pelo digest sem abrir o sistema

Às 07:00 BRT, de segunda a sexta, o serviço de digest seleciona usuários do tenant cujo fuso horário IANA indica o horário configurado. Verifica se há pendências: atividades vencidas, atividades de hoje ou oportunidades estagnadas (> 14 dias sem atividade). Se houver, envia o e-mail com branding do tenant. O vendedor recebe o digest e clica em link autenticado para concluir ou reagendar uma atividade — sem abrir o portal. O evento é registrado via `EmailDigestLog` (idempotência por usuário + data). Na segunda-feira, o e-mail inclui o "azimute da semana" com resumo do pipeline por estágio e BU e bloco de metas (omitido se não houver meta cadastrada).

**Resultado esperado:** vendedor age sobre pendências via e-mail; sistema atualizado sem acesso ao portal.

**Variantes:**
- Vendedor sem pendências e sem papel de gestão não recebe o digest
- Segunda-feira: bloco de metas incluído para gestores e executivos; omitido se não há meta cadastrada (graceful degradation)

**Exceções principais:**
- Falha de entrega de e-mail → evento registrado; retentativa conforme política do provider; alerta operacional
- Link de ação já executada clicado novamente → sistema confirma sem duplicar (idempotência por `EmailDigestLog`)

---

### Jornada J-03 — Gestor de BU acompanha pipeline e metas

O gestor acessa o painel da BU e visualiza o pipeline por estágio com soma de valor total e forecast ponderado. Seleciona o período mensal e compara realizado vs meta cadastrada. Identifica oportunidades estagnadas (> 14 dias sem atividade) e as redistribui entre vendedores. Acessa o relatório de comissões por parceiro (projetado vs consolidado). Na segunda-feira, recebe o digest com variação de pipeline vs semana anterior, top oportunidades e fechamentos esperados.

**Resultado esperado:** gestor tem visibilidade completa da BU sem exportar dados; identifica pontos de atenção e age sobre eles.

**Variantes:**
- Gestor de múltiplas BUs: visão consolidada de todas as suas BUs
- Sem meta cadastrada: painel exibe graceful degradation — apenas realizado vs pipeline, sem comparativo percentual

**Exceções principais:**
- Meta do período não cadastrada → painel exibe realizado e pipeline disponível; sem percentual de atingimento

---

### Jornada J-04 — Migração da planilha Pipeline Vellus.xlsx

O administrador do tenant faz upload do arquivo `Pipeline Vellus.xlsx` no módulo de migração. O sistema executa dry-run e retorna relatório com: contagem de oportunidades por BU, 64 oportunidades sem owner, 24 com parceiro sem percentual de comissão, dedupes de contas detectados e campos com erros de digitação. O administrador realiza triagem assistida — associa owners, define estágios e parceiros/percentuais. Após confirmação, o sistema executa import transacional (rollback total em caso de falha). Ao término, emite relatório final auditado. A planilha é congelada (read-only no OneDrive) com banner apontando para o Azim.

**Resultado esperado:** 108 oportunidades importadas com 100% de owners definidos; planilha congelada; relatório de migração disponível para auditoria.

**Variantes:**
- Import parcial por BU para validação incremental (a confirmar no `frd.md`)

**Exceções principais:**
- Upload de arquivo com formato inesperado → mensagem de erro orientativa; import abortado sem efeitos colaterais
- Falha no import transacional → rollback total; estado anterior preservado integralmente

---

### Jornada J-05 — Comissão de parceiro é registrada e congelada ao fechar negócio

O vendedor (ou gestor) abre uma oportunidade com parceiro associado. Registra percentuais de comissão (`pct_setup`, `pct_recorrente`) e opcionalmente valor fixo e meses comissionados. Antes de mover para "Ganho", o sistema exibe o valor calculado de comissão para confirmação. Ao mover para "Ganho", o sistema gera snapshot imutável da comissão na data do evento — disponível no relatório de comissões mesmo que percentuais futuros do parceiro sejam alterados.

**Resultado esperado:** comissão calculada, congelada e auditada ao fechar negócio; relatório rastreável por período.

**Variantes:**
- Oportunidade sem parceiro: campo de comissão omitido do formulário
- Reabertura de oportunidade ganha (apenas Tenant Admin ou Gestor de BU): snapshot original preservado; novo snapshot gerado ao re-fechar

**Exceções principais:**
- Tentativa de editar snapshot por usuário sem permissão → bloqueado; mensagem orientativa
- Oportunidade movida para "Ganho" com parceiro associado e percentual em branco → comportamento (alerta vs bloqueio) a definir no `frd.md` (LAC-01)

---

## 7. Requisitos Funcionais

> Os requisitos funcionais neste PRD estão em nível de produto: descrevem o que o sistema deve entregar e por que isso importa. Regras de negócio, fluxos detalhados, validações, mensagens de erro e critérios de aceite por funcionalidade devem ser tratados no `frd.md`.

### RF-01 — Autenticação e Acesso

O produto deve permitir que usuários autentiquem-se no Azim e que suas sessões sejam isoladas por tenant via slug.

O requisito deve contemplar:

- Login por e-mail/senha e conta Google via GCP Identity Platform multi-tenant
- Convite de usuário por e-mail com ativação por link temporário com expiração configurável
- Recuperação de senha por e-mail
- Sessão resolvida pelo slug do tenant (`app.azim.com.br/{slug}`)
- Expiração e renovação de sessão conforme política de segurança definida no `nfrd.md`
- Logout com invalidação de sessão em todos os dispositivos

**Valor de negócio:** elimina o arquivo Excel compartilhado sem autenticação; cumpre isolamento por tenant desde o ponto de entrada; base legal para RBAC e auditoria.

**Personas impactadas:** P-01, P-02, P-03, P-04, P-05

**Documentos filhos relacionados:** `frd.md` (RF-01), `nfrd.md` (política de sessão e segurança)

---

### RF-02 — Administração do Tenant (White-Label Estrito)

O produto deve permitir que o administrador do tenant configure a identidade visual da empresa no Azim dentro dos limites do white-label estrito aprovado (DEC-004).

O requisito deve contemplar:

- Upload de logo (PNG/SVG, tamanho máximo: 1 MB)
- Upload de favicon
- Configuração de cor primária e cor secundária com validação de contraste WCAG AA no upload
- Definição e bloqueio do slug do tenant (`app.azim.com.br/{slug}`)
- Configuração de fuso horário IANA do tenant (utilizado pelo digest diário — DEC-009)
- Nenhum elemento além dos listados é customizável: sem CSS, fontes ou layout por tenant

**Valor de negócio:** permite identidade visual por tenant sem custo de manutenção proporcional ao número de tenants; WCAG AA garante acessibilidade mesmo com cores customizadas.

**Personas impactadas:** P-04, P-05

**Documentos filhos relacionados:** `frd.md` (RF-02), `UXD.md` (design tokens e theming)

---

### RF-03 — BUs, Usuários e Papéis

O produto deve permitir que o administrador do tenant gerencie unidades de negócio, usuários e papéis com isolamento por BU.

O requisito deve contemplar:

- CRUD de BUs com nome e configurações próprias (estágios, canais de origem, motivos de perda)
- Convite de usuários por e-mail com papel e memberships por BU
- Papéis disponíveis: Platform Operator, Tenant Admin, Gestor de BU, Vendedor, Viewer
- Usuário pode ter papéis distintos em BUs distintas
- Desativação de usuário preserva todo o histórico (sem exclusão física; FKs intactas)
- Matriz de permissões RBAC aplicada em nível de API e verificada por RLS

**Valor de negócio:** elimina o acesso irrestrito à planilha compartilhada; cumpre controle de acesso por função; suporta estrutura multi-BU da Vellus (Vellus, Axis, Vellus Tech).

**Personas impactadas:** P-04, P-05

**Documentos filhos relacionados:** `frd.md` (RF-03), `TRD.md` (RBAC e RLS)

---

### RF-04 — Contas e Contatos

O produto deve permitir o cadastro e gestão de contas e contatos com visão 360° e dedupe por nome normalizado.

O requisito deve contemplar:

- CRUD de contas com busca e dedupe por nome normalizado (alerta "conta similar existe" antes de confirmar criação)
- CRUD de contatos vinculados à conta (campos: nome, e-mail, celular, cargo)
- Visão 360° da conta: oportunidades de todas as BUs visíveis ao usuário, contatos, atividades e histórico em ordem cronológica
- Conta compartilhada entre BUs: visível para usuários com acesso a múltiplas BUs sem duplicação (resolve Pag.ai e Ethoca entre Vellus e Axis)
- Reaproveitamento de conta em múltiplas oportunidades sem duplicação de registro

**Valor de negócio:** resolve duplicatas de contas entre BUs; concentra histórico do cliente num único lugar; elimina inconsistências de typo por normalização no cadastro.

**Personas impactadas:** P-01, P-02, P-04

**Documentos filhos relacionados:** `frd.md` (RF-04)

---

### RF-05 — Parceiros

O produto deve permitir o cadastro e gestão de parceiros comissionados com visibilidade de pipeline originado e comissões projetadas e consolidadas por período.

O requisito deve contemplar:

- CRUD de parceiros com papel tipado e percentuais default por componente (`pct_setup`, `pct_recorrente`)
- Canal de origem "Parceiro" exige vinculação de `partner_id` obrigatória na oportunidade
- Visão do parceiro: oportunidades originadas, comissão projetada (pipeline aberto) e comissão consolidada (oportunidades Ganhas) por período
- Parceiro é entidade de dados gerida pelo tenant — sem acesso ao sistema no MVP (DEC-012)

**Valor de negócio:** transforma o controle manual de comissão (11 parceiros sem % definidos) em processo estruturado e rastreável; base para o relatório de comissões da Fase 1.

**Personas impactadas:** P-02, P-04

**Documentos filhos relacionados:** `frd.md` (RF-05)

---

### RF-06 — Pipeline e Oportunidades

O produto deve prover o gerenciamento completo do funil de vendas por BU, com Kanban, modelo de valor estruturado, comissão de parceiro nativa e regras de estágio configuráveis.

O requisito deve contemplar:

- Kanban por BU com drag-and-drop entre estágios; colunas exibem soma de valor total e forecast ponderado
- Visão lista com filtros salvos (por owner, canal, parceiro, estágio, data de fechamento, estagnadas)
- Criação rápida (conta nova inline) e formulário completo de oportunidade
- Modelo de valor: `valor_total = valor_setup + valor_mensal × duracao_meses`; `forecast_ponderado = valor_total × probabilidade`; todos os campos de valor armazenados como centavos inteiros (DEC-011)
- `owner_id` obrigatório em toda oportunidade — ataca os 64/108 (59%) sem responsável identificados na planilha `Pipeline Vellus.xlsx` (evidência primária do problema)
- `data_fechamento_esperada` obrigatória a partir do estágio "Proposta Enviada"
- `motivo_perda` obrigatório ao mover para "Perdido" (lista configurável por BU)
- Estágios configuráveis por BU com probabilidade default e categoria (aberta/ganha/perdida) — DEC-001; seed derivado do processo da Vellus: Lead (10%) → Prospecção (25%) → Diagnóstico (50%) → Proposta Enviada (60%) → Negociação (75%) → Fechamento Provável (90%) → Ganho (100%) / Perdido (0%)
- Canais de origem configuráveis por BU; seed: Parceiro, Indicação, Prospecção ativa, Inbound, Evento, Base/Cliente existente, Outro
- Comissão de parceiro nativa — `OpportunityPartnerCommission`: `pct_setup`, `pct_recorrente`, `valor_fixo` opcional, `meses_comissionados`; snapshot imutável ao mover para "Ganho" (DEC-002)
- Numeração automática e imutável por tenant (ex.: `AZ-0095`), substitui a fórmula manual da planilha
- Linha do tempo da oportunidade: estágios, atividades, notas e alterações com auditoria
- Reabertura de oportunidade Ganha ou Perdida permitida apenas para Tenant Admin e Gestor de BU

**Valor de negócio:** core do produto; resolve diretamente ownership, prazo, comissão e rastreabilidade identificados na planilha; diferencial competitivo confirmado sobre Salesforce e Zoho (comissão de parceiro nativa).

**Personas impactadas:** P-01, P-02, P-03, P-04

**Documentos filhos relacionados:** `frd.md` (RF-06), `TRD.md` (modelo de domínio, RLS, snapshot de comissão)

---

### RF-07 — Atividades e Follow-ups

O produto deve permitir o registro e acompanhamento de atividades comerciais vinculadas a oportunidades e contas, com to-do diário e conclusão em 1 clique.

O requisito deve contemplar:

- CRUD de atividades com tipo (call, reunião, e-mail, tarefa), data, hora, responsável e descrição
- Visão "Meu dia / Minha semana": atividades vencidas, atividades de hoje, próximas atividades
- Conclusão em 1 clique a partir da visão lista e a partir do digest diário (link autenticado)
- Ao concluir atividade, sugestão de criação de próxima atividade
- Vinculação de atividade a oportunidade, conta ou contato
- Oportunidades estagnadas (> 14 dias sem atividade registrada) sinalizadas no Kanban e no digest

**Valor de negócio:** cria o to-do estruturado que o vendedor não tem hoje; é o mecanismo que alimenta o digest — sem atividades estruturadas, o digest não tem conteúdo acionável.

**Personas impactadas:** P-01, P-02, P-04

**Documentos filhos relacionados:** `frd.md` (RF-07)

---

### RF-08 — Metas e Forecast

O produto deve permitir o cadastro de metas comerciais e a comparação contínua com o pipeline e o realizado.

O requisito deve contemplar (Fase 1 — básico):

- Cadastro de metas mensais por BU e/ou por responsável com granularidade mensal (DEC-003)
- Agregações trimestral e anual derivadas automaticamente das metas mensais
- Painel de comparação: realizado vs meta vs pipeline disponível
- Graceful degradation: sem meta cadastrada, painel exibe apenas realizado vs pipeline sem erro conspícuo
- Bloco de metas no digest de segunda-feira omitido quando não há meta cadastrada

Capacidades adicionais previstas para Fase 2:

- Projeções avançadas com tendência de fechamento e gap analysis
- Painel Direção com comparativo histórico e previsão de fechamento

**Valor de negócio:** resolve o gap de zero metas cadastradas na Vellus; permite ao gestor e ao executivo comparar pipeline com objetivo de negócio pela primeira vez.

**Personas impactadas:** P-02, P-03, P-04

**Documentos filhos relacionados:** `frd.md` (RF-08)

---

### RF-09 — Digest Diário por E-mail

O produto deve enviar automaticamente um e-mail diário personalizado e acionável a cada usuário com pendências comerciais, no horário configurado do tenant, de segunda a sexta-feira.

O requisito deve contemplar:

- Cloud Scheduler dispara job horário em UTC; o serviço seleciona tenants cuja hora local IANA = `horario_digest` (default 07:00) e o dia é útil (seg–sex no fuso do tenant) — DEC-009
- **Terça a sexta (por usuário com pendências):** atividades vencidas, atividades de hoje, oportunidades estagnadas (> 14 dias), datas de fechamento vencidas; links autenticados de 1 clique para concluir/reagendar
- **Segunda-feira ("azimute da semana"):** resumo do pipeline por estágio e BU; variação vs semana anterior; ganho no mês/trimestre; top oportunidades; fechamentos esperados na semana; segmentação por canal; bloco de metas (realizado vs meta, gap, pipeline disponível — omitido se não há meta)
- Idempotência por `EmailDigestLog` (usuário + data): sem envio duplicado em retentativas
- Usuário sem pendências e sem papel de gestão não recebe o digest
- E-mail renderizado com branding do tenant (logo, cores via CSS variables)
- Domínio de envio `mail.azim.com.br` com SPF/DKIM/DMARC validados — DEC-008
- Supressão e bounce handling registrados; webhooks de evento (entregue/aberto) alimentam KPI-03 e KPI-04
- Provider: Postmark (candidato primário) atrás da abstração `IEmailSender` — DEC-008; SendGrid como alternativa avaliada no spike (LAC-03)

**Valor de negócio:** diferencial competitivo central — o CRM vai ao vendedor em vez do contrário; principal mecanismo de cobrança automatizada que substitui lembretes manuais de gestores.

**Personas impactadas:** P-01, P-02, P-03

**Documentos filhos relacionados:** `frd.md` (RF-09), `TRD.md` (Cloud Scheduler, Pub/Sub, IEmailSender)

---

### RF-10 — Notificações In-App

O produto deve prover notificações dentro da plataforma para eventos de domínio relevantes ao usuário. *(Fase 2)*

O requisito deve contemplar:

- Notificações in-app para eventos como: atividade atribuída, oportunidade movida de estágio, automação executada
- Central de notificações acessível na navegação principal com contagem de não lidas
- Marcação como lida individual e em massa; limpeza do histórico

**Valor de negócio:** complementa o digest diário com feedback imediato em tempo real para ações dentro do sistema; reduz dependência do e-mail para eventos de alta urgência.

**Personas impactadas:** P-01, P-02, P-04

**Documentos filhos relacionados:** `frd.md` (RF-10)

---

### RF-11 — Relatórios

O produto deve prover relatórios operacionais para gestores e executivos acompanharem performance comercial por BU e por período.

O requisito deve contemplar (Fase 1 — enxuto):

- Funil por estágio: total de oportunidades e valor por estágio no período
- Forecast por BU e por mês
- Ranking por responsável: valor total e ganhos no período
- Oportunidades por canal de origem
- Comissões por parceiro: projetado (pipeline aberto) e consolidado (oportunidades Ganhas) por período
- Export CSV de todos os relatórios

**Valor de negócio:** entrega a visibilidade gerencial que hoje exige exportar e agregar manualmente a planilha; base para decisões de pipeline e de comissão.

**Personas impactadas:** P-02, P-03, P-04

**Documentos filhos relacionados:** `frd.md` (RF-11)

---

### RF-12 — Automações Visuais

O produto deve permitir a criação de workflows de automação comercial por BU de forma visual, sem código. *(Fase 2)*

O requisito deve contemplar:

- Editor de workflows por BU com React Flow
- Nós disponíveis: Gatilho (eventos de domínio), Condição, Ações (criar atividade, atualizar campo, notificar in-app, enviar e-mail, webhook), Fim
- Execução sempre assíncrona via Pub/Sub → worker (nunca síncrona)
- Log de execução por nó e botão "Executar teste"
- Templates prontos incluídos (ex.: criar atividade ao avançar estágio; notificar gestor ao estagnar 7 dias)

**Valor de negócio:** reduz trabalho manual repetitivo; democratiza automação comercial sem exigir desenvolvimento customizado.

**Personas impactadas:** P-02, P-04

**Documentos filhos relacionados:** `frd.md` (RF-12), `TRD.md` (Pub/Sub, workers)

---

### RF-13 — Agentes de IA

O produto deve prover capacidades de inteligência artificial para scoring, resumo e sugestão de próxima ação no processo comercial, com rastreabilidade completa e isolamento de dados por tenant. *(Fase 3)*

O requisito deve contemplar:

- Lead/opportunity scoring em batch noturno via Pub/Sub
- Resumo 360° de conta: briefing sob demanda antes de reunião
- Próxima melhor ação por oportunidade com justificativa legível
- Copilot conversacional de vendas sobre os dados do tenant (REST síncrono)
- Observabilidade total via Langfuse: prompt, resposta, tokens, latência, passos intermediários
- Rate-limit de IA por tenant via Redis; dados jamais cruzam tenants
- Custo de IA por tenant visível no painel de operações

**Valor de negócio:** diferencial futuro confirmado — comparativos de CRM de 2026 não citam IA como entregue; posiciona o Azim como plataforma de direção inteligente.

**Personas impactadas:** P-01, P-02, P-03

**Documentos filhos relacionados:** `frd.md` (RF-13), `TRD.md` (LangGraph, Langfuse, Python/FastAPI)

---

### 7.1 Matriz Resumida de Requisitos Funcionais

| Código | Requisito | Descrição Resumida | Prioridade | Fase | Persona Principal | Documento |
|---|---|---|---|---|---|---|
| RF-01 | Autenticação e Acesso | Login e-mail/senha e Google; sessão por tenant via slug | Must | 0/1 | P-01..P-05 | `frd.md` RF-01 |
| RF-02 | Administração do Tenant | White-label estrito: logo, favicon, cores, slug, fuso | Must | 1 | P-04 | `frd.md` RF-02 |
| RF-03 | BUs, Usuários e Papéis | CRUD de BUs, convite, RBAC por BU, desativação | Must | 1 | P-04 | `frd.md` RF-03 |
| RF-04 | Contas e Contatos | CRUD com dedupe, visão 360°, conta compartilhada entre BUs | Must | 1 | P-01, P-02 | `frd.md` RF-04 |
| RF-05 | Parceiros | CRUD com percentuais, visão de comissão projetada e consolidada | Must | 1 | P-02, P-04 | `frd.md` RF-05 |
| RF-06 | Pipeline e Oportunidades | Kanban, modelo Setup+Recorrente, owner obrigatório, comissão nativa | Must | 1 | P-01, P-02 | `frd.md` RF-06 |
| RF-07 | Atividades e Follow-ups | CRUD, to-do diário, conclusão 1 clique, sugestão de próxima | Must | 1 | P-01, P-02 | `frd.md` RF-07 |
| RF-08 | Metas e Forecast | Metas mensais por BU/responsável, graceful degradation | Must (básico) | 1/2 | P-02, P-03 | `frd.md` RF-08 |
| RF-09 | Digest Diário por E-mail | E-mail diário acionável 07:00 BRT; segunda com azimute | Must | 1 | P-01, P-02, P-03 | `frd.md` RF-09 |
| RF-10 | Notificações In-App | Central de notificações por eventos de domínio | Should | 2 | P-01, P-02 | `frd.md` RF-10 |
| RF-11 | Relatórios | Funil, forecast, ranking, canais, comissões, export CSV | Must | 1 | P-02, P-03 | `frd.md` RF-11 |
| RF-12 | Automações Visuais | Editor React Flow; nós de gatilho/condição/ação; execução assíncrona | Should | 2 | P-02, P-04 | `frd.md` RF-12 |
| RF-13 | Agentes de IA | Scoring, resumo, próxima ação, copilot; Langfuse; isolamento por tenant | Could | 3 | P-01, P-02 | `frd.md` RF-13 |

### 7.2 Priorização

Classificação MoSCoW aplicada à Fase 1 (MVP Comercial):

- **Must:** RF-01 a RF-09 e RF-11 — obrigatórios para substituir a planilha e atingir o critério de conclusão da Fase 1
- **Should:** RF-10 e RF-12 — importantes para a Fase 2 (Direção); não bloqueiam o MVP
- **Could:** RF-13 — desejável na Fase 3 (Inteligência); depende de validação do produto nas fases anteriores
- **Won't (nesta versão):** billing SaaS, portal do parceiro, múltiplos parceiros por oportunidade, Lead como entidade separada (DEC-007), multimoeda, app mobile

### 7.3 User Stories

### US-01 — Vendedor recebe e age pelo digest diário sem abrir o sistema

Como vendedor,
quero receber às 07:00 BRT um e-mail com minhas atividades vencidas e de hoje,
para que eu possa agir sobre minhas pendências comerciais sem precisar abrir o CRM manualmente.

**Requisito funcional relacionado:** RF-09

**Critérios objetivos mínimos:**
- O e-mail é enviado entre 07:00 e 07:05 BRT em dias úteis (seg–sex) para usuários com pelo menos 1 atividade vencida ou agendada para hoje
- Cada atividade no e-mail tem link autenticado que permite concluir ou reagendar em 1 clique sem exigir login adicional
- O `EmailDigestLog` registra o envio; um segundo disparo no mesmo dia para o mesmo usuário não gera segundo e-mail (idempotência verificável em banco)

---

### US-02 — Vendedor não perde oportunidade por falta de responsável

Como gestor de BU,
quero que toda nova oportunidade exija um owner definido no momento da criação,
para que nenhuma oportunidade fique sem responsável e sem aparecer no digest de ninguém.

**Requisito funcional relacionado:** RF-06

**Critérios objetivos mínimos:**
- O formulário de criação de oportunidade bloqueia o envio se `owner_id` estiver em branco (validação na API retorna HTTP 422 com campo e mensagem identificados)
- O campo owner é pré-preenchido com o usuário autenticado e pode ser alterado pelo usuário com permissão adequada
- Após salvar, a oportunidade aparece no digest do owner na próxima execução do scheduler

---

## 8. Requisitos Não Funcionais

> Os requisitos não funcionais neste PRD estão em nível de produto. O detalhamento completo de SLOs, SLAs, padrões de teste e critérios de aceite deve ser feito no `nfrd.md`.

### 8.1 Disponibilidade

O produto deve operar com disponibilidade compatível com a criticidade de cada módulo.

| Tier | Módulos / Capacidades | Meta de Disponibilidade | Observação |
|---|---|---|---|
| Tier 1 | Pipeline/Kanban (RF-06), Digest (RF-09), Autenticação (RF-01) | >= 99,5% | Críticos para a operação diária da equipe comercial |
| Tier 2 | Contas/Contatos (RF-04), Atividades (RF-07), Relatórios (RF-11), Metas (RF-08), BUs/Usuários (RF-03) | >= 99,0% | Importantes; degradação tolerável por horas |
| Tier 3 | Backoffice Platform Operator, Migração de planilha, Automações (RF-12), IA (RF-13) | >= 98,0% | Operações administrativas e assíncronas |

Definição de janelas de manutenção, RPO, RTO e SLAs por capacidade no `nfrd.md`. RPO <= 5 min e RTO <= 4 h para Cloud SQL (PITR ativo) como referência aprovada no `azim-product-spec.md`.

### 8.2 Performance

Metas de referência para o MVP (Fase 1):

- Kanban da BU — carregamento inicial da visão com oportunidades: <= 2 segundos (p95)
- Criação/edição de oportunidade: <= 500 ms (p95)
- Autenticação (login, renovação de token): <= 1 segundo (p95)
- Processamento do digest para todos os tenants do horário selecionado: <= 5 minutos após trigger do Cloud Scheduler
- Import transacional da planilha (até 500 oportunidades): <= 5 minutos
- Relatórios enxutos (RF-11) para períodos de até 12 meses: <= 3 segundos (p95)

Metas de SLO detalhadas por endpoint e operação no `nfrd.md`.

### 8.3 Segurança

O produto deve garantir:

- Autenticação via GCP Identity Platform multi-tenant — sem senha armazenada no sistema Azim
- Autorização RBAC verificada em todo endpoint de API; implementação dupla: API + RLS de banco
- Multi-tenancy com defesa em profundidade — (1) EF Core global query filter por `tenant_id`; (2) RLS Postgres via `SET app.current_tenant = @tenant_id`; (3) testes de isolamento em CI como gate obrigatório (DEC-006); vazamento entre tenants = incidente sev-1
- Dados pessoais de contatos (nome, e-mail, celular) jamais presentes em logs
- Criptografia em trânsito: TLS 1.2+ obrigatório em todos os endpoints
- Criptografia em repouso: Cloud SQL com encryption at rest gerenciado pelo GCP
- Segredos gerenciados exclusivamente via GCP Secret Manager — nunca em repositório ou variável de ambiente não gerenciada
- Trilha de auditoria imutável em `AuditLog`: toda escrita em entidade de negócio registra autor, delta e timestamp
- WAF + rate limit de borda via Cloud Armor; CSP estrita no frontend

Padrões de segurança detalhados no `nfrd.md` e no `TRD.md`.

### 8.4 Privacidade e Proteção de Dados

O produto deve cumprir a LGPD (Lei nº 13.709/2018) para dados pessoais de contatos (nome, e-mail, celular):

- Minimização de dados pessoais coletados — coletar apenas o necessário para a operação comercial
- Base legal documentada para tratamento de dados de contatos (a confirmar com equipe jurídica da Vellus — LAC-07)
- Mecanismo de anonimização ou exclusão de dados pessoais de contatos a pedido (direito ao esquecimento)
- Sem PII em logs: nome, e-mail e celular de contatos jamais registrados no Cloud Logging
- Acesso a dados pessoais de contatos restrito ao escopo do usuário (RBAC + RLS)
- Acesso a dados comerciais dos tenants bloqueado para Platform Operator por padrão; acesso autorizado e auditado apenas em casos de suporte
- Retenção controlada: política de retenção de dados a ser definida no `nfrd.md`

Requisitos LGPD detalhados no `nfrd.md`.

### 8.5 Usabilidade e Acessibilidade

O produto deve oferecer:

- Interface responsiva compatível com desktop (>= 1280px) e tablet (>= 768px); acesso mobile coberto pelo digest via e-mail no MVP
- Contraste WCAG 2.1 nível AA mínimo, inclusive para as cores customizadas de cada tenant (validado no upload — DEC-004)
- Kanban com drag-and-drop utilizável em mouse e touchpad
- Fluxos críticos (criar oportunidade, mover estágio, registrar atividade) executáveis em no máximo 5 passos a partir do Kanban
- Mensagens de erro claras, acionáveis e orientativas — ex.: "Data de fechamento é obrigatória a partir de Proposta Enviada. Preencha antes de avançar."
- Tom visual corporativo, objetivo e estratégico em UI, digest e mensagens de sistema — padrão da marca Azim

Diretrizes de interface, wireframes e fluxos de UX no `UXD.md`.

### 8.6 Observabilidade e Auditabilidade

O produto deve permitir:

- Logs estruturados no Cloud Logging com `tenant_id` como label obrigatório em todo evento; sem PII nos logs
- Métricas técnicas e de negócio no Cloud Monitoring: taxa de entrega do digest, uso por tenant, erros por módulo
- Rastreamento distribuído via Cloud Trace com `tenant_id` propagado entre serviços
- Alertas operacionais configurados para: taxa de 5xx > 1%, latência p95 > 2 s por mais de 5 minutos, falha de digest (>0 falhas em 1 hora), fila Pub/Sub crescendo sem consumo, violação de RLS detectada
- `EmailDigestLog` como trilha de idempotência e evidência de entrega por usuário e data
- `AuditLog` imutável: toda escrita em entidade de negócio registra autor, delta JSON, timestamp e `tenant_id`
- Retenção mínima de logs e eventos: política a definir no `nfrd.md` (referência: 90 dias)
- Custo de IA por tenant visível no painel de operações (Fase 3)

Padrões de observabilidade detalhados no `nfrd.md` e no `TRD.md`.

### 8.7 Escalabilidade e Capacidade

| Métrica | Volume Inicial (Fase 1) | Volume Esperado (12 meses) | Pico Estimado | Observação |
|---|---|---|---|---|
| Tenants ativos | 1 (Vellus) | 5–20 | 50 | Crescimento gradual; pool + RLS suporta centenas sem retrabalho |
| Oportunidades por tenant | 108 (Vellus) | 500 | 2.000 | Baseado em porte médio do mid-market B2B |
| Usuários por tenant | 5–10 (Vellus) | 20 | 100 | Equipe comercial + gestores |
| Destinatários do digest / dia | ~10 (Fase 1) | ~200 | 1.000 | Cresce proporcionalmente a tenants × usuários |
| Requisições por segundo | < 5 (Fase 1) | 50 | 200 | Estimativa; a validar com load test antes do go-live |

Premissas de capacidade e estratégia de escalabilidade detalhadas no `nfrd.md` e no `TRD.md`.

### 8.8 Compliance

O produto deve estar aderente a:

- LGPD (Lei nº 13.709/2018) — dados pessoais de contatos (nome, e-mail, celular)
- WCAG 2.1 nível AA — acessibilidade da interface, incluindo cores customizadas por tenant
- Políticas internas do projeto: valores monetários como centavos inteiros (DEC-011), sem PII em logs, `AuditLog` imutável
- Padrões de segurança GCP: Secret Manager, Workload Identity Federation, Cloud Armor
- Nenhuma obrigação regulatória setorial adicional foi identificada nos insumos disponíveis; a confirmar com equipe jurídica da Vellus (LAC-07; detalhamento no `nfrd.md`)

---

## 9. Restrições, Premissas e Lacunas

### 9.1 Restrições

### REST-01 — Billing SaaS fora do escopo do MVP

O sistema não deve incluir nenhum módulo de cobrança, assinatura, faturamento ou integração com gateway de pagamento nesta versão.

- **Origem:** DEC-013 — decisão de escopo aprovada pelo Milton em 10/06/2026
- **Impacto:** sem módulo de pricing ou billing no MVP; a estrutura multi-tenant e o rate-limit por tenant (Redis) estão desenhados para suportar diferenciação de planos futura sem retrabalho
- **Consequência se não atendida:** escopo e prazo do MVP aumentam significativamente antes de qualquer validação de produto

### REST-02 — White-label estrito: apenas logo, favicon, slug, cor primária e cor secundária

Nenhum tenant pode personalizar CSS, fontes, layouts ou componentes visuais além dos cinco elementos configuráveis definidos em DEC-004.

- **Origem:** DEC-004 — decisão de produto aprovada pelo Milton em 10/06/2026
- **Impacto:** custo de manutenção de UI linear ao número de tenants; validação de contraste WCAG AA é obrigatória no upload de cores
- **Consequência se não atendida:** explosão de combinações de UI a testar e manter; risco de acessibilidade em cores customizadas fora do controle do produto

### REST-03 — Parceiro comissionado sem login no MVP

O parceiro é uma entidade de dados gerida pelo tenant; não possui credencial de acesso ao sistema no MVP.

- **Origem:** DEC-012 — decisão de escopo aprovada pelo Milton em 10/06/2026
- **Impacto:** simplifica a Fase 1; portal do parceiro (deal registration) é evolução futura no horizonte
- **Consequência se não atendida:** modelo de acesso externo exige Identity Platform adicional, telas de portal e revisão de RBAC

### REST-04 — Lead como estágio do funil, não entidade separada, no v1

Lead não é uma entidade distinta no modelo de domínio do MVP; é o primeiro estágio configurável do pipeline de oportunidades.

- **Origem:** DEC-007 — decisão que espelha a operação atual da Vellus; registrada para revisão futura
- **Impacto:** simplifica o modelo de domínio no MVP; deve ser revisitada ao implementar portal do parceiro ou lead inbound
- **Consequência se não atendida:** migração de dados futura necessária caso Lead vire entidade separada após dados em produção

### REST-05 — Multi-tenancy pooled com RLS Postgres como estratégia de isolamento

A estratégia de isolamento é pool + RLS (não schema separado por tenant). Mudança após deploy em produção seria um incidente de alto custo.

- **Origem:** DEC-006 — decisão arquitetural aprovada pelo Milton e confirmada pela research adversarial (3 alegações, 0 refutadas)
- **Impacto:** define arquitetura de dados, middleware .NET, testes de isolamento em CI e padrões de acesso ao banco
- **Consequência se não atendida:** schema físico e testes de isolamento precisam ser completamente redesenhados

---

### 9.2 Premissas

### PRM-01 — GCP Identity Platform multi-tenant disponível na região southamerica-east1 via Terraform

A Terraform do GCP suporta Identity Platform multi-tenant na região-alvo sem limitações técnicas relevantes.

- **Dependência associada:** GCP / Terraform provider
- **Impacto se a premissa falhar:** estratégia de autenticação multi-tenant exige revisão; alternativas (Auth0, Firebase Auth direto) precisam ser avaliadas

### PRM-02 — Vellus define owners para as 64 oportunidades sem responsável antes do import definitivo

A triagem manual é pré-condição para o critério de conclusão da Fase 1 ("100% das oportunidades migradas com owner definido").

- **Dependência associada:** equipe da Vellus — gestores de BU (LAC-02)
- **Impacto se a premissa falhar:** critério de conclusão da Fase 1 não é atingido; import concluído com flags de triagem pendente exigiria renegociação do critério

### PRM-03 — SPF/DKIM/DMARC do domínio mail.azim.com.br configurados antes do primeiro envio em produção

A entregabilidade do digest depende de configuração de DNS correta antes do go-live.

- **Dependência associada:** gestão de DNS do domínio azim.com.br; provider de e-mail selecionado (LAC-03)
- **Impacto se a premissa falhar:** digest classificado como spam; taxa de entrega abaixo de 98%; principal diferencial do produto comprometido no lançamento

### PRM-04 — Todos os valores monetários das oportunidades estão em BRL no MVP

Nenhuma oportunidade da Vellus utiliza moeda estrangeira nas fases iniciais; o modelo de dados não precisa de campo de moeda no MVP.

- **Dependência associada:** equipe comercial da Vellus (LAC-05)
- **Impacto se a premissa falhar:** necessidade de adicionar campo `currency` ao modelo de dados e adaptar cálculos de forecast e comissão antes do schema freeze

### PRM-05 — A planilha Pipeline Vellus.xlsx é congelada imediatamente após a conclusão da migração

Nenhuma oportunidade nova é inserida na planilha após o início do import definitivo; o arquivo fica read-only no OneDrive.

- **Dependência associada:** decisão operacional alinhada com todos os usuários da planilha na Vellus
- **Impacto se a premissa falhar:** divergência entre planilha e Azim; necessidade de novo ciclo de import; perda de confiança no dado

---

### 9.3 Lacunas e Pontos a Validar

| Código | Lacuna ou Ponto a Validar | Impacto Potencial | Responsável | Status |
|---|---|---|---|---|
| LAC-01 | Percentuais de comissão dos 11 parceiros da Vellus (Montanaro, Salto, JV Korporate, Bus2, Finaya, Paytime, Nelson e outros) são inexistentes na planilha | Impacta RF-05, RF-06 e relatório de comissões; import deve deixar campos em branco com flag de triagem obrigatória | Equipe comercial da Vellus | Aberto |
| LAC-02 | Atribuição de owner para as 64 oportunidades (59%) sem responsável definido na planilha | Bloqueia o critério de conclusão da Fase 1 ("100% com owner"); triagem manual obrigatória antes do import definitivo | Gestores de BU da Vellus | Aberto |
| LAC-03 | Validação do provedor de e-mail transacional: Postmark (candidato primário) vs SendGrid — spike de entregabilidade, SPF/DKIM/DMARC e custo previsto no MVP | Pode confirmar ou reverter DEC-008; impacta RF-09, RF-01 e todas as notificações por e-mail | Time de Engenharia | Aberto |
| LAC-04 | Definição de pricing e planos do Azim como produto SaaS | Não bloqueia o MVP; impacta o roadmap comercial e a capacidade de onboarding de novos tenants pós-Fase 1 | Milton / Time de Produto | Aberto |
| LAC-05 | Suporte a multimoeda: a Vellus pode fechar negócios em USD ou EUR antes da Fase 2 | Exige campo `currency` no modelo de dados, conversão de forecast e adaptação de comissão; deve ser decidido antes do schema freeze | Equipe comercial da Vellus | Aberto |
| LAC-06 | Calendário de feriados para o digest (Fase 2): quais feriados, para quais países e estados, e de qual fonte serão obtidos | Impacta RF-09 na Fase 2; pode exigir integração com API de feriados ou lista curada por tenant | Time de Produto | Aberto |
| LAC-07 | Base legal LGPD para tratamento de dados pessoais de contatos e política de retenção de dados | Impacta RF-04 e conformidade (seções 8.4 e 8.8); originalmente registrada apenas inline — formalizada nesta validação; detalhamento no `nfrd.md` | Equipe jurídica da Vellus / Time de Produto | Aberto |

---

## 10. Riscos e Mitigações

### 10.1 Riscos de Produto e Negócio

### RISCO-P01 — Migração incompleta bloqueia critério de conclusão da Fase 1

- **Descrição:** as 64 oportunidades sem owner (LAC-02) exigem triagem manual obrigatória; se a Vellus não completar a triagem antes do import definitivo, o critério de conclusão da Fase 1 ("100% com owner definido") não é atingido
- **Probabilidade:** Alta (evidência direta: 59% das oportunidades sem owner na planilha)
- **Impacto:** Alto (atraso no lançamento; possível retorno parcial à planilha durante a espera)
- **Categoria:** Produto
- **Mitigação:** import em duas etapas obrigatórias — dry-run com relatório de flags + triagem assistida com interface de atribuição em massa; critério pode ser renegociado para excluir oportunidades definitivamente encerradas antes da migração
- **Responsável:** Gestores de BU da Vellus + Time de Produto
- **Indicador de monitoramento:** % de oportunidades com owner atribuído após dry-run; meta: 100% antes do import definitivo

### RISCO-P02 — Abandono do produto após migração (retorno à planilha)

- **Descrição:** usuários migram os dados mas continuam usando a planilha por hábito; o digest diário isolado não é suficiente para criar o novo hábito sem planilha congelada e acompanhamento de adoção
- **Probabilidade:** Média
- **Impacto:** Alto (produto não gera valor; Fase 1 não cumpre seu objetivo de substituição)
- **Categoria:** Produto
- **Mitigação:** planilha congelada (read-only) imediatamente após o import; critério de conclusão inclui 14 dias sem retorno; acompanhamento ativo de DAU e taxa de abertura do digest
- **Responsável:** Milton / Time de Produto
- **Indicador de monitoramento:** DAU >= 80% dos usuários cadastrados por 14 dias consecutivos após go-live (KPI-09); taxa de abertura do digest >= 30% (KPI-04)

---

### 10.2 Riscos Técnicos

### RISCO-T01 — Vazamento de dados entre tenants (falha de RLS ou de global query filter)

- **Descrição:** bug no EF Core global query filter ou na variável de runtime do RLS Postgres permite que tenant A leia dados do tenant B
- **Probabilidade:** Baixa (defesa em profundidade; testes de isolamento em CI como gate)
- **Impacto:** Crítico (violação de confidencialidade comercial e potencial LGPD; incidente sev-1 — DEC-006)
- **Categoria:** Técnico
- **Mitigação:** testes de isolamento automatizados em CI como gate obrigatório (sem merge sem 100% aprovação); alertas de violação de RLS em produção; code review obrigatório em mudanças de middleware de tenant
- **Responsável:** Engenharia
- **Indicador de monitoramento:** 0 falhas de isolamento nos testes de CI; 0 alertas de violação de RLS em produção (KPI-06)

### RISCO-T02 — Falha de entrega do digest diário (spam, bounce ou erro de configuração)

- **Descrição:** SPF/DKIM/DMARC incorretos, má reputação de IP do provider ou bounce rate elevado fazem o digest ser classificado como spam — o principal diferencial do produto perde efeito no lançamento
- **Probabilidade:** Média (depende da execução da Fase 0 de e-mail e do spike de validação — LAC-03)
- **Impacto:** Alto (principal diferencial comprometido no lançamento; vendedores perdem o to-do diário)
- **Categoria:** Técnico
- **Mitigação:** SPF/DKIM/DMARC validados na Fase 0 antes de qualquer envio em produção; spike de seleção de provider com critério de entregabilidade >= 93,8%; supressão e bounce handling implementados; alerta operacional para taxa de falha > 2%
- **Responsável:** Engenharia
- **Indicador de monitoramento:** taxa de entrega do digest >= 98% (KPI-03); 0 alertas de bounce rate > 5% nos primeiros 30 dias

### RISCO-T03 — Performance degradada do Kanban com crescimento de oportunidades

- **Descrição:** sem índices adequados e paginação, o Kanban fica lento à medida que o número de oportunidades por tenant cresce além das 108 iniciais
- **Probabilidade:** Baixa (volume inicial pequeno; arquitetura com RLS e EF Core global filter)
- **Impacto:** Médio (experiência degradada; adoção comprometida)
- **Categoria:** Técnico
- **Mitigação:** paginação e lazy loading no Kanban desde o MVP; índices compostos por `tenant_id + bu_id + stage_id`; load test com 2.000 oportunidades antes do go-live
- **Responsável:** Engenharia
- **Indicador de monitoramento:** latência do Kanban <= 2 s (p95) em load test com 2.000 oportunidades (KPI-05)

---

### 10.3 Riscos Operacionais

### RISCO-O01 — Vellus acumula dupla responsabilidade (primeiro cliente e operador da plataforma)

- **Descrição:** os sócios e a equipe da Vellus precisam simultaneamente operar o CRM como usuários (Fase 1) e operar a plataforma como produto SaaS para outros tenants (Fases 2+); sobrecarga pode atrasar decisões e a expansão
- **Probabilidade:** Média
- **Impacto:** Médio (atraso na expansão para novos tenants; sobrecarga de recursos)
- **Categoria:** Operacional
- **Mitigação:** RBAC separa claramente o papel de Platform Operator dos papéis de negócio; backoffice do operador não interfere na operação do tenant; segundo tenant apenas após Fase 1 estabilizada por >= 30 dias
- **Responsável:** Milton
- **Indicador de monitoramento:** Fase 1 estabilizada por >= 30 dias antes de onboarding do segundo tenant

### RISCO-O02 — Percentuais de comissão dos parceiros não definidos antes da migração

- **Descrição:** os 11 parceiros da Vellus com 24 oportunidades associadas não têm percentuais de comissão na planilha; se não definidos na triagem, o relatório de comissões da Fase 1 estará incompleto
- **Probabilidade:** Alta (evidência direta: ausência de percentuais na planilha)
- **Impacto:** Médio (relatório de comissões incompleto; RF-05 entregue com dados parciais)
- **Categoria:** Operacional
- **Mitigação:** dry-run lista explicitamente parceiros sem percentuais e marca oportunidades afetadas; triagem assistida inclui tela de definição de percentuais antes do import definitivo (LAC-01)
- **Responsável:** Equipe comercial da Vellus + Time de Produto
- **Indicador de monitoramento:** >= 80% dos parceiros com percentuais definidos antes do import definitivo (KPI-07)

---

### 10.4 Matriz Consolidada de Riscos

| Código | Risco | Categoria | Probabilidade | Impacto | Severidade | Mitigação Principal |
|---|---|---|---|---|---|---|
| RISCO-P01 | Migração incompleta — owners faltantes | Produto | Alta | Alto | Alta | Dry-run + triagem assistida; renegociação do critério |
| RISCO-P02 | Abandono do produto após migração | Produto | Média | Alto | Alta | Planilha congelada; DAU monitorado; digest como âncora |
| RISCO-T01 | Vazamento de dados entre tenants | Técnico | Baixa | Crítico | Alta | Testes de isolamento em CI; alertas de RLS |
| RISCO-T02 | Falha de entrega do digest | Técnico | Média | Alto | Alta | SPF/DKIM/DMARC na Fase 0; spike de provider |
| RISCO-T03 | Kanban lento com crescimento de dados | Técnico | Baixa | Médio | Média | Índices compostos; load test; paginação |
| RISCO-O01 | Dupla responsabilidade da Vellus | Operacional | Média | Médio | Média | RBAC separado; segundo tenant só após 30 dias estável |
| RISCO-O02 | Comissões de parceiros sem percentuais | Operacional | Alta | Médio | Alta | Dry-run com flag; triagem obrigatória no import |

---

## 11. Métricas de Sucesso

> As métricas devem medir o sucesso do produto após a implantação. Todas são objetivas, mensuráveis e associadas a objetivos estratégicos.

### 11.1 Métricas de Produto

### KPI-01 — Taxa de oportunidades com owner definido

- **Objetivo relacionado:** OBJ-01
- **Meta:** 100% das oportunidades ativas com `owner_id` preenchido ao término da Fase 1 (vs 41% atual na planilha)
- **Medição:** `COUNT(*) WHERE owner_id IS NOT NULL / COUNT(*) × 100` no tenant Vellus após import definitivo
- **Fonte de dados:** Cloud SQL / banco de dados Azim
- **Periodicidade:** verificação pontual ao término da migração; monitoramento mensal após o go-live

### KPI-02 — Volume de oportunidades migradas

- **Objetivo relacionado:** OBJ-01
- **Meta:** 108 oportunidades importadas e validadas (100%) ao término da Fase 1
- **Medição:** `COUNT(*)` de oportunidades no tenant Vellus após import definitivo com status confirmado no relatório final
- **Fonte de dados:** Cloud SQL / relatório de migração auditado
- **Periodicidade:** verificação pontual ao término da migração

### KPI-07 — Cobertura de comissão por oportunidade com parceiro

- **Objetivo relacionado:** OBJ-02
- **Meta:** >= 80% das oportunidades com `partner_id` associado têm `pct_setup` ou `pct_recorrente` definidos ao término da Fase 1
- **Medição:** `COUNT(*) WHERE partner_id IS NOT NULL AND (pct_setup IS NOT NULL OR pct_recorrente IS NOT NULL) / COUNT(*) WHERE partner_id IS NOT NULL × 100`
- **Fonte de dados:** Cloud SQL / banco de dados Azim
- **Periodicidade:** verificação pontual ao término da migração; monitoramento mensal

---

### 11.2 Métricas Operacionais

### KPI-03 — Taxa de entrega do digest diário

- **Objetivo relacionado:** OBJ-03
- **Meta:** >= 98% dos digests agendados entregues com sucesso (sem bounce, sem falha de envio) nos primeiros 30 dias de produção
- **Medição:** `(EmailDigestLog WHERE status = 'delivered') / (EmailDigestLog WHERE status IN ('scheduled', 'sent', 'delivered', 'failed')) × 100`
- **Fonte de dados:** `EmailDigestLog` + webhooks do provider de e-mail (Postmark ou SendGrid)
- **Periodicidade:** diária (alerta automático se taxa cair abaixo de 95% por mais de 1 hora)

### KPI-04 — Taxa de abertura do digest diário

- **Objetivo relacionado:** OBJ-03
- **Meta:** >= 30% de taxa de abertura nos primeiros 30 dias de produção
- **Medição:** `(eventos 'opened' recebidos via webhook do provider) / (e-mails entregues) × 100`
- **Fonte de dados:** webhooks do provider de e-mail
- **Periodicidade:** semanal

### KPI-08 — Usuários ativos semanais (WAU) entre gestores e executivos

- **Objetivo relacionado:** OBJ-04
- **Meta:** >= 80% dos usuários com papel de Gestor de BU (P-02) ou Executivo do Tenant (P-03) acessam o painel ao menos 1 vez por semana nos 30 dias pós-go-live
- **Medição:** contagem de sessões únicas por usuário/semana filtradas pelos papéis P-02 e P-03
- **Fonte de dados:** Cloud Logging (eventos de sessão autenticada com papel identificado)
- **Periodicidade:** semanal

### KPI-09 — Operação sem retorno à planilha por 14 dias consecutivos

- **Objetivo relacionado:** OBJ-01
- **Meta:** critério de conclusão da Fase 1 — a Vellus opera as 3 BUs integralmente no Azim por >= 14 dias sem criar ou atualizar oportunidades na planilha
- **Medição:** data de última modificação do arquivo `Pipeline Vellus.xlsx` no OneDrive (deve ser anterior ao go-live) + DAU >= 80% dos usuários cadastrados por 14 dias consecutivos
- **Fonte de dados:** OneDrive (metadata do arquivo) + Cloud Logging (DAU no Azim)
- **Periodicidade:** verificação pontual no D+14 após o go-live

---

### 11.3 Métricas Técnicas

### KPI-05 — Latência do Kanban (p95)

- **Objetivo relacionado:** OBJ-01 (qualidade da substituição da planilha)
- **Meta:** <= 2 segundos (p95) em carga normal; validado também em load test com até 2.000 oportunidades por tenant (consistente com o indicador de RISCO-T03)
- **Medição:** latência p95 do endpoint de carregamento do Kanban medida via Cloud Trace
- **Fonte de dados:** Cloud Monitoring / Cloud Trace
- **Periodicidade:** contínua (alerta automático se p95 > 2 s por > 5 minutos)

### KPI-06 — Aprovação dos testes de isolamento de tenant em CI

- **Objetivo relacionado:** OBJ-05
- **Meta:** 100% de aprovação do suite de testes de isolamento em CI a cada build; 0 regressões em produção
- **Medição:** resultado do gate de isolamento no pipeline CI (GitHub Actions) — build bloqueado em caso de falha
- **Fonte de dados:** GitHub Actions CI pipeline
- **Periodicidade:** a cada build (contínua)

---

### 11.4 Métricas de Adoção

### KPI-10 — DAU — Usuários Ativos Diários

- **Objetivo relacionado:** OBJ-01
- **Meta:** >= 80% dos usuários cadastrados com papel ativo (Vendedor ou acima) acessam o sistema ao menos 1 vez por dia nos 30 dias pós-go-live
- **Medição:** contagem de usuários únicos com pelo menos 1 evento de sessão autenticada por dia
- **Fonte de dados:** Cloud Logging (eventos de sessão)
- **Periodicidade:** diária

---

### 11.5 Tabela Consolidada de KPIs

| Código | KPI | Objetivo Relacionado | Meta | Fonte | Periodicidade |
|---|---|---|---|---|---|
| KPI-01 | Taxa de oportunidades com owner | OBJ-01 | 100% | Cloud SQL | Pontual + mensal |
| KPI-02 | Volume migrado | OBJ-01 | 108/108 | Cloud SQL / relatório | Pontual |
| KPI-03 | Taxa de entrega do digest | OBJ-03 | >= 98% | EmailDigestLog + webhooks | Diária |
| KPI-04 | Taxa de abertura do digest | OBJ-03 | >= 30% | Webhooks do provider | Semanal |
| KPI-05 | Latência Kanban p95 | OBJ-01 | <= 2 s | Cloud Trace | Contínua |
| KPI-06 | Isolamento de tenant em CI | OBJ-05 | 100% aprovação | GitHub Actions | A cada build |
| KPI-07 | Cobertura de comissão por parceiro | OBJ-02 | >= 80% | Cloud SQL | Pontual + mensal |
| KPI-08 | WAU gestores e executivos | OBJ-04 | >= 80% | Cloud Logging | Semanal |
| KPI-09 | 14 dias sem retorno à planilha | OBJ-01 | 14 dias consecutivos | OneDrive + DAU | Pontual (D+14) |
| KPI-10 | DAU usuários ativos | OBJ-01 | >= 80% | Cloud Logging | Diária |

---

## 12. Documentos Relacionados

| Documento | Descrição | Status |
|---|---|---|
| `docs/product/prd/frd.md` | Requisitos Funcionais Detalhados — RF-01 a RF-13: regras de negócio, fluxos, validações, mensagens e critérios de aceite | A criar |
| `docs/product/prd/nfrd.md` | Requisitos Não Funcionais Detalhados — SLOs, SLAs, segurança, LGPD, observabilidade, escalabilidade e capacidade | A criar |
| `docs/product/prd/TRD.md` | Requisitos Técnicos — stack GCP/.NET/React, multi-tenancy, integrações, APIs, CI/CD, persistência | A criar |
| `docs/product/prd/ADR.md` | Decisões Arquiteturais — DEC-001 a DEC-013 são candidatos a ADRs formais | A criar |
| `docs/product/prd/UXD.md` | Experiência do Usuário — fluxos Kanban, digest, automação visual (React Flow), design tokens e diretrizes de interface | A criar |
| `docs/product/azim-product-spec.md` | Especificação mestre do produto Azim CRM v1.0 (aprovada para fundação) — fonte de verdade técnica e funcional detalhada | Aprovado |
| `docs/research/analise-planilha-pipeline-vellus.md` | Análise quantitativa da planilha `Pipeline Vellus.xlsx` — evidência primária do problema (108 opps, 3 BUs, dores mensuradas) | Aprovado |
| `docs/research/deep-research-crm-multitenant.md` | Research adversarial — 115 alegações, 25 verificadas; valida gap de comissão e arquitetura multi-tenant GCP | Aprovado |
| `docs/research/analise-video-saas-multitenant.md` | Análise do vídeo TeamForge — lições de arquitetura SaaS multi-tenant com React Flow validadas na prática | Aprovado |
| `docs/discovery/discovery-notes.md` | Notas de discovery consolidadas — insumo primário deste PRD; 13 decisões aprovadas e 6 pontos a validar | Aprovado |

---

## 13. Anexos

### Anexo A — Glossário

| Termo | Definição |
|---|---|
| Tenant | Empresa cliente do Azim CRM; cada tenant é isolado dos demais por RLS no banco de dados |
| BU (Business Unit) | Unidade de negócio dentro de um tenant; tem pipeline, estágios e configurações próprias |
| Owner | Responsável por uma oportunidade; campo obrigatório em toda oportunidade no Azim |
| Forecast ponderado | Valor total da oportunidade multiplicado pela probabilidade do estágio atual |
| Digest | E-mail diário automatizado com pendências comerciais; enviado às 07:00 BRT de seg–sex |
| Azimute | Metáfora da marca: referência de direção e orientação; origem do nome "Azim" |
| Snapshot de comissão | Registro imutável dos percentuais e valores de comissão calculados no momento em que a oportunidade é movida para "Ganho" |
| Pool + RLS | Estratégia de multi-tenancy onde todos os tenants compartilham o banco de dados; isolamento garantido por Row-Level Security do Postgres |
| Integer cents | Convenção de armazenamento de valores monetários como inteiros em centavos (ex.: R$ 1.000,00 = 100.000 centavos) |
| White-label estrito | Personalização visual limitada a logo, favicon, slug, cor primária e cor secundária — sem CSS ou layout por tenant |
| IEmailSender | Abstração de interface para envio de e-mail; permite troca de provider (Postmark/SendGrid) sem mudança no código de negócio |
| RBAC | Role-Based Access Control — controle de acesso por papel: Platform Operator, Tenant Admin, Gestor de BU, Vendedor, Viewer |
| Dry-run | Execução de simulação do import sem efetuar alterações no banco; gera relatório de validação |
| AuditLog | Tabela imutável que registra toda escrita em entidade de negócio com autor, delta JSON e timestamp |
| EmailDigestLog | Registro de idempotência do digest; evita envio duplicado mesmo em retentativas |
| OpportunityPartnerCommission | Entidade de domínio que armazena percentuais e o snapshot imutável de comissão vinculados a uma oportunidade |
| Azimute da semana | Digest de segunda-feira com visão executiva do pipeline global, variação semanal e bloco de metas |
| Graceful degradation | Comportamento do sistema quando dado opcional está ausente: exibe o que está disponível sem erro conspícuo (ex.: painel sem meta cadastrada) |

### Anexo B — Siglas

| Sigla | Significado |
|---|---|
| CRM | Customer Relationship Management |
| SaaS | Software as a Service |
| BU | Business Unit (Unidade de Negócio) |
| RBAC | Role-Based Access Control |
| RLS | Row-Level Security |
| GCP | Google Cloud Platform |
| IaC | Infrastructure as Code |
| LGPD | Lei Geral de Proteção de Dados |
| WCAG | Web Content Accessibility Guidelines |
| REST | Representational State Transfer |
| CI | Continuous Integration |
| CD | Continuous Deployment |
| IANA | Internet Assigned Numbers Authority (padrão de nomes de fuso horário) |
| SPF | Sender Policy Framework |
| DKIM | DomainKeys Identified Mail |
| DMARC | Domain-based Message Authentication, Reporting and Conformance |
| MVP | Minimum Viable Product |
| DAU | Daily Active Users |
| WAU | Weekly Active Users |
| KPI | Key Performance Indicator |
| SLO | Service Level Objective |
| SLA | Service Level Agreement |
| PRD | Product Requirements Document |
| FRD | Functional Requirements Document |
| NFRD | Non-Functional Requirements Document |
| TRD | Technical Requirements Document |
| ADR | Architecture Decision Record |
| UXD | User Experience Document |
| WAF | Web Application Firewall |
| DST | Daylight Saving Time |
| PII | Personally Identifiable Information |
| PITR | Point-In-Time Recovery |
| RPO | Recovery Point Objective |
| RTO | Recovery Time Objective |

### Anexo C — Referências

- `docs/discovery/discovery-notes.md` — Notas de discovery consolidadas; insumo primário deste PRD (2026-06-10)
- `docs/product/azim-product-spec.md` — Especificação mestre do produto Azim CRM v1.0; aprovada para fundação
- `docs/research/analise-planilha-pipeline-vellus.md` — Análise quantitativa da planilha `Pipeline Vellus.xlsx`
- `docs/research/deep-research-crm-multitenant.md` — Research adversarial: 5 frentes, 115 alegações, 25 verificadas adversarialmente
- `docs/research/analise-video-saas-multitenant.md` — Análise do vídeo TeamForge (SaaS multi-tenant com React Flow)
- LGPD — Lei nº 13.709, de 14 de agosto de 2018
- WCAG 2.1 — Web Content Accessibility Guidelines, nível AA, W3C Recommendation
- IIBA — A Guide to the Business Analysis Body of Knowledge (BABOK Guide v3)
- IIBA — Agile Extension to the BABOK Guide
