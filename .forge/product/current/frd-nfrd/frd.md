# FRD — Azim CRM

**Produto:** Azim CRM
**Versão:** v1.0
**Data:** 2026-06-10
**Status:** Rascunho para revisão
**Fonte Principal:** docs/product/prd/prd.md

---

## Controle de Versão

| Versão | Data | Descrição |
|---|---|---|
| v1.0 | 2026-06-10 | Criação inicial do FRD a partir do PRD v0.1 e das discovery notes |

---

## Sumário

1. [Introdução](#1-introdução)
2. [Objetivo do Documento](#2-objetivo-do-documento)
3. [Referências](#3-referências)
4. [Visão Geral Funcional](#4-visão-geral-funcional)
5. [Escopo Funcional](#5-escopo-funcional)
6. [Fora de Escopo](#6-fora-de-escopo)
7. [Personas e Atores](#7-personas-e-atores)
8. [Jornadas Funcionais](#8-jornadas-funcionais)
9. [Módulos Funcionais](#9-módulos-funcionais)
10. [Requisitos Funcionais — Tabela Resumida](#10-requisitos-funcionais--tabela-resumida)
11. [Detalhamento dos Requisitos Funcionais](#11-detalhamento-dos-requisitos-funcionais)
12. [Casos de Uso](#12-casos-de-uso)
13. [Regras de Negócio](#13-regras-de-negócio)
14. [Mensagens de Erro e Validação](#14-mensagens-de-erro-e-validação)
15. [Matriz de Permissões Funcionais](#15-matriz-de-permissões-funcionais)
16. [Matriz de Rastreabilidade](#16-matriz-de-rastreabilidade)
17. [Dependências Funcionais](#17-dependências-funcionais)
18. [Premissas](#18-premissas)
19. [Pontos a Validar](#19-pontos-a-validar)
20. [Anexos](#20-anexos)

---

## 1. Introdução

O **Azim CRM** é uma plataforma SaaS multi-tenant de gestão comercial B2B construída para substituir a operação da Vellus — primeiro tenant e operador da plataforma — que hoje conduz 108 oportunidades ativas (R$ 9.013.250 em forecast ponderado) em uma planilha Excel compartilhada sem controle de acesso, sem trilha de auditoria e com 59% das oportunidades sem responsável definido.

Este documento detalha funcionalmente os treze requisitos funcionais (RF-01 a RF-13) do PRD v0.1, traduzindo-os em requisitos verificáveis, casos de uso, regras de negócio, fluxos, mensagens e critérios de aceite prontos para apoiar arquitetura, engenharia, QA e UX.

---

## 2. Objetivo do Documento

Este FRD responde:

- O que o sistema Azim CRM deve fazer funcionalmente?
- Para quem cada funcionalidade se destina?
- Em quais jornadas cada funcionalidade é acionada?
- Sob quais regras de negócio cada funcionalidade opera?
- Quais entradas e saídas cada funcionalidade exige?
- Quais fluxos principais, alternativos e de exceção existem?
- Quais mensagens de erro e feedback devem ser exibidos?
- Quais critérios permitem verificar se a funcionalidade foi corretamente implementada?

O FRD não define arquitetura técnica, não cria requisitos não funcionais e não altera decisões de produto já registradas no PRD.

---

## 3. Referências

| Documento | Caminho | Observação |
|---|---|---|
| PRD | `docs/product/prd/prd.md` | Fonte principal — versão v0.1 de 2026-06-10 |
| Discovery Notes | `docs/discovery/discovery-notes.md` | Insumo complementar; 13 decisões aprovadas (DEC-001..DEC-013) e 6 pontos a validar (VAL-001..VAL-006) |
| Especificação mestre | `docs/product/azim-product-spec.md` | Aprovada para fundação — fonte de verdade técnica e funcional |
| Análise da planilha | `docs/research/analise-planilha-pipeline-vellus.md` | Evidência primária do problema (108 opps, 3 BUs) |
| Research adversarial | `docs/research/deep-research-crm-multitenant.md` | 115 alegações, 25 verificadas; confirma gap de comissão |

---

## 4. Visão Geral Funcional

O Azim CRM entrega cinco capacidades funcionais nucleares ao tenant:

1. **Pipeline estruturado com dono obrigatório:** toda oportunidade tem owner; o Kanban é o cockpit diário do vendedor; estágios configuráveis por BU com probabilidade default e regras de transição.
2. **Comissão de parceiro nativa na oportunidade:** `OpportunityPartnerCommission` por componente (setup, recorrente), com snapshot imutável ao fechar negócio — diferencial confirmado sobre Salesforce e Zoho.
3. **Digest diário acionável:** e-mail às 07:00 no fuso do tenant, de segunda a sexta, com pendências e links de 1 clique; segunda-feira inclui azimute da semana para gestores e executivos.
4. **Metas e forecast:** cadastro mensal por BU e por responsável com graceful degradation; painel de realizado vs meta vs pipeline disponível.
5. **Multi-tenancy verificada por RLS:** isolamento de dados por tenant garantido em duas camadas (EF Core + Postgres RLS); branding por tenant via design tokens.

A plataforma é organizada em módulos funcionais que correspondem diretamente aos RF-01 a RF-13 do PRD, mais o módulo de migração da planilha (obrigatório na Fase 1).

---

## 5. Escopo Funcional

| Código | Item de Escopo | Descrição | Fase |
|---|---|---|---|
| ESC-01 | Autenticação e acesso | Login e-mail/senha e Google; convite; recuperação de senha; sessão por slug de tenant | 0/1 |
| ESC-02 | Administração do tenant (white-label estrito) | Logo, favicon, cor primária, cor secundária, slug, fuso horário IANA | 1 |
| ESC-03 | BUs, usuários e papéis | CRUD de BUs; convite com RBAC por BU; desativação com histórico preservado | 1 |
| ESC-04 | Contas e contatos | CRUD com dedupe por nome normalizado; visão 360°; conta compartilhada entre BUs | 1 |
| ESC-05 | Parceiros | CRUD com percentuais default; visão de comissão projetada e consolidada | 1 |
| ESC-06 | Pipeline e oportunidades | Kanban, lista, modelo Setup+Recorrente, owner obrigatório, comissão nativa, numeração sequencial | 1 |
| ESC-07 | Atividades e follow-ups | CRUD; to-do diário; conclusão em 1 clique; oportunidades estagnadas sinalizadas | 1 |
| ESC-08 | Metas e forecast | Metas mensais por BU e por responsável; graceful degradation; painel comparativo | 1 (básico) / 2 (completo) |
| ESC-09 | Digest diário por e-mail | E-mail 07:00 BRT seg–sex; azimute na segunda; idempotência por EmailDigestLog | 1 |
| ESC-10 | Relatórios enxutos | Funil, forecast, ranking, canais, comissões, export CSV | 1 |
| ESC-11 | Migração da planilha | Dry-run → triagem assistida → import transacional → congelamento da planilha | 1 |
| ESC-12 | Trilha de auditoria | AuditLog imutável para toda escrita em entidade de negócio | 1 |
| ESC-13 | Notificações in-app | Central de notificações por eventos de domínio | 2 |
| ESC-14 | Automações visuais | Editor React Flow; nós de gatilho/condição/ação; execução assíncrona | 2 |
| ESC-15 | Agentes de IA | Scoring, resumo, próxima ação, copilot; Langfuse; isolamento por tenant | 3 |

---

## 6. Fora de Escopo

| Código | Item Fora de Escopo | Justificativa | Fonte |
|---|---|---|---|
| OOS-01 | Billing, cobrança e faturamento do Azim como SaaS | DEC-013: fora do escopo do MVP; estrutura multi-tenant suporta diferenciação futura | PRD seção 9.1 |
| OOS-02 | Portal de acesso externo para parceiros (deal registration) | DEC-012: parceiro é entidade de dados sem credencial no MVP | PRD seção 9.1 |
| OOS-03 | Múltiplos parceiros por oportunidade | Horizonte do roadmap; operação atual da Vellus não requer | PRD seção 5.2 |
| OOS-04 | Lead como entidade separada do funil | DEC-007: Lead é estágio configurável no v1 | PRD seção 9.1 |
| OOS-05 | Suporte a multimoeda | LAC-05: todos os valores assumem BRL no MVP | PRD seção 5.2 |
| OOS-06 | App mobile nativo ou PWA | Horizonte do roadmap; decisão após Fase 1 validada | PRD seção 5.2 |
| OOS-07 | Integração com WhatsApp ou canais externos | Não está no escopo de nenhuma fase comprometida | PRD seção 5.2 |
| OOS-08 | CSS, fontes ou layout customizável por tenant | DEC-004: white-label estrito — apenas 5 elementos configuráveis | PRD seção 9.1 |
| OOS-09 | Módulo de ERP, RH, financeiro ou contabilidade | Fora do domínio do CRM comercial | PRD seção 5.2 |

---

## 7. Personas e Atores

| Código | Ator/Persona | Tipo | Descrição |
|---|---|---|---|
| ACT-01 | Vendedor / Executivo de Contas (P-01) | Usuário primário | Conduz oportunidades; principal destinatário do digest; opera o Kanban diário |
| ACT-02 | Gestor de BU (P-02) | Usuário gerencial | Acompanha pipeline e metas da BU; redistribui oportunidades; recebe azimute da semana |
| ACT-03 | Executivo do Tenant (P-03) | Usuário executivo | Visão macro consolidada; recebe azimute da semana; não opera o CRM diariamente |
| ACT-04 | Administrador do Tenant / Tenant Admin (P-04) | Usuário de configuração | Configura branding, usuários, BUs, estágios, parceiros e metas |
| ACT-05 | Operador da Plataforma / Platform Operator (P-05) | Ator operacional | Provisiona tenants; monitora a plataforma; sem acesso a dados comerciais dos tenants |
| ACT-06 | Parceiro comissionado | Ator externo (dado) | Entidade de dados gerida pelo tenant; sem login no MVP (DEC-012) |
| ACT-07 | Cloud Scheduler | Sistema | Dispara o job horário UTC para seleção dos destinatários do digest |
| ACT-08 | Provider de e-mail (IEmailSender) | Sistema externo | Entrega o digest e demais e-mails transacionais; retorna eventos de entrega/abertura |

---

## 8. Jornadas Funcionais

| Código | Jornada | Ator Principal | Descrição |
|---|---|---|---|
| JRN-01 | Vendedor gerencia o pipeline do dia | ACT-01 | Acessa o Kanban da BU, avança oportunidades, registra atividades e agenda a próxima ação |
| JRN-02 | Vendedor age pelo digest sem abrir o sistema | ACT-01, ACT-07, ACT-08 | Às 07:00, recebe e-mail com pendências e age via link autenticado de 1 clique |
| JRN-03 | Gestor de BU acompanha pipeline e metas | ACT-02 | Visualiza painel da BU, compara realizado vs meta, identifica estagnações e redistribui |
| JRN-04 | Migração da planilha Pipeline Vellus.xlsx | ACT-04, ACT-01, ACT-02 | Upload → dry-run → triagem assistida → import transacional → planilha congelada |
| JRN-05 | Comissão de parceiro é registrada e congelada ao fechar negócio | ACT-01, ACT-02 | Registra percentuais, confirma cálculo, move para Ganho e gera snapshot imutável |

---

## 9. Módulos Funcionais

| Código | Módulo Funcional | Descrição | RF Relacionado | Fase |
|---|---|---|---|---|
| MOD-01 | Autenticação e Acesso | Login, convite, recuperação de senha, sessão por tenant | RF-01 | 0/1 |
| MOD-02 | Administração do Tenant | White-label estrito: branding, slug, fuso horário | RF-02 | 1 |
| MOD-03 | BUs, Usuários e Papéis | CRUD de BUs, convite com RBAC por BU, desativação | RF-03 | 1 |
| MOD-04 | Contas e Contatos | CRUD com dedupe, visão 360°, conta compartilhada | RF-04 | 1 |
| MOD-05 | Parceiros | CRUD com percentuais default, visão de comissão | RF-05 | 1 |
| MOD-06 | Pipeline e Oportunidades | Kanban, modelo Setup+Recorrente, owner obrigatório, comissão nativa, numeração | RF-06 | 1 |
| MOD-07 | Atividades e Follow-ups | CRUD, to-do diário, conclusão 1 clique, estagnações | RF-07 | 1 |
| MOD-08 | Metas e Forecast | Metas mensais por BU/responsável, graceful degradation | RF-08 | 1/2 |
| MOD-09 | Digest Diário por E-mail | E-mail diário acionável, azimute da semana, idempotência | RF-09 | 1 |
| MOD-10 | Notificações In-App | Central de notificações por eventos de domínio | RF-10 | 2 |
| MOD-11 | Relatórios | Funil, forecast, ranking, canais, comissões, export CSV | RF-11 | 1 |
| MOD-12 | Automações Visuais | Editor React Flow, execução assíncrona via barramento de eventos | RF-12 | 2 |
| MOD-13 | Agentes de IA | Scoring, resumo, próxima ação, copilot, Langfuse | RF-13 | 3 |
| MOD-14 | Migração de Planilha | Dry-run, triagem assistida, import transacional, congelamento | RF-06 / Fase 1 | 1 |

---

## 10. Requisitos Funcionais — Tabela Resumida

| Código | Requisito Funcional | Descrição | Prioridade | RF PRD | Fase |
|---|---|---|---|---|---|
| FRD-auth-01 | Autenticar usuário por e-mail/senha | Permite login com credencial e-mail/senha no tenant resolvido por slug | Must Have | RF-01 | 0/1 |
| FRD-auth-02 | Autenticar usuário via conta Google | Permite login social Google via Identity Platform multi-tenant | Must Have | RF-01 | 0/1 |
| FRD-auth-03 | Convidar usuário por e-mail | Envia convite com link temporário de ativação; define papel e BU(s) | Must Have | RF-01 | 1 |
| FRD-auth-04 | Recuperar senha por e-mail | Envia link de redefinição de senha com expiração | Must Have | RF-01 | 1 |
| FRD-auth-05 | Encerrar sessão com invalidação global | Logout invalida sessão em todos os dispositivos | Must Have | RF-01 | 1 |
| FRD-admin-01 | Configurar branding do tenant | Upload de logo, favicon e definição de cores com validação WCAG AA | Must Have | RF-02 | 1 |
| FRD-admin-02 | Definir slug e fuso horário do tenant | Slug imutável após definição; fuso IANA utilizado pelo digest | Must Have | RF-02 | 1 |
| FRD-org-01 | Gerenciar BUs | CRUD de BUs com nome, estágios, canais e motivos de perda configuráveis | Must Have | RF-03 | 1 |
| FRD-org-02 | Gerenciar usuários e papéis | Convite, atribuição de papel por BU, desativação sem exclusão física | Must Have | RF-03 | 1 |
| FRD-account-01 | Gerenciar contas | CRUD com busca e dedupe por nome normalizado | Must Have | RF-04 | 1 |
| FRD-account-02 | Gerenciar contatos | CRUD de contatos vinculados à conta | Must Have | RF-04 | 1 |
| FRD-account-03 | Exibir visão 360° da conta | Oportunidades de todas as BUs visíveis, contatos, atividades, histórico | Must Have | RF-04 | 1 |
| FRD-partner-01 | Gerenciar parceiros | CRUD com papel tipado e percentuais default por componente | Must Have | RF-05 | 1 |
| FRD-partner-02 | Exibir visão de comissão do parceiro | Comissão projetada (pipeline) e consolidada (Ganhas) por período | Must Have | RF-05 | 1 |
| FRD-pipeline-01 | Exibir Kanban de oportunidades por BU | Colunas de estágio com drag-and-drop; soma de valor total e forecast ponderado | Must Have | RF-06 | 1 |
| FRD-pipeline-02 | Criar oportunidade | Formulário completo e criação rápida com conta inline; owner obrigatório; numeração automática | Must Have | RF-06 | 1 |
| FRD-pipeline-03 | Editar oportunidade | Atualização de campos da oportunidade com validação por estágio | Must Have | RF-06 | 1 |
| FRD-pipeline-04 | Mover oportunidade de estágio | Drag-and-drop com validação de regras de transição; registra transição na linha do tempo | Must Have | RF-06 | 1 |
| FRD-pipeline-05 | Registrar comissão de parceiro | `pct_setup`, `pct_recorrente`, `valor_fixo`, `meses_comissionados` por oportunidade | Must Have | RF-06 | 1 |
| FRD-pipeline-06 | Fechar oportunidade como Ganho | Gera snapshot imutável de comissão; registra na AuditLog | Must Have | RF-06 | 1 |
| FRD-pipeline-07 | Fechar oportunidade como Perdido | Exige motivo de perda obrigatório; registra na AuditLog | Must Have | RF-06 | 1 |
| FRD-pipeline-08 | Reabrir oportunidade Ganha ou Perdida | Apenas Tenant Admin e Gestor de BU; preserva snapshot original | Must Have | RF-06 | 1 |
| FRD-pipeline-09 | Exibir visão lista de oportunidades | Filtros salvos por owner, canal, parceiro, estágio, data de fechamento, estagnadas | Must Have | RF-06 | 1 |
| FRD-activity-01 | Registrar atividade | CRUD de atividades com tipo, data, responsável, oportunidade/conta vinculada | Must Have | RF-07 | 1 |
| FRD-activity-02 | Exibir to-do diário | Vencidas, hoje, próximas — visão "Meu dia / Minha semana" | Must Have | RF-07 | 1 |
| FRD-activity-03 | Concluir atividade em 1 clique | A partir da lista ou do link autenticado do digest | Must Have | RF-07 | 1 |
| FRD-activity-04 | Sugerir próxima atividade | Ao concluir uma atividade, sistema sugere criação da próxima | Must Have | RF-07 | 1 |
| FRD-goal-01 | Cadastrar meta mensal | Meta por BU e/ou responsável com granularidade mensal | Must Have | RF-08 | 1 |
| FRD-goal-02 | Exibir painel de metas e forecast | Realizado vs meta vs pipeline disponível; graceful degradation sem meta | Must Have | RF-08 | 1 |
| FRD-digest-01 | Selecionar destinatários do digest | Por fuso IANA do tenant, dia útil (seg–sex) e presença de pendências | Must Have | RF-09 | 1 |
| FRD-digest-02 | Enviar digest de terça a sexta | Atividades vencidas, de hoje, oportunidades estagnadas; links autenticados | Must Have | RF-09 | 1 |
| FRD-digest-03 | Enviar azimute da semana (segunda-feira) | Pipeline por estágio/BU, variação semanal, ganhos, fechamentos, bloco de metas | Must Have | RF-09 | 1 |
| FRD-digest-04 | Garantir idempotência do digest | EmailDigestLog por usuário+data impede reenvio em retentativas | Must Have | RF-09 | 1 |
| FRD-notif-01 | Exibir notificações in-app | Central de notificações com contagem de não lidas; leitura individual e em massa | Should Have | RF-10 | 2 |
| FRD-report-01 | Relatório de funil por estágio | Total de oportunidades e valor por estágio no período | Must Have | RF-11 | 1 |
| FRD-report-02 | Relatório de forecast por BU e mês | Forecast ponderado por BU e período | Must Have | RF-11 | 1 |
| FRD-report-03 | Ranking por responsável | Valor total e ganhos por responsável no período | Must Have | RF-11 | 1 |
| FRD-report-04 | Oportunidades por canal de origem | Distribuição de oportunidades por canal | Must Have | RF-11 | 1 |
| FRD-report-05 | Comissões por parceiro | Projetado (pipeline aberto) e consolidado (Ganhas) por período | Must Have | RF-11 | 1 |
| FRD-report-06 | Export CSV | Export de todos os relatórios em CSV | Must Have | RF-11 | 1 |
| FRD-workflow-01 | Editor de automações visuais | Criação de workflows por BU com React Flow; nós de gatilho/condição/ação/fim | Should Have | RF-12 | 2 |
| FRD-workflow-02 | Executar automação de forma assíncrona | Execução via barramento de eventos → worker; nunca síncrona | Should Have | RF-12 | 2 |
| FRD-ai-01 | Scoring de oportunidades em batch | Lead/opportunity scoring noturno; resultado disponível na oportunidade | Could Have | RF-13 | 3 |
| FRD-ai-02 | Resumo 360° de conta sob demanda | Briefing antes de reunião; baseado em dados do tenant isolados | Could Have | RF-13 | 3 |
| FRD-ai-03 | Próxima melhor ação por oportunidade | Sugestão com justificativa; tracing completo via Langfuse | Could Have | RF-13 | 3 |
| FRD-ai-04 | Copilot conversacional de vendas | REST síncrono; responde sobre dados do tenant; isolamento garantido | Could Have | RF-13 | 3 |
| FRD-migration-01 | Executar dry-run da planilha | Upload → validação → relatório de flags sem efeitos colaterais no banco | Must Have | RF-06/Migração | 1 |
| FRD-migration-02 | Triagem assistida de oportunidades | Interface para atribuir owners, estágios e percentuais de comissão pendentes | Must Have | RF-06/Migração | 1 |
| FRD-migration-03 | Executar import transacional | Import com rollback total em falha; relatório final auditado | Must Have | RF-06/Migração | 1 |

---

## 11. Detalhamento dos Requisitos Funcionais

---

### FRD-auth-01 — Autenticar usuário por e-mail/senha

#### Descrição

O sistema deve permitir que o usuário acesse o Azim CRM informando e-mail e senha. A sessão é resolvida pelo slug do tenant na URL (`app.azim.com.br/{slug}`).

#### Objetivo

Substituir o acesso irrestrito à planilha por autenticação individual com sessão isolada por tenant.

#### Atores Envolvidos

| Ator | Papel |
|---|---|
| ACT-01 a ACT-04 | Usuário que se autentica |
| ACT-05 | Autenticação no backoffice da plataforma |

#### Pré-condições

- Tenant provisionado com slug ativo no sistema.
- Usuário convidado e com conta ativada.
- Identidade associada ao tenant correto no Identity Platform.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Usuário acessa `app.azim.com.br/{slug}` |
| 2 | Sistema resolve o slug para `tenant_id` e exibe tela de login com branding do tenant |
| 3 | Usuário informa e-mail e senha |
| 4 | Sistema valida credenciais via Identity Platform (tenant de identidade correspondente) |
| 5 | Sistema cria sessão autenticada associada ao `tenant_id` e ao `user_id`; carrega papel e memberships por BU via cache |
| 6 | Sistema redireciona para o Kanban padrão da BU principal do usuário |

#### Fluxos Alternativos

| Código | Condição | Fluxo |
|---|---|---|
| FA-auth01-01 | Usuário com papel de Tenant Admin sem BU principal | Redireciona para painel de administração do tenant |
| FA-auth01-02 | Usuário com múltiplas BUs | Sistema seleciona a BU mais recentemente acessada ou solicita seleção se for o primeiro acesso |

#### Fluxos de Exceção

| Código | Condição | Comportamento Esperado | Mensagem |
|---|---|---|---|
| FE-auth01-01 | Credenciais inválidas | Bloqueia acesso; não indica qual campo está incorreto | MSG-001 |
| FE-auth01-02 | Slug de tenant inexistente ou inativo | Exibe página de erro 404 orientativa | MSG-002 |
| FE-auth01-03 | Conta de usuário desativada | Bloqueia acesso com mensagem orientativa | MSG-003 |
| FE-auth01-04 | Conta não ativada (convite pendente) | Orienta o usuário a verificar o e-mail de convite | MSG-004 |
| FE-auth01-05 | Falha de comunicação com Identity Platform | Exibe erro genérico com orientação; registra log operacional | MSG-005 |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-001 | Sessão associada ao `tenant_id` resolvido pelo slug; usuário não acessa dados de outro tenant |
| RN-012 | Toda requisição subsequente à sessão valida o `tenant_id` ativo |

#### Entradas

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| slug | string | Sim | Identificador do tenant na URL |
| email | string (e-mail) | Sim | E-mail cadastrado do usuário |
| password | string | Sim | Senha do usuário |

#### Saídas

| Campo | Tipo | Descrição |
|---|---|---|
| session_token | string | Token de sessão autenticada com `tenant_id` e `user_id` |
| user_profile | object | Papel, BUs e configurações do usuário naquele tenant |

#### Permissões

| Perfil/Papel | Permissão |
|---|---|
| Qualquer perfil cadastrado e ativo | Pode autenticar |
| Usuário desativado | Bloqueado |

#### Critérios de Aceite

- [ ] Login com credencial válida redireciona para o Kanban da BU em < 1 s (p95)
- [ ] Login com credencial inválida exibe MSG-001 sem revelar qual campo está incorreto
- [ ] Slug inexistente retorna página 404 sem expor dados internos
- [ ] Usuário desativado não consegue autenticar mesmo com credencial válida
- [ ] A sessão carrega corretamente os papéis e as BUs do usuário

#### Dependências

- Identity Platform GCP multi-tenant provisionado (Fase 0)
- Resolução de slug configurada no middleware .NET

---

### FRD-auth-02 — Autenticar usuário via conta Google

#### Descrição

O sistema deve permitir que o usuário acesse o Azim CRM usando sua conta Google, via Identity Platform multi-tenant, sem precisar criar senha separada.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Usuário clica em "Entrar com Google" na tela de login do tenant |
| 2 | Sistema redireciona para o fluxo OAuth do Identity Platform com o tenant de identidade do slug |
| 3 | Usuário autoriza o acesso na tela do Google |
| 4 | Identity Platform retorna token; sistema valida que o e-mail está associado a um usuário ativo daquele tenant |
| 5 | Sistema cria sessão e redireciona igual ao FRD-auth-01 passo 6 |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-auth02-01 | E-mail Google não associado ao tenant | Bloqueia; orienta a usar o e-mail correto ou solicitar convite | MSG-006 |
| FE-auth02-02 | Usuário Google com conta desativada no tenant | Bloqueia com mensagem orientativa | MSG-003 |

#### Critérios de Aceite

- [ ] Login via Google com e-mail associado ao tenant cria sessão válida
- [ ] Login via Google com e-mail não cadastrado no tenant exibe MSG-006
- [ ] O mesmo papel e as mesmas BUs são carregados independentemente do método de login (e-mail/senha ou Google)

---

### FRD-auth-03 — Convidar usuário por e-mail

#### Descrição

O Tenant Admin convida um novo usuário ao tenant informando e-mail, papel e BU(s). O sistema envia e-mail com link de ativação temporário.

#### Atores Envolvidos

| Ator | Papel |
|---|---|
| ACT-04 | Convida o usuário |
| ACT-08 | Entrega o e-mail de convite |

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Tenant Admin acessa a gestão de usuários |
| 2 | Informa e-mail, papel e BU(s) do convidado |
| 3 | Sistema valida que o e-mail não está já cadastrado no tenant |
| 4 | Sistema cria convite com `token` temporário e `expires_at` configurável |
| 5 | Sistema envia e-mail com link de ativação via IEmailSender |
| 6 | Convidado clica no link, define senha (ou vincula conta Google) e ativa a conta |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-auth03-01 | E-mail já cadastrado no tenant | Bloqueia criação; orienta o admin | MSG-007 |
| FE-auth03-02 | Link de convite expirado | Orienta o usuário a solicitar novo convite ao admin | MSG-008 |
| FE-auth03-03 | Falha de entrega do e-mail | Registra log; alerta o admin na interface | MSG-009 |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-030 | Link de convite tem expiração configurável (referência: 72 horas); após expirar, o link é inválido |

#### Critérios de Aceite

- [ ] Convite enviado chega ao e-mail do convidado em < 2 minutos após a ação do admin
- [ ] Link de convite expirado exibe MSG-008 e não ativa a conta
- [ ] Tentativa de convidar e-mail já existente exibe MSG-007
- [ ] Usuário ativado aparece na lista de usuários do tenant com papel e BU(s) corretos

---

### FRD-auth-04 — Recuperar senha por e-mail

#### Descrição

Usuário com credencial e-mail/senha pode solicitar redefinição de senha. Sistema envia link temporário de redefinição.

#### Critérios de Aceite

- [ ] Link de redefinição enviado em < 2 minutos após solicitação
- [ ] Link expira após uso ou após o prazo configurado
- [ ] Usuário que usa Google como método principal não visualiza opção de recuperação de senha

---

### FRD-auth-05 — Encerrar sessão com invalidação global

#### Descrição

Usuário pode fazer logout, invalidando a sessão em todos os dispositivos onde está autenticado.

#### Critérios de Aceite

- [ ] Após logout, tokens de sessão anteriores são invalidados
- [ ] Acesso via token invalidado retorna HTTP 401
- [ ] Links autenticados do digest utilizados antes do logout continuam válidos se ainda dentro do prazo de validade definido

---

### FRD-admin-01 — Configurar branding do tenant

#### Descrição

O Tenant Admin pode configurar a identidade visual do tenant dentro dos limites do white-label estrito (DEC-004): logo, favicon, cor primária e cor secundária.

#### Atores Envolvidos

| Ator | Papel |
|---|---|
| ACT-04 | Configura o branding |

#### Pré-condições

- Usuário autenticado com papel Tenant Admin.
- Tenant provisionado.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Tenant Admin acessa configurações de branding |
| 2 | Realiza upload de logo (PNG ou SVG, ≤ 1 MB) |
| 3 | Realiza upload de favicon |
| 4 | Define cor primária e cor secundária (seletor ou código hex) |
| 5 | Sistema valida o contraste WCAG AA para cada cor em relação ao branco e ao preto |
| 6 | Sistema salva as configurações; theming é propagado via CSS variables |
| 7 | Sistema exibe preview com branding atualizado |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-admin01-01 | Logo com tamanho > 1 MB | Bloqueia upload | MSG-010 |
| FE-admin01-02 | Logo com formato inválido (não PNG/SVG) | Bloqueia upload | MSG-011 |
| FE-admin01-03 | Cor com contraste WCAG AA insuficiente | Alerta com valor calculado e sugestão de ajuste | MSG-012 |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-019 | Somente logo, favicon, cor primária e cor secundária são configuráveis; sem CSS, fonte ou layout por tenant |
| RN-020 | Logo aceito apenas em formato PNG ou SVG com tamanho ≤ 1 MB; cores validadas por contraste WCAG 2.1 AA mínimo |

#### Critérios de Aceite

- [ ] Upload de logo PNG/SVG ≤ 1 MB é aceito e exibido no portal
- [ ] Upload de logo > 1 MB exibe MSG-010 sem salvar
- [ ] Upload de arquivo com formato inválido exibe MSG-011
- [ ] Cor com contraste WCAG AA insuficiente exibe MSG-012 com valor calculado; não bloqueia mas alerta
- [ ] Branding atualizado é refletido no portal em < 30 segundos após salvar
- [ ] Branding é aplicado no e-mail digest seguinte ao da configuração

---

### FRD-admin-02 — Definir slug e fuso horário do tenant

#### Descrição

O Tenant Admin define o slug do tenant (identificador único na URL) e o fuso horário IANA utilizado pelo digest diário. O slug é imutável após a primeira definição.

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-019 | Slug do tenant é imutável após definição; mudança exigiria operação de plataforma com impacto em URLs |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-admin02-01 | Slug já em uso por outro tenant | Bloqueia e orienta escolha de slug alternativo | MSG-013 |
| FE-admin02-02 | Slug com caracteres inválidos | Bloqueia; informa formato aceito (minúsculas, hífens) | MSG-014 |

#### Critérios de Aceite

- [ ] Slug configurado resolve corretamente o tenant em `app.azim.com.br/{slug}`
- [ ] Tentativa de alterar slug após definição é bloqueada via interface e via API
- [ ] Fuso horário IANA configurado é usado corretamente para seleção dos destinatários do digest

---

### FRD-org-01 — Gerenciar BUs

#### Descrição

O Tenant Admin pode criar, editar e inativar BUs. Cada BU possui nome, lista de estágios configuráveis, canais de origem e motivos de perda próprios.

#### Atores Envolvidos

| Ator | Papel |
|---|---|
| ACT-04 | Cria e configura BUs |

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Tenant Admin acessa gestão de BUs |
| 2 | Cria BU com nome único no tenant |
| 3 | Configura estágios (nome, probabilidade default, categoria: aberta/ganha/perdida) ou aceita o seed derivado do processo da Vellus |
| 4 | Configura canais de origem (lista editável; seed: Parceiro, Indicação, Prospecção ativa, Inbound, Evento, Base/Cliente existente, Outro) |
| 5 | Configura motivos de perda (lista editável; pelo menos um motivo obrigatório para habilitar a BU) |
| 6 | Sistema salva BU e a disponibiliza para associação de usuários e oportunidades |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-001 | Estágios do pipeline são configuráveis por BU com probabilidade default por estágio |
| RN-003 | Canal "Parceiro" exige vinculação de `partner_id` na oportunidade |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-org01-01 | Nome de BU duplicado no tenant | Bloqueia criação | MSG-015 |
| FE-org01-02 | Tentativa de inativar BU com oportunidades ativas | Bloqueia com contagem de oportunidades afetadas | MSG-016 |

#### Critérios de Aceite

- [ ] BU criada aparece na seleção de BU ao criar oportunidade
- [ ] Estágios seed da Vellus (Lead → Ganho/Perdido) são pré-carregados ao criar nova BU
- [ ] Motivos de perda da BU aparecem na lista ao mover oportunidade para "Perdido"
- [ ] BU com oportunidades ativas não pode ser inativada sem realocação

---

### FRD-org-02 — Gerenciar usuários e papéis

#### Descrição

O Tenant Admin convida usuários, define papel e BU(s) e pode desativar usuários. Um usuário pode ter papéis distintos em BUs distintas. A desativação não exclui registros históricos.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Tenant Admin acessa gestão de usuários |
| 2 | Convida usuário por e-mail (FRD-auth-03) |
| 3 | Define papel do usuário (Gestor de BU, Vendedor, Viewer) e BU(s) de atuação |
| 4 | Usuário aceita convite e ativa a conta |
| 5 | Admin pode alterar papel por BU ou desativar o usuário a qualquer momento |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-013 | Desativação de usuário não gera exclusão física; todas as FKs e histórico são preservados |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-org02-01 | Tentativa de remover último Tenant Admin | Bloqueia; tenant deve ter ao menos um admin ativo | MSG-017 |
| FE-org02-02 | Desativar usuário com atividades futuras atribuídas | Alerta; solicita reatribuição das atividades antes de confirmar | MSG-018 |

#### Critérios de Aceite

- [ ] Usuário desativado não consegue autenticar
- [ ] Oportunidades cujo owner foi desativado continuam visíveis e com owner exibido (nome histórico)
- [ ] Tenant deve ter ao menos um Tenant Admin ativo; remoção do último é bloqueada
- [ ] Um mesmo usuário pode ter papel Vendedor na BU Vellus e Gestor de BU na BU Axis

---

### FRD-account-01 — Gerenciar contas

#### Descrição

O sistema deve permitir criar, visualizar, editar e buscar contas. Ao criar, o sistema verifica se existe conta com nome similar (dedupe por nome normalizado) e exibe alerta antes de confirmar a criação.

#### Atores Envolvidos

| Ator | Papel |
|---|---|
| ACT-01, ACT-02, ACT-04 | Criam e gerenciam contas |

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Usuário acessa o módulo de Contas |
| 2 | Informa nome da conta para criação |
| 3 | Sistema normaliza o nome (sem acentos, minúsculas, sem espaços duplos) e verifica duplicatas |
| 4 | Se conta similar existir: exibe alerta com a conta encontrada e opções "Usar esta" ou "Criar nova mesmo assim" |
| 5 | Usuário confirma criação ou reutiliza conta existente |
| 6 | Sistema salva a conta vinculada ao `tenant_id` |

#### Fluxos Alternativos

| Código | Condição | Fluxo |
|---|---|---|
| FA-account01-01 | Criação de conta inline ao criar oportunidade | Fluxo simplificado: apenas nome; demais campos preenchidos depois na conta criada |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-014 | Dedupe por nome normalizado: sistema remove acentos, converte para minúsculas e ignora espaços extras antes de comparar |
| RN-012 | Contas são isoladas por tenant via RLS; conta da Vellus não é visível para outro tenant |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-account01-01 | Nome em branco | Bloqueia criação | MSG-019 |

#### Critérios de Aceite

- [ ] Ao digitar "Pag.ai", sistema encontra "PAG.AI " como similar e exibe alerta
- [ ] Usuário pode criar conta mesmo após alerta de similar (escolha explícita)
- [ ] Conta criada em inline (oportunidade) aparece na lista de contas para reaproveitamento
- [ ] Conta é compartilhada entre BUs do mesmo tenant sem duplicação de registro

---

### FRD-account-02 — Gerenciar contatos

#### Descrição

O sistema deve permitir criar, editar e vincular contatos a uma conta. Campos: nome, e-mail, celular e cargo.

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-025 | Dados pessoais de contatos (nome, e-mail, celular) não são incluídos em logs de sistema |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-account02-01 | E-mail de contato com formato inválido | Bloqueia salvamento | MSG-020 |

#### Critérios de Aceite

- [ ] Contato criado aparece na visão 360° da conta
- [ ] Campos de e-mail e celular do contato não aparecem em nenhum log de Cloud Logging
- [ ] Contato pode ser reutilizado em múltiplas oportunidades da mesma conta

---

### FRD-account-03 — Exibir visão 360° da conta

#### Descrição

A visão 360° de uma conta exibe, em ordem cronológica: todas as oportunidades das BUs visíveis ao usuário, contatos vinculados, atividades e histórico de alterações.

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-012 | Oportunidades exibidas são filtradas pelo escopo do usuário (RLS + RBAC); usuário vê apenas BUs às quais tem acesso |

#### Critérios de Aceite

- [ ] Conta Pag.ai exibe oportunidades das BUs Vellus e Axis se o usuário tem acesso a ambas
- [ ] Usuário com acesso apenas à BU Axis vê somente as oportunidades da Axis para a mesma conta
- [ ] Histórico exibe ações em ordem cronológica decrescente

---

### FRD-partner-01 — Gerenciar parceiros

#### Descrição

O Tenant Admin e o Gestor de BU podem criar, editar e visualizar parceiros. Cada parceiro tem nome, papel tipado e percentuais default de comissão por componente (`pct_setup`, `pct_recorrente`).

#### Atores Envolvidos

| Ator | Papel |
|---|---|
| ACT-04 | Cria e configura parceiros no tenant |
| ACT-02 | Edita parceiros de sua BU |

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Tenant Admin acessa gestão de parceiros |
| 2 | Informa nome do parceiro, papel tipado (ex.: Distribuidor, Indicador, Revenda) |
| 3 | Define percentuais default: `pct_setup` (% sobre valor_setup) e `pct_recorrente` (% sobre valor_mensal × meses_comissionados) |
| 4 | Opcionalmente define `valor_fixo` e `meses_comissionados` default |
| 5 | Sistema salva o parceiro; fica disponível para vinculação em oportunidades da BU |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-021 | Parceiro não tem credencial de acesso ao sistema no MVP; é entidade de dados gerida pelo tenant |
| RN-008 | Canal de origem "Parceiro" em uma oportunidade exige `partner_id` associado obrigatoriamente |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-partner01-01 | Nome de parceiro duplicado no tenant | Alerta; permite confirmar criação com nome idêntico se intencional | MSG-021 |

#### Critérios de Aceite

- [ ] Parceiro criado aparece na seleção de parceiro ao criar/editar oportunidade
- [ ] Percentuais default do parceiro são pré-preenchidos ao associar o parceiro a uma oportunidade
- [ ] Parceiro sem percentuais definidos é marcado com flag de triagem no dry-run de migração

---

### FRD-partner-02 — Exibir visão de comissão do parceiro

#### Descrição

O sistema exibe para o parceiro selecionado: oportunidades origadas, comissão projetada (oportunidades em aberto) e comissão consolidada (oportunidades com status Ganho) por período.

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-026 | Comissão projetada por oportunidade = `pct_setup × valor_setup + pct_recorrente × valor_mensal × meses_comissionados` (valores em centavos inteiros) |
| RN-007 | Comissão consolidada usa o snapshot imutável gerado ao mover a oportunidade para Ganho |

#### Critérios de Aceite

- [ ] Comissão projetada é calculada corretamente em centavos inteiros e exibida formatada em BRL
- [ ] Comissão consolidada não é alterada quando percentuais do parceiro são editados após o fechamento
- [ ] Filtro de período funciona corretamente nas duas visões (projetada e consolidada)

---

### FRD-pipeline-01 — Exibir Kanban de oportunidades por BU

#### Descrição

O sistema exibe o Kanban da BU selecionada com colunas representando os estágios configurados. Cada coluna exibe a soma de `valor_total` e `forecast_ponderado` das oportunidades naquele estágio. Cards podem ser movidos entre colunas via drag-and-drop.

#### Atores Envolvidos

| Ator | Papel |
|---|---|
| ACT-01 | Opera o Kanban diariamente |
| ACT-02 | Monitora e redistribui oportunidades |

#### Pré-condições

- Usuário autenticado com acesso à BU.
- BU com ao menos um estágio configurado.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Usuário acessa o Kanban da BU |
| 2 | Sistema carrega oportunidades filtradas pelo escopo do usuário (RBAC + RLS) |
| 3 | Agrupa oportunidades por estágio; calcula soma de `valor_total` e `forecast_ponderado` por coluna |
| 4 | Exibe indicadores visuais de oportunidades estagnadas (> 14 dias sem atividade) |
| 5 | Usuário navega e interage com os cards |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-005 | `valor_total = valor_setup + valor_mensal × duracao_meses` (valores em centavos inteiros) |
| RN-006 | `forecast_ponderado = valor_total × probabilidade_do_estagio` (valores em centavos inteiros) |
| RN-028 | Oportunidade estagnada: > 14 dias sem atividade registrada; sinalizada visualmente no card |

#### Critérios de Aceite

- [ ] Kanban carrega em ≤ 2 s (p95) com até 2.000 oportunidades por tenant
- [ ] Soma de valor_total e forecast_ponderado por coluna é calculada corretamente em centavos inteiros e exibida em BRL
- [ ] Cards estagnados exibem indicador visual distinto
- [ ] Apenas oportunidades do escopo do usuário são exibidas (Vendedor vê apenas as suas)

---

### FRD-pipeline-02 — Criar oportunidade

#### Descrição

O sistema deve permitir criar uma oportunidade com formulário completo ou via criação rápida (com conta nova inline). `owner_id` é obrigatório e é pré-preenchido com o usuário autenticado. O sistema gera automaticamente a numeração sequencial e imutável por tenant.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Usuário clica em "Nova oportunidade" no Kanban ou na lista |
| 2 | Sistema pré-preenche `owner_id` com o usuário autenticado |
| 3 | Usuário informa: nome da oportunidade, conta (busca existente ou cria inline), BU, estágio inicial, canal de origem |
| 4 | Se canal = "Parceiro": campo `partner_id` torna-se obrigatório |
| 5 | Usuário informa modelo de valor: `valor_setup`, `valor_mensal`, `duracao_meses` |
| 6 | Sistema calcula e exibe `valor_total` e `forecast_ponderado` em tempo real |
| 7 | Usuário salva a oportunidade |
| 8 | Sistema gera número sequencial (`AZ-NNNN`) imutável e salva a oportunidade com registro no `AuditLog` |

#### Fluxos Alternativos

| Código | Condição | Fluxo |
|---|---|---|
| FA-pipeline02-01 | Criação rápida | Apenas nome e conta obrigatórios; owner pré-preenchido; demais campos com defaults |
| FA-pipeline02-02 | Conta nova inline | Usuário informa nome da conta; sistema executa dedupe (FRD-account-01) e cria conta se confirmado |
| FA-pipeline02-03 | Usuário com permissão altera owner | Campo owner editável para Tenant Admin e Gestor de BU; Vendedor mantém owner como si mesmo |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-pipeline02-01 | `owner_id` em branco no envio | API retorna HTTP 422 com campo e mensagem identificados | MSG-022 |
| FE-pipeline02-02 | Canal "Parceiro" sem `partner_id` | Bloqueia salvamento | MSG-023 |
| FE-pipeline02-03 | `valor_mensal` preenchido sem `duracao_meses` | Alerta; `duracao_meses` torna-se obrigatório | MSG-024 |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-001 | Numeração sequencial automática e imutável por tenant: formato `AZ-NNNN`; nunca reutilizada |
| RN-002 | `owner_id` é obrigatório em toda oportunidade; sem exceções |
| RN-005 | `valor_total = valor_setup + valor_mensal × duracao_meses` |
| RN-006 | `forecast_ponderado = valor_total × probabilidade_do_estagio` |
| RN-008 | Canal "Parceiro" exige `partner_id` obrigatório |
| RN-015 | Todos os campos de valor armazenados como centavos inteiros |

#### Entradas

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| nome | string | Sim | Nome da oportunidade |
| account_id | uuid | Sim | Conta vinculada (existente ou criada inline) |
| owner_id | uuid | Sim | Responsável pela oportunidade |
| bu_id | uuid | Sim | BU à qual pertence a oportunidade |
| stage_id | uuid | Sim | Estágio inicial |
| canal_origem | enum | Sim | Canal de origem da oportunidade |
| partner_id | uuid | Condicional | Obrigatório quando canal_origem = "Parceiro" |
| valor_setup | integer (centavos) | Não | Valor de implantação/setup |
| valor_mensal | integer (centavos) | Não | Mensalidade do contrato |
| duracao_meses | integer | Condicional | Obrigatório quando valor_mensal > 0 |
| data_fechamento_esperada | date | Condicional | Obrigatória a partir do estágio "Proposta Enviada" (RN-003) |

#### Saídas

| Campo | Tipo | Descrição |
|---|---|---|
| opportunity_id | uuid | Identificador interno da oportunidade |
| numero | string | Número sequencial do tenant (ex.: `AZ-0095`) |
| valor_total | integer (centavos) | Calculado: valor_setup + valor_mensal × duracao_meses |
| forecast_ponderado | integer (centavos) | Calculado: valor_total × probabilidade do estágio |

#### Critérios de Aceite

- [ ] Oportunidade criada sem `owner_id` retorna HTTP 422 com campo `owner_id` identificado na resposta
- [ ] Número sequencial gerado é único no tenant e nunca se repete
- [ ] Valor total e forecast ponderado são calculados em centavos inteiros e exibidos corretamente em BRL
- [ ] Canal "Parceiro" sem `partner_id` bloqueia o salvamento com MSG-023
- [ ] Oportunidade aparece no Kanban da BU na coluna do estágio escolhido após criação

---

### FRD-pipeline-04 — Mover oportunidade de estágio

#### Descrição

O usuário pode mover uma oportunidade entre estágios via drag-and-drop no Kanban ou pelo seletor de estágio no formulário. O sistema valida regras de transição antes de confirmar.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Usuário arrasta o card de oportunidade para outro estágio no Kanban (ou altera o estágio no formulário) |
| 2 | Sistema identifica o estágio de destino e valida as regras de transição aplicáveis |
| 3 | Se destino = "Proposta Enviada" (ou estágio posterior): valida se `data_fechamento_esperada` está preenchida |
| 4 | Se destino = "Perdido": solicita motivo de perda (lista configurável da BU) |
| 5 | Se destino = "Ganho": executa FRD-pipeline-06 |
| 6 | Após validação aprovada: atualiza o estágio, registra a transição na linha do tempo e no `AuditLog` |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-pipeline04-01 | Mover para "Proposta Enviada" (ou posterior) sem `data_fechamento_esperada` | Bloqueia a transição; exibe formulário para preenchimento | MSG-025 |
| FE-pipeline04-02 | Mover para "Perdido" sem `motivo_perda` | Bloqueia; exibe lista de motivos para seleção | MSG-026 |
| FE-pipeline04-03 | Usuário sem permissão de edição tentando mover | Bloqueia; exibe mensagem de permissão | MSG-027 |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-003 | `data_fechamento_esperada` é obrigatória a partir do estágio "Proposta Enviada" |
| RN-004 | `motivo_perda` é obrigatório ao mover para o estágio "Perdido" |

#### Critérios de Aceite

- [ ] Mover para estágio posterior a "Diagnóstico" sem `data_fechamento_esperada` bloqueia a operação com MSG-025
- [ ] Mover para "Perdido" sem selecionar motivo bloqueia com MSG-026
- [ ] Transição registrada na linha do tempo com timestamp e usuário que executou
- [ ] Forecast ponderado da coluna é atualizado após a transição

---

### FRD-pipeline-05 — Registrar comissão de parceiro

#### Descrição

Quando uma oportunidade tem parceiro vinculado, o usuário pode registrar ou editar os percentuais de comissão por componente (`pct_setup`, `pct_recorrente`), valor fixo opcional e meses comissionados. O sistema exibe o valor calculado de comissão em tempo real.

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-026 | Comissão calculada = `pct_setup × valor_setup + pct_recorrente × valor_mensal × meses_comissionados` |
| RN-015 | Todos os valores em centavos inteiros |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-pipeline05-01 | Oportunidade movida para "Ganho" com parceiro associado e percentuais em branco | **Ponto a Validar (VAL-07):** comportamento não definido no PRD — alerta ou bloqueio | MSG-028 |

#### Critérios de Aceite

- [ ] Percentuais default do parceiro são pré-preenchidos ao vincular o parceiro
- [ ] Cálculo de comissão em centavos inteiros é exibido em tempo real ao alterar percentuais ou valores da oportunidade
- [ ] Campo de comissão é omitido do formulário quando a oportunidade não tem parceiro vinculado

---

### FRD-pipeline-06 — Fechar oportunidade como Ganho

#### Descrição

Ao mover uma oportunidade para o estágio "Ganho", o sistema gera o snapshot imutável da comissão de parceiro (`OpportunityPartnerCommission`) com a data e hora do evento. O snapshot não pode ser editado por nenhum usuário após a geração.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Usuário move oportunidade para "Ganho" |
| 2 | Sistema exibe sumário de comissão calculada para confirmação (quando `partner_id` está associado) |
| 3 | Usuário confirma |
| 4 | Sistema gera snapshot imutável: copia `pct_setup`, `pct_recorrente`, `valor_fixo`, `meses_comissionados`, `valor_calculado` e `fechado_em` para o registro de snapshot |
| 5 | Sistema atualiza o estágio da oportunidade para "Ganho" e registra no `AuditLog` |

#### Fluxos Alternativos

| Código | Condição | Fluxo |
|---|---|---|
| FA-pipeline06-01 | Oportunidade sem parceiro | Passo 2 é omitido; não há snapshot de comissão |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-007 | Snapshot imutável de comissão gerado ao mover para "Ganho"; nunca alterado por edições futuras no parceiro |
| RN-022 | Snapshot preservado mesmo que percentuais do parceiro sejam alterados posteriormente |

#### Critérios de Aceite

- [ ] Snapshot gerado contém exatamente os valores dos percentuais no momento do fechamento
- [ ] Alterar percentuais do parceiro após o fechamento não altera o snapshot da oportunidade
- [ ] Relatório de comissões consolidadas usa o snapshot, não os percentuais atuais do parceiro
- [ ] Snapshot registrado no `AuditLog` com timestamp e `user_id`

---

### FRD-pipeline-07 — Fechar oportunidade como Perdido

#### Descrição

Ao mover uma oportunidade para "Perdido", o sistema exige que o usuário selecione um motivo de perda da lista configurável da BU.

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-004 | `motivo_perda` é obrigatório ao mover para "Perdido"; bloqueante sem possibilidade de contornar |

#### Critérios de Aceite

- [ ] Tentativa de mover para "Perdido" sem motivo é bloqueada com MSG-026
- [ ] Motivo selecionado é registrado no `AuditLog` e exibido na linha do tempo
- [ ] Lista de motivos exibida é a configurada para a BU da oportunidade

---

### FRD-pipeline-08 — Reabrir oportunidade Ganha ou Perdida

#### Descrição

Tenant Admin e Gestor de BU podem reabrir oportunidades nos estágios "Ganho" ou "Perdido". O snapshot original de comissão é preservado. Um novo snapshot é gerado se a oportunidade for re-fechada.

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-016 | Reabertura permitida apenas para Tenant Admin e Gestor de BU; Vendedor não pode reabrir |

#### Critérios de Aceite

- [ ] Vendedor não vê a opção de reabrir; Gestor de BU e Tenant Admin a veem
- [ ] Snapshot original de comissão é preservado após reabertura
- [ ] Ao re-fechar como "Ganho", novo snapshot é gerado com os valores atuais

---

### FRD-pipeline-09 — Exibir visão lista de oportunidades

#### Descrição

O sistema oferece visão em lista das oportunidades da BU, com filtros combinados e a possibilidade de salvar filtros personalizados.

#### Filtros disponíveis

| Filtro | Tipo | Descrição |
|---|---|---|
| owner | uuid | Responsável pela oportunidade |
| canal | enum | Canal de origem |
| partner_id | uuid | Parceiro vinculado |
| stage_id | uuid | Estágio atual |
| data_fechamento_ate | date | Data de fechamento ≤ data informada |
| estagnadas | boolean | Oportunidades sem atividade há > 14 dias |

#### Critérios de Aceite

- [ ] Filtro por "estagnadas" retorna apenas oportunidades sem atividade há > 14 dias
- [ ] Filtros combinados funcionam como AND entre si
- [ ] Filtros salvos persistem entre sessões do mesmo usuário no tenant

---

### FRD-activity-01 — Registrar atividade

#### Descrição

O sistema deve permitir criar, editar, visualizar e excluir atividades comerciais vinculadas a oportunidades, contas ou contatos.

#### Atores Envolvidos

| Ator | Papel |
|---|---|
| ACT-01, ACT-02 | Criam e gerenciam atividades |

#### Entradas

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| tipo | enum | Sim | Call, reunião, e-mail, tarefa |
| data | date | Sim | Data da atividade |
| hora | time | Não | Hora da atividade |
| responsavel_id | uuid | Sim | Usuário responsável |
| descricao | string | Não | Detalhes da atividade |
| opportunity_id | uuid | Condicional | Oportunidade vinculada (ou conta ou contato) |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-028 | Atividade registrada interrompe o contador de estagnação da oportunidade vinculada |

#### Critérios de Aceite

- [ ] Atividade criada aparece na visão "Meu dia" no dia configurado
- [ ] Atividade vinculada a oportunidade aparece na linha do tempo da oportunidade
- [ ] Criação de atividade remove o indicador de estagnação da oportunidade naquele momento
- [ ] Atividade registrada aparece no digest do responsável se estiver vencida ou for do dia

---

### FRD-activity-02 — Exibir to-do diário

#### Descrição

O sistema exibe ao usuário uma visão consolidada das suas atividades organizadas em: vencidas (data < hoje e não concluídas), de hoje (data = hoje), próximas (data > hoje).

#### Critérios de Aceite

- [ ] Atividades vencidas são exibidas com destaque visual
- [ ] Oportunidades estagnadas (> 14 dias) aparecem sinalizadas no Kanban e na visão "Meu dia"
- [ ] Contador de estagnação é calculado em dias corridos desde a última atividade registrada

---

### FRD-activity-03 — Concluir atividade em 1 clique

#### Descrição

O usuário pode marcar uma atividade como concluída diretamente da visão lista ou via link autenticado recebido no digest, sem precisar abrir o portal ou o formulário completo.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Usuário clica em "Concluir" na lista ou no link do digest |
| 2 | Sistema marca a atividade como `concluida = true` e registra `concluida_em` com o timestamp atual |
| 3 | Sistema remove a atividade da visão "Meu dia / vencidas / hoje" |
| 4 | Sistema oferece sugestão de criação de próxima atividade (FRD-activity-04) |
| 5 | Evento registrado no `AuditLog` |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-activity03-01 | Link autenticado do digest clicado mais de uma vez | Sistema confirma conclusão sem duplicar o registro (idempotência) | MSG-029 |
| FE-activity03-02 | Link autenticado expirado | Redireciona para login do portal com mensagem orientativa | MSG-030 |

#### Critérios de Aceite

- [ ] Clicar em "Concluir" marca a atividade como concluída sem abrir formulário
- [ ] Link do digest concluído duas vezes não gera duplicação de registro
- [ ] Sugestão de próxima atividade é exibida após a conclusão

---

### FRD-activity-04 — Sugerir próxima atividade

#### Descrição

Ao concluir uma atividade, o sistema sugere ao usuário criar uma próxima atividade para a mesma oportunidade ou conta.

#### Critérios de Aceite

- [ ] Sugestão é exibida automaticamente após conclusão de atividade vinculada a oportunidade aberta
- [ ] Usuário pode ignorar a sugestão sem impacto no sistema
- [ ] Ao aceitar a sugestão, formulário de nova atividade é pré-preenchido com a oportunidade vinculada

---

### FRD-goal-01 — Cadastrar meta mensal

#### Descrição

O Tenant Admin e o Gestor de BU podem cadastrar metas mensais de valor (`valor_meta`) por BU e/ou por responsável (usuário com papel de Vendedor ou Gestor).

#### Atores Envolvidos

| Ator | Papel |
|---|---|
| ACT-04 | Cadastra metas para qualquer BU do tenant |
| ACT-02 | Cadastra metas para sua BU |

#### Entradas

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| escopo | enum | Sim | BU ou Responsável |
| bu_id | uuid | Sim | BU à qual a meta se aplica |
| responsavel_id | uuid | Condicional | Obrigatório quando escopo = Responsável |
| mes | integer (1-12) | Sim | Mês de referência |
| ano | integer | Sim | Ano de referência |
| valor_meta | integer (centavos) | Sim | Valor da meta em centavos inteiros |

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-027 | Metas são granulares por mês; agregações trimestral e anual são derivadas automaticamente (soma dos meses) |

#### Critérios de Aceite

- [ ] Meta mensal cadastrada aparece no painel de forecast no mês correspondente
- [ ] Agregação trimestral = soma dos 3 meses do trimestre; anual = soma dos 12 meses
- [ ] Gestor vê apenas metas da sua BU; Tenant Admin vê todas

---

### FRD-goal-02 — Exibir painel de metas e forecast

#### Descrição

O sistema exibe o painel de comparação: `realizado` (oportunidades Ganhas no período) vs `meta` (quando cadastrada) vs `pipeline disponível` (forecast ponderado das oportunidades em aberto).

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-017 | Sem meta cadastrada, painel exibe apenas realizado vs pipeline sem erro conspícuo (graceful degradation) |
| RN-018 | Bloco de metas no digest de segunda é omitido quando não há meta cadastrada |

#### Critérios de Aceite

- [ ] Com meta cadastrada: painel exibe % de atingimento, gap e pipeline disponível
- [ ] Sem meta cadastrada: painel exibe realizado e pipeline sem mensagem de erro; área de meta exibe "Meta não cadastrada"
- [ ] Painel filtrável por BU e por responsável

---

### FRD-digest-01 — Selecionar destinatários do digest

#### Descrição

O Cloud Scheduler dispara um job horário em UTC. O serviço de digest compara a hora local do tenant (fuso IANA configurado) com o `horario_digest` (default 07:00) e filtra tenants cujo horário local atual corresponde. Em seguida, filtra usuários com pendências (atividades vencidas, atividades de hoje, oportunidades estagnadas ou `data_fechamento_esperada` vencida). Usuários sem pendências e sem papel de gestão não recebem o digest.

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-009 | Seleção de destinatários usa fuso IANA do tenant para converter UTC para hora local; a comparação é feita hora a hora para evitar um job por tenant |
| RN-010 | Idempotência garantida pelo `EmailDigestLog` (usuário + data no fuso do tenant); segundo disparo no mesmo dia para o mesmo usuário é ignorado |
| RN-011 | Usuário sem pendências e sem papel de Gestor de BU ou Executivo não recebe o digest |

#### Critérios de Aceite

- [ ] Usuário com fuso `America/Sao_Paulo` (BRT = UTC-3) recebe o digest quando o job UTC das 10:00 UTC executa (= 07:00 BRT)
- [ ] DST (horário de verão) é tratado corretamente pela conversão IANA, sem envio antecipado ou atrasado
- [ ] Usuário sem atividades vencidas, de hoje, nem oportunidades estagnadas, sem papel de gestão: não recebe digest
- [ ] Segundo disparo do scheduler no mesmo dia não gera segundo e-mail para o mesmo usuário

---

### FRD-digest-02 — Enviar digest de terça a sexta

#### Descrição

Para dias úteis de terça a sexta, o digest por usuário inclui: saudação no tom da marca, lista de atividades vencidas, lista de atividades de hoje, oportunidades estagnadas (> 14 dias), datas de fechamento vencidas. Cada item de atividade tem link autenticado de 1 clique.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Serviço de digest, após seleção (FRD-digest-01), monta o conteúdo personalizado por usuário |
| 2 | Renderiza template de e-mail com branding do tenant (logo, cores via CSS variables) |
| 3 | Gera links autenticados temporários para cada ação (concluir/reagendar atividade) |
| 4 | Envia via IEmailSender |
| 5 | Registra envio no `EmailDigestLog` com status `sent` |
| 6 | Webhook do provider atualiza status para `delivered` ou `failed`; alimenta KPI-03 |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-digest02-01 | Falha de entrega (bounce) | Evento registrado no `EmailDigestLog`; alerta operacional se taxa de falha > 2% | MSG-031 |
| FE-digest02-02 | Provider retorna erro temporário | Retentativa conforme política do provider; log registrado; idempotência previne duplicação | — |

#### Critérios de Aceite

- [ ] E-mail entregue entre 07:00 e 07:05 no fuso do tenant em dias úteis
- [ ] E-mail renderiza logo e cores do tenant corretamente
- [ ] Cada atividade no e-mail tem link de 1 clique para concluir ou reagendar
- [ ] `EmailDigestLog` registra `user_id`, `date` (no fuso do tenant), `status` e `message_id` do provider
- [ ] Taxa de entrega monitorada via KPI-03 (meta: >= 98%)

---

### FRD-digest-03 — Enviar azimute da semana (segunda-feira)

#### Descrição

Na segunda-feira, o digest para usuários com papel de Gestor de BU (ACT-02) e Executivo do Tenant (ACT-03) inclui o "azimute da semana": resumo do pipeline por estágio e BU, variação vs semana anterior, ganhos do mês/trimestre, top oportunidades, fechamentos esperados na semana, segmentação por canal. O bloco de metas (realizado vs meta, gap, pipeline disponível) é incluído somente quando há meta cadastrada.

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-018 | Bloco de metas omitido na segunda-feira quando não há meta cadastrada para o período |
| RN-029 | Azimute da semana é enviado na segunda-feira; o digest de atividades normais também é enviado se o usuário tem pendências |

#### Critérios de Aceite

- [ ] Segunda-feira: Gestor de BU recebe o azimute da semana com seu escopo de BU
- [ ] Segunda-feira: Executivo do Tenant recebe o azimute consolidado do tenant
- [ ] Bloco de metas ausente quando não há meta cadastrada; sem erro ou placeholder vazio
- [ ] Variação vs semana anterior calculada corretamente em centavos inteiros

---

### FRD-digest-04 — Garantir idempotência do digest

#### Descrição

O sistema usa a tabela `EmailDigestLog` para garantir que um mesmo usuário não receba mais de um digest por dia, mesmo em caso de retentativas do scheduler ou falhas parciais.

#### Entradas do EmailDigestLog

| Campo | Tipo | Descrição |
|---|---|---|
| user_id | uuid | Usuário destinatário |
| date | date | Data no fuso do tenant |
| status | enum | scheduled, sent, delivered, failed, opened |
| message_id | string | ID retornado pelo provider de e-mail |

#### Critérios de Aceite

- [ ] Se `EmailDigestLog` já contém `user_id + date` com status `sent` ou `delivered`, o serviço não reenvia
- [ ] Retentativa por falha temporária do provider não gera segundo e-mail ao usuário
- [ ] Evento `opened` do provider atualiza o status no `EmailDigestLog`; alimenta KPI-04

---

### FRD-notif-01 — Exibir notificações in-app *(Fase 2)*

#### Descrição

O sistema exibe notificações dentro da plataforma para eventos de domínio relevantes: atividade atribuída, oportunidade avançada de estágio, automação executada. A central de notificações mostra contagem de não lidas e permite leitura individual e em massa.

#### Critérios de Aceite

- [ ] Notificação criada aparece na central em < 5 segundos após o evento
- [ ] Leitura individual marca a notificação como lida
- [ ] "Marcar todas como lidas" zera o contador de não lidas

---

### FRD-report-01 a FRD-report-06 — Relatórios

#### Descrição geral

O módulo de relatórios entrega visões operacionais para gestores e executivos. Todos os relatórios são filtráveis por período (mês, trimestre, intervalo personalizado) e por BU (para Tenant Admin e Gestor com múltiplas BUs). Todos exportáveis em CSV.

| Requisito | Relatório | Colunas principais |
|---|---|---|
| FRD-report-01 | Funil por estágio | Estágio, qtd. de oportunidades, valor total (centavos), forecast ponderado (centavos) |
| FRD-report-02 | Forecast por BU e mês | BU, mês/ano, forecast ponderado (centavos), realizado (centavos) |
| FRD-report-03 | Ranking por responsável | Responsável, qtd. ganhas, valor ganho (centavos), valor em pipeline (centavos) |
| FRD-report-04 | Oportunidades por canal de origem | Canal, qtd., valor total (centavos), % do total |
| FRD-report-05 | Comissões por parceiro | Parceiro, comissão projetada (centavos), comissão consolidada (centavos), qtd. de oportunidades |
| FRD-report-06 | Export CSV | Todas as colunas do relatório selecionado em formato UTF-8 BOM |

#### Critérios de Aceite comuns a todos os relatórios

- [ ] Relatório carrega em ≤ 3 s (p95) para períodos de até 12 meses
- [ ] Valores exibidos em BRL formatados (mas armazenados e calculados em centavos inteiros)
- [ ] Export CSV gerado em ≤ 10 s com encoding UTF-8 BOM
- [ ] Usuário com papel Vendedor vê apenas seus próprios dados; Gestor vê sua BU; Tenant Admin vê tudo

---

### FRD-workflow-01 e FRD-workflow-02 — Automações Visuais *(Fase 2)*

#### Descrição

Editor de workflows por BU com React Flow. Nós disponíveis: Gatilho (eventos de domínio), Condição, Ações (criar atividade, atualizar campo, notificar in-app, enviar e-mail, webhook), Fim. Execução sempre assíncrona via barramento de eventos → worker; nunca síncrona.

#### Critérios de Aceite

- [ ] Workflow publicado por um tenant não impacta dados de outro tenant
- [ ] Execução assíncrona: nenhuma automação é executada de forma síncrona no request do usuário
- [ ] Log de execução por nó disponível para auditoria

---

### FRD-ai-01 a FRD-ai-04 — Agentes de IA *(Fase 3)*

#### Descrição

Capacidades de inteligência artificial com isolamento por tenant: scoring em batch, resumo 360° sob demanda, próxima melhor ação e copilot conversacional. Tracing completo via Langfuse. Rate-limit por tenant via Redis.

#### Critérios de Aceite comuns

- [ ] Dados de um tenant jamais são usados como contexto para outro tenant
- [ ] Custo de IA por tenant visível no painel de operações
- [ ] Todo prompt e resposta rastreado no Langfuse com `tenant_id` como atributo

---

### FRD-migration-01 — Executar dry-run da planilha

#### Descrição

O Tenant Admin faz upload do arquivo `Pipeline Vellus.xlsx`. O sistema executa validação completa (dry-run) sem efetuar nenhuma alteração no banco de dados e retorna relatório detalhado com flags por registro.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Admin faz upload do arquivo `.xlsx` |
| 2 | Sistema valida formato e estrutura do arquivo |
| 3 | Executa análise: conta oportunidades por BU, identifica registros sem owner, sem data de fechamento, com parceiro sem percentual, com typos em campos livres, dedupes de contas |
| 4 | Retorna relatório de dry-run com contagens, lista de flags e alertas |
| 5 | Nenhuma alteração é feita no banco de dados |

#### Saídas do dry-run

| Campo | Descrição |
|---|---|
| total_oportunidades | Total de linhas detectadas |
| por_bu | Distribuição por BU (Vellus, Axis, Vellus Tech) |
| sem_owner | Quantidade de oportunidades sem responsável |
| sem_data_fechamento | Quantidade sem `data_fechamento_esperada` |
| parceiro_sem_percentual | Oportunidades com parceiro citado mas sem percentual |
| dedupes_contas | Pares de contas com nomes similares detectados |
| erros_digitacao | Campos com typos identificados |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-migration01-01 | Arquivo com formato inesperado (não .xlsx) | Import abortado sem efeitos colaterais | MSG-032 |
| FE-migration01-02 | Arquivo com estrutura de colunas não reconhecida | Import abortado com indicação das colunas esperadas | MSG-033 |

#### Critérios de Aceite

- [ ] Dry-run com 108 oportunidades concluído em ≤ 5 minutos
- [ ] Relatório lista exatamente as 64 oportunidades sem owner da planilha Vellus
- [ ] Nenhuma linha é escrita no banco durante o dry-run
- [ ] Arquivo com formato inválido retorna MSG-032 sem efeitos colaterais

---

### FRD-migration-02 — Triagem assistida de oportunidades

#### Descrição

Após o dry-run, o sistema exibe a interface de triagem assistida para resolução de todos os flags antes do import definitivo.

#### Fluxo Principal

| Passo | Ação |
|---|---|
| 1 | Admin abre a triagem assistida a partir do relatório de dry-run |
| 2 | Sistema lista oportunidades sem owner para atribuição em massa ou individual |
| 3 | Admin atribui owners; sistema valida que todos os registros receberam owner |
| 4 | Sistema lista parceiros sem percentual de comissão; admin define ou marca como "a definir" |
| 5 | Sistema lista dedupes de contas; admin escolhe merge ou manutenção de ambas |
| 6 | Após resolução de todos os flags obrigatórios, o botão "Iniciar import" fica habilitado |

#### Critérios de Aceite

- [ ] Botão "Iniciar import" fica desabilitado enquanto houver oportunidade sem owner
- [ ] Atribuição em massa permite associar um owner a todas as oportunidades de uma BU de uma vez
- [ ] Admin pode salvar o progresso da triagem e retomar em outra sessão

---

### FRD-migration-03 — Executar import transacional

#### Descrição

Após a triagem, o sistema executa o import como transação atômica. Em caso de qualquer falha, executa rollback total. Ao término bem-sucedido, gera relatório final auditado.

#### Regras de Negócio Aplicáveis

| Código | Regra |
|---|---|
| RN-001 | Numeração sequencial das oportunidades importadas é gerada a partir do próximo número livre do tenant (`AZ-0095` no caso da Vellus) |

#### Fluxos de Exceção

| Código | Condição | Comportamento | Mensagem |
|---|---|---|---|
| FE-migration03-01 | Qualquer falha durante o import | Rollback total; estado anterior preservado integralmente | MSG-034 |

#### Critérios de Aceite

- [ ] Import de 108 oportunidades concluído em ≤ 5 minutos
- [ ] Falha em qualquer passo executa rollback total sem dados parcialmente importados
- [ ] Relatório final auditado disponível para download após conclusão bem-sucedida
- [ ] Numeração sequencial inicia corretamente a partir do `AZ-0095` (próximo após 94 registros da planilha)

---

## 12. Casos de Uso

---

### UC-01 — Login e acesso ao tenant

| Campo | Descrição |
|---|---|
| Objetivo | Usuário autenticado acessa o Azim no tenant correto resolvido pelo slug da URL |
| Ator Principal | ACT-01 a ACT-05 |
| Atores Secundários | Identity Platform (GCP), ACT-07 |
| Pré-condições | Tenant provisionado; usuário convidado e com conta ativada |
| Pós-condições | Sessão criada com `tenant_id`, papel e BUs do usuário; redirecionamento para Kanban ou painel admin |
| Requisitos Relacionados | FRD-auth-01, FRD-auth-02 |

#### Fluxo Principal

| Passo | Descrição |
|---|---|
| 1 | Usuário acessa `app.azim.com.br/{slug}` |
| 2 | Sistema exibe tela de login com branding do tenant (logo + cores) |
| 3 | Usuário autentica via e-mail/senha ou Google |
| 4 | Sessão criada; redirecionamento para destino principal |

#### Fluxos de Exceção

| Código | Erro | Tratamento |
|---|---|---|
| FE-UC01-01 | Credencial inválida | MSG-001; sem indicar qual campo |
| FE-UC01-02 | Slug inexistente | Página 404 orientativa |
| FE-UC01-03 | Conta desativada | MSG-003 |

---

### UC-02 — Criar e avançar oportunidade no funil

| Campo | Descrição |
|---|---|
| Objetivo | Vendedor cria oportunidade e a avança ao longo dos estágios do pipeline, respeitando as regras de transição |
| Ator Principal | ACT-01 |
| Atores Secundários | ACT-02 (supervisão) |
| Pré-condições | Usuário autenticado com papel Vendedor ou superior; BU com estágios configurados |
| Pós-condições | Oportunidade no novo estágio; transição registrada na linha do tempo; AuditLog atualizado |
| Requisitos Relacionados | FRD-pipeline-02, FRD-pipeline-04, FRD-pipeline-06, FRD-pipeline-07 |

#### Fluxo Principal

| Passo | Descrição |
|---|---|
| 1 | Vendedor cria oportunidade com owner (si mesmo), conta, BU, estágio inicial, canal e valores |
| 2 | Sistema gera número sequencial (`AZ-NNNN`) e salva oportunidade |
| 3 | Vendedor arrasta o card para o próximo estágio no Kanban |
| 4 | Sistema valida regras de transição (data de fechamento, motivo de perda) |
| 5 | Transição aprovada: estágio atualizado, linha do tempo registrada |

#### Fluxos Alternativos

| Código | Descrição |
|---|---|
| FA-UC02-01 | Avanço para "Proposta Enviada": sistema exige `data_fechamento_esperada` antes de confirmar |
| FA-UC02-02 | Mover para "Ganho": sistema exibe sumário de comissão e gera snapshot imutável após confirmação |
| FA-UC02-03 | Mover para "Perdido": sistema exige seleção de motivo de perda da lista da BU |

#### Fluxos de Exceção

| Código | Erro | Tratamento |
|---|---|---|
| FE-UC02-01 | Sem `data_fechamento_esperada` ao avançar além de Diagnóstico | MSG-025; bloqueia transição |
| FE-UC02-02 | Sem `motivo_perda` ao mover para Perdido | MSG-026; bloqueia transição |

---

### UC-03 — Registrar comissão e fechar negócio

| Campo | Descrição |
|---|---|
| Objetivo | Vendedor ou gestor registra percentuais de comissão do parceiro e fecha a oportunidade como Ganho gerando snapshot imutável |
| Ator Principal | ACT-01, ACT-02 |
| Atores Secundários | — |
| Pré-condições | Oportunidade com `partner_id` associado; percentuais de comissão a registrar |
| Pós-condições | Snapshot imutável de comissão criado; oportunidade em estágio Ganho; relatório de comissões atualizado |
| Requisitos Relacionados | FRD-pipeline-05, FRD-pipeline-06 |

#### Fluxo Principal

| Passo | Descrição |
|---|---|
| 1 | Usuário abre oportunidade com parceiro vinculado |
| 2 | Registra `pct_setup`, `pct_recorrente` (e opcionalmente `valor_fixo`, `meses_comissionados`) |
| 3 | Sistema calcula e exibe comissão projetada em BRL |
| 4 | Usuário move oportunidade para "Ganho" |
| 5 | Sistema exibe sumário de comissão para confirmação |
| 6 | Usuário confirma; sistema gera snapshot imutável e atualiza o estágio |

#### Fluxos de Exceção

| Código | Erro | Tratamento |
|---|---|---|
| FE-UC03-01 | Parceiro sem percentuais ao mover para Ganho | VAL-07: comportamento a definir (alerta ou bloqueio — MSG-028) |

---

### UC-04 — Vendedor age pelo digest sem abrir o sistema

| Campo | Descrição |
|---|---|
| Objetivo | Vendedor recebe digest às 07:00 e conclui atividade em 1 clique sem acessar o portal |
| Ator Principal | ACT-01, ACT-07, ACT-08 |
| Atores Secundários | — |
| Pré-condições | Usuário com ao menos 1 atividade vencida ou agendada para hoje; fuso IANA configurado no tenant |
| Pós-condições | Atividade marcada como concluída; sugestão de próxima atividade enviada; EmailDigestLog atualizado |
| Requisitos Relacionados | FRD-digest-01, FRD-digest-02, FRD-digest-04, FRD-activity-03 |

#### Fluxo Principal

| Passo | Descrição |
|---|---|
| 1 | Cloud Scheduler dispara job às 07:00 no fuso do tenant |
| 2 | Serviço seleciona usuário com pendências; verifica idempotência (EmailDigestLog) |
| 3 | Renderiza e-mail com branding do tenant; gera links autenticados |
| 4 | Envia via IEmailSender; registra no EmailDigestLog como `sent` |
| 5 | Vendedor recebe e-mail; clica em "Concluir" para uma atividade |
| 6 | Sistema marca atividade como concluída; confirma no e-mail de retorno ou atualiza silenciosamente |

#### Fluxos de Exceção

| Código | Erro | Tratamento |
|---|---|---|
| FE-UC04-01 | Link clicado após expiração | MSG-030; redireciona para login |
| FE-UC04-02 | Link clicado 2x | Idempotência: confirma sem duplicar |

---

### UC-05 — Gestor acompanha pipeline e metas da BU

| Campo | Descrição |
|---|---|
| Objetivo | Gestor de BU consulta painel de pipeline vs meta, identifica estagnações e redistribui oportunidades |
| Ator Principal | ACT-02 |
| Atores Secundários | ACT-03 (visão executiva) |
| Pré-condições | Usuário autenticado com papel Gestor de BU ou Executivo do Tenant |
| Pós-condições | Gestor informado sobre status da BU; oportunidades redistribuídas se necessário |
| Requisitos Relacionados | FRD-goal-02, FRD-pipeline-01, FRD-pipeline-09, FRD-report-02 |

#### Fluxo Principal

| Passo | Descrição |
|---|---|
| 1 | Gestor acessa o painel da BU |
| 2 | Visualiza pipeline por estágio com valor total e forecast ponderado |
| 3 | Seleciona período mensal; sistema compara realizado vs meta vs pipeline disponível |
| 4 | Identifica oportunidades estagnadas no filtro da lista |
| 5 | Reatribui owner de oportunidades estagnadas para outro vendedor |

#### Fluxos Alternativos

| Código | Descrição |
|---|---|
| FA-UC05-01 | Sem meta cadastrada: painel exibe apenas realizado e pipeline; sem percentual de atingimento |

---

### UC-06 — Migração da planilha Pipeline Vellus.xlsx

| Campo | Descrição |
|---|---|
| Objetivo | Administrador do tenant importa todas as oportunidades da planilha para o Azim com 100% de owners definidos |
| Ator Principal | ACT-04 |
| Atores Secundários | ACT-02 (triagem de owners) |
| Pré-condições | Planilha `Pipeline Vellus.xlsx` disponível; BUs, usuários e estágios configurados no Azim |
| Pós-condições | 108 oportunidades importadas com owner; planilha congelada; relatório auditado disponível |
| Requisitos Relacionados | FRD-migration-01, FRD-migration-02, FRD-migration-03 |

#### Fluxo Principal

| Passo | Descrição |
|---|---|
| 1 | Admin faz upload da planilha |
| 2 | Sistema executa dry-run e retorna relatório com flags |
| 3 | Admin e gestores realizam triagem assistida: atribuem owners, definem percentuais de parceiros |
| 4 | Todos os flags obrigatórios resolvidos; admin inicia import transacional |
| 5 | Import concluído; relatório final auditado gerado |
| 6 | Planilha congelada (read-only) no OneDrive |

#### Fluxos de Exceção

| Código | Erro | Tratamento |
|---|---|---|
| FE-UC06-01 | Falha durante import | Rollback total; MSG-034; admin pode reiniciar |
| FE-UC06-02 | Arquivo com formato inválido | MSG-032; dry-run abortado |

---

### UC-07 — Configurar tenant e white-label

| Campo | Descrição |
|---|---|
| Objetivo | Tenant Admin configura a identidade visual do tenant e as definições operacionais (slug, fuso) |
| Ator Principal | ACT-04 |
| Atores Secundários | — |
| Pré-condições | Tenant provisionado pelo Operador da Plataforma |
| Pós-condições | Branding aplicado ao portal e ao digest; slug imutável definido; fuso IANA configurado |
| Requisitos Relacionados | FRD-admin-01, FRD-admin-02 |

#### Fluxo Principal

| Passo | Descrição |
|---|---|
| 1 | Admin acessa configurações do tenant |
| 2 | Faz upload de logo e favicon |
| 3 | Define cor primária e secundária; sistema valida WCAG AA |
| 4 | Define slug (imutável após confirmação) e fuso horário IANA |
| 5 | Sistema aplica branding via CSS variables |

---

### UC-08 — Provisionar novo tenant *(Platform Operator)*

| Campo | Descrição |
|---|---|
| Objetivo | Operador da plataforma cria um novo tenant isolado com branding inicial e usuário admin |
| Ator Principal | ACT-05 |
| Atores Secundários | — |
| Pré-condições | Acesso ao backoffice de plataforma |
| Pós-condições | Tenant provisionado; tenant de identidade criado no Identity Platform; slug ativo; Tenant Admin convidado |
| Requisitos Relacionados | FRD-auth-03, FRD-admin-02 |

#### Critérios de Aceite

- [ ] Tenant provisionado não tem acesso a dados de outros tenants (verificado por testes de isolamento em CI)
- [ ] Platform Operator não acessa dados comerciais do tenant por padrão

---

## 13. Regras de Negócio

| Código | Regra | Descrição | Requisitos Relacionados | Fonte |
|---|---|---|---|---|
| RN-001 | Numeração sequencial imutável por tenant | Toda oportunidade recebe um número sequencial no formato `AZ-NNNN`, gerado pelo sistema de forma atômica, imutável após criação e nunca reutilizado mesmo após exclusão | FRD-pipeline-02, FRD-migration-03 | PRD RF-06; substituição da fórmula `LARGE()+1` da planilha |
| RN-002 | `owner_id` obrigatório em toda oportunidade | Toda oportunidade deve ter um responsável definido; API retorna HTTP 422 sem `owner_id`; campo pré-preenchido com o usuário autenticado | FRD-pipeline-02 | PRD RF-06; DEC-010; evidência: 64/108 sem owner |
| RN-003 | `data_fechamento_esperada` obrigatória a partir de "Proposta Enviada" | Transição de qualquer oportunidade para o estágio "Proposta Enviada" ou estágios posteriores (Negociação, Fechamento Provável) é bloqueada se `data_fechamento_esperada` estiver ausente | FRD-pipeline-04 | PRD RF-06; J-01 |
| RN-004 | `motivo_perda` obrigatório ao mover para "Perdido" | Mover uma oportunidade para o estágio com categoria "perdida" exige seleção obrigatória de um motivo da lista configurável da BU; bloqueante sem possibilidade de contornar | FRD-pipeline-07 | PRD RF-06; J-01 |
| RN-005 | Cálculo de valor_total | `valor_total = valor_setup + valor_mensal × duracao_meses`; todos os valores em centavos inteiros; `valor_total` nunca é digitado diretamente — é sempre calculado | FRD-pipeline-01, FRD-pipeline-02 | PRD RF-06; DEC-011 |
| RN-006 | Cálculo de forecast_ponderado | `forecast_ponderado = valor_total × probabilidade_do_estagio`; probabilidade é o percentual default configurado no estágio (ex.: 60% para "Proposta Enviada"); em centavos inteiros | FRD-pipeline-01, FRD-goal-02 | PRD RF-06 |
| RN-007 | Snapshot imutável de comissão ao mover para "Ganho" | Ao mover oportunidade para o estágio "Ganho", o sistema cria registro `OpportunityPartnerCommission` com snapshot dos percentuais, valores calculados e `fechado_em`; esse registro não pode ser editado por nenhum usuário após a criação | FRD-pipeline-06 | PRD RF-06; DEC-002; J-05 |
| RN-008 | Canal "Parceiro" exige `partner_id` | Quando `canal_origem = Parceiro`, o campo `partner_id` é obrigatório; API retorna HTTP 422 sem ele | FRD-pipeline-02, FRD-org-01 | PRD RF-06; RF-05 |
| RN-009 | Seleção de destinatários do digest por fuso IANA | Cloud Scheduler dispara job horário em UTC; o serviço converte para hora local de cada tenant usando o fuso IANA configurado; envia quando hora local = `horario_digest` (default 07:00) e o dia local = seg–sex | FRD-digest-01 | PRD RF-09; DEC-009 |
| RN-010 | Idempotência do digest por EmailDigestLog | O registro `(user_id, date)` no `EmailDigestLog` impede reenvio no mesmo dia; `date` é calculada no fuso do tenant; retentativas verificam antes de enviar | FRD-digest-04 | PRD RF-09; US-01 |
| RN-011 | Usuário sem pendências e sem papel de gestão não recebe digest | Pendências = atividades vencidas OU atividades de hoje OU oportunidades estagnadas OU `data_fechamento_esperada` vencida; usuários com papel Gestor de BU ou Executivo recebem independentemente de pendências na segunda-feira (azimute) | FRD-digest-01 | PRD RF-09 |
| RN-012 | Isolamento de dados por tenant (RLS) | Toda entidade de negócio carrega `tenant_id`; EF Core aplica global query filter; Postgres aplica RLS via `SET app.current_tenant`; vazamento entre tenants = incidente sev-1; testes de isolamento são gate de CI obrigatório | Todos os módulos | PRD seção 8.3; DEC-006 |
| RN-013 | Desativação de usuário preserva histórico | Usuário desativado não pode autenticar; FKs de oportunidades, atividades e audit logs intactas; nome histórico exibido nos registros | FRD-org-02 | PRD RF-03 |
| RN-014 | Dedupe de conta por nome normalizado | Antes de criar uma conta, o sistema normaliza o nome (remove acentos, converte para minúsculas, colapsa espaços) e busca similaridade; se encontrar, exibe alerta com opção de usar existente ou criar novo mesmo assim | FRD-account-01 | PRD RF-04 |
| RN-015 | Valores monetários como centavos inteiros | Todos os campos de valor (`valor_setup`, `valor_mensal`, `valor_total`, `forecast_ponderado`, `valor_meta`, comissões) são armazenados, calculados e transmitidos via API como inteiros em centavos; exibição em BRL formatado é responsabilidade do frontend | Todos os módulos com valor | DEC-011; PRD seção 1.1 |
| RN-016 | Reabertura restrita a Tenant Admin e Gestor de BU | Oportunidades em estágio "Ganho" ou "Perdido" só podem ser reabertas por usuários com papel Tenant Admin ou Gestor de BU (na BU da oportunidade); Vendedor não tem esta permissão | FRD-pipeline-08 | PRD RF-06 |
| RN-017 | Graceful degradation sem meta cadastrada | Painel de forecast exibe realizado e pipeline sem erro ou placeholder vazio quando não há meta; percentual de atingimento é omitido; sem mensagem de erro conspícua | FRD-goal-02 | PRD RF-08 |
| RN-018 | Bloco de metas omitido no digest sem meta | O bloco "realizado vs meta" no digest de segunda-feira (azimute da semana) é completamente omitido quando não há meta cadastrada para o período; sem espaço vazio ou "Meta: —" | FRD-digest-03 | PRD RF-09; RF-08 |
| RN-019 | Slug de tenant imutável após definição | O slug identifica o tenant na URL canônica; uma vez definido e confirmado, não pode ser alterado via interface; mudança exige operação de plataforma | FRD-admin-02 | PRD RF-02; DEC-004 |
| RN-020 | Validações de logo e cores do tenant | Logo aceito apenas em PNG ou SVG com tamanho ≤ 1 MB; cores validadas por contraste WCAG 2.1 nível AA mínimo em relação ao branco e ao preto; alerta (não bloqueio) para contraste insuficiente | FRD-admin-01 | PRD RF-02; DEC-004 |
| RN-021 | Parceiro sem acesso ao sistema | Parceiro comissionado é entidade de dados gerida pelo tenant; não tem credencial, login ou portal no MVP | FRD-partner-01 | PRD seção 9.1; DEC-012 |
| RN-022 | Snapshot de comissão preservado após alterações futuras | Edição dos percentuais de comissão do parceiro após o fechamento de uma oportunidade não altera o snapshot imutável gerado no momento do fechamento | FRD-pipeline-06 | PRD RF-06; DEC-002 |
| RN-023 | Import transacional com rollback total | O import da planilha é executado como transação única; qualquer falha em qualquer registro provoca rollback total; nenhum dado parcial é persistido | FRD-migration-03 | PRD J-04 |
| RN-024 | AuditLog imutável para toda escrita em entidade de negócio | Toda operação de criação, edição ou exclusão em entidades de negócio (oportunidade, conta, contato, atividade, parceiro, meta) gera um registro imutável no `AuditLog` com: `user_id`, `entity_type`, `entity_id`, `action`, `delta_json`, `timestamp`, `tenant_id` | Todos os módulos | PRD seção 8.6 |
| RN-025 | PII de contatos não aparece em logs | Nome, e-mail e celular de contatos não são incluídos em nenhum log de Cloud Logging ou trace; esses campos são mascarados ou omitidos antes do logging | FRD-account-02 | PRD seção 8.4; LGPD |
| RN-026 | Fórmula de comissão projetada | Comissão calculada = `pct_setup × valor_setup + pct_recorrente × valor_mensal × meses_comissionados`; resultado em centavos inteiros; arredondamento para o inteiro mais próximo | FRD-partner-02, FRD-pipeline-05 | PRD RF-05; DEC-002 |
| RN-027 | Metas com agregação derivada | Metas são definidas na granularidade mensal; agregação trimestral = soma dos 3 meses do trimestre; anual = soma dos 12 meses; não há campo de meta trimestral ou anual separado | FRD-goal-01 | PRD RF-08; DEC-003 |
| RN-028 | Critério de estagnação de oportunidade | Oportunidade estagnada = oportunidade aberta (categoria "aberta") sem nenhuma atividade registrada nos últimos 14 dias corridos; calculado em tempo real; sinalizado no Kanban e no digest | FRD-pipeline-01, FRD-digest-02 | PRD RF-07; J-01 |
| RN-029 | Azimute da semana apenas na segunda-feira | O conteúdo do "azimute da semana" (variação de pipeline, top oportunidades, fechamentos esperados, bloco de metas) é incluído apenas no digest de segunda-feira; de terça a sexta, o digest contém apenas as pendências do usuário | FRD-digest-03 | PRD RF-09 |
| RN-030 | Link de convite com expiração configurável | Token de convite de usuário tem prazo de validade configurável (referência: 72 horas a partir do envio); após expirar, o link é inválido e o admin precisa reenviar o convite | FRD-auth-03 | PRD RF-01; *Inferência Funcional* — prazo de 72 h não está explícito no PRD; confirmar em VAL-09 |

---

## 14. Mensagens de Erro e Validação

| Código | Cenário | Mensagem | Tipo | Requisito Relacionado |
|---|---|---|---|---|
| MSG-001 | Credencial de login inválida | "E-mail ou senha incorretos. Verifique e tente novamente." | Erro de Negócio | FRD-auth-01 |
| MSG-002 | Slug de tenant inexistente ou inativo | "Este endereço não está disponível. Verifique o link de acesso." | Erro de Negócio | FRD-auth-01 |
| MSG-003 | Conta de usuário desativada | "Sua conta está desativada. Entre em contato com o administrador do sistema." | Erro de Permissão | FRD-auth-01 |
| MSG-004 | Conta não ativada (convite pendente) | "Sua conta ainda não foi ativada. Verifique o e-mail de convite ou solicite um novo ao administrador." | Erro de Negócio | FRD-auth-01 |
| MSG-005 | Falha de comunicação com provedor de identidade | "Serviço de autenticação indisponível no momento. Tente novamente em alguns instantes." | Erro Sistêmico | FRD-auth-01 |
| MSG-006 | E-mail Google não associado ao tenant | "Esta conta Google não está cadastrada neste sistema. Solicite um convite ao administrador." | Erro de Permissão | FRD-auth-02 |
| MSG-007 | E-mail já cadastrado no tenant ao convidar | "Este e-mail já está cadastrado. Edite o usuário existente ou use outro e-mail." | Validação | FRD-auth-03 |
| MSG-008 | Link de convite expirado | "Este link de convite expirou. Solicite um novo convite ao administrador." | Erro de Negócio | FRD-auth-03 |
| MSG-009 | Falha de entrega do e-mail de convite | "Não foi possível enviar o e-mail de convite. Verifique o endereço e tente novamente." | Erro de Integração | FRD-auth-03 |
| MSG-010 | Upload de logo com tamanho > 1 MB | "O arquivo de logo excede o limite de 1 MB. Reduza o tamanho e tente novamente." | Validação | FRD-admin-01 |
| MSG-011 | Upload de logo com formato inválido | "Formato de arquivo não suportado. Envie um arquivo PNG ou SVG." | Validação | FRD-admin-01 |
| MSG-012 | Cor com contraste WCAG AA insuficiente | "A cor selecionada pode dificultar a leitura (contraste: {valor}:1; mínimo recomendado: 4.5:1). Recomendamos ajustar antes de salvar." | Alerta | FRD-admin-01 |
| MSG-013 | Slug já em uso por outro tenant | "Este endereço já está em uso. Escolha outro identificador para o seu tenant." | Validação | FRD-admin-02 |
| MSG-014 | Slug com caracteres inválidos | "O endereço deve conter apenas letras minúsculas, números e hífens, sem espaços." | Validação | FRD-admin-02 |
| MSG-015 | Nome de BU duplicado no tenant | "Já existe uma BU com este nome. Escolha um nome diferente." | Validação | FRD-org-01 |
| MSG-016 | Tentativa de inativar BU com oportunidades ativas | "Esta BU possui {N} oportunidades ativas. Encerre ou realoque-as antes de inativar a BU." | Erro de Negócio | FRD-org-01 |
| MSG-017 | Remoção do último Tenant Admin | "O tenant precisa ter ao menos um administrador ativo. Promova outro usuário antes de remover este." | Erro de Negócio | FRD-org-02 |
| MSG-018 | Desativação de usuário com atividades futuras | "Este usuário possui {N} atividades futuras atribuídas. Reatribua-as antes de desativar." | Alerta | FRD-org-02 |
| MSG-019 | Nome de conta em branco | "O nome da conta é obrigatório." | Validação | FRD-account-01 |
| MSG-020 | E-mail de contato com formato inválido | "O e-mail informado não é válido. Verifique o formato (ex.: nome@dominio.com)." | Validação | FRD-account-02 |
| MSG-021 | Nome de parceiro duplicado | "Já existe um parceiro com este nome. Confirme se deseja criar um novo registro mesmo assim." | Alerta | FRD-partner-01 |
| MSG-022 | Oportunidade sem `owner_id` | "O responsável pela oportunidade é obrigatório. Selecione um responsável para continuar." | Validação | FRD-pipeline-02 |
| MSG-023 | Canal "Parceiro" sem `partner_id` | "O canal Parceiro exige a seleção de um parceiro. Selecione o parceiro responsável pela indicação." | Validação | FRD-pipeline-02 |
| MSG-024 | `valor_mensal` preenchido sem `duracao_meses` | "A duração do contrato é obrigatória quando há mensalidade. Informe a duração em meses." | Validação | FRD-pipeline-02 |
| MSG-025 | Transição para "Proposta Enviada" sem `data_fechamento_esperada` | "Data de fechamento esperada é obrigatória a partir de Proposta Enviada. Preencha a data antes de avançar." | Erro de Negócio | FRD-pipeline-04 |
| MSG-026 | Transição para "Perdido" sem `motivo_perda` | "Selecione o motivo de perda para mover esta oportunidade para Perdido." | Validação | FRD-pipeline-07 |
| MSG-027 | Usuário sem permissão para mover oportunidade | "Você não tem permissão para mover esta oportunidade. Contate o gestor da BU." | Erro de Permissão | FRD-pipeline-04 |
| MSG-028 | Oportunidade com parceiro e percentuais em branco ao mover para Ganho | "O parceiro {nome} está vinculado, mas os percentuais de comissão não foram definidos. {ação a definir — VAL-07}." | Ponto a Validar (VAL-07) | FRD-pipeline-05 |
| MSG-029 | Link de ação do digest já executado | "Esta ação já foi registrada. Nenhuma alteração adicional foi feita." | Sucesso | FRD-activity-03 |
| MSG-030 | Link autenticado do digest expirado | "Este link expirou. Acesse o portal para registrar sua ação." | Erro de Negócio | FRD-activity-03 |
| MSG-031 | Falha de entrega de e-mail de digest | "Falha no envio do digest para {user_id} em {date}. Verificar logs do provider." | Erro de Integração | FRD-digest-02 |
| MSG-032 | Upload de planilha com formato inválido | "Formato de arquivo não suportado. Envie o arquivo no formato .xlsx." | Validação | FRD-migration-01 |
| MSG-033 | Planilha com estrutura de colunas não reconhecida | "Não foi possível reconhecer a estrutura do arquivo. Colunas esperadas: {lista}. Verifique se o arquivo correto foi enviado." | Validação | FRD-migration-01 |
| MSG-034 | Falha durante o import transacional | "Ocorreu um erro durante o import. Nenhum dado foi alterado. Verifique os logs e tente novamente." | Erro Sistêmico | FRD-migration-03 |

---

## 15. Matriz de Permissões Funcionais

> Derivada da matriz RBAC do PRD (seção 5.2 das discovery notes). Papéis: **PlatOp** = Platform Operator; **TAdmin** = Tenant Admin; **GestorBU** = Gestor de BU (escopo da sua BU); **Vendedor** = escopo das suas oportunidades; **Viewer** = somente leitura no escopo designado.

| Funcionalidade | PlatOp | TAdmin | GestorBU | Vendedor | Viewer |
|---|---|---|---|---|---|
| Provisionar / suspender tenants | Sim | — | — | — | — |
| Acessar backoffice de plataforma | Sim | — | — | — | — |
| Configurar branding do tenant | — | Sim | — | — | — |
| Definir slug e fuso horário | — | Sim | — | — | — |
| Criar / editar / inativar BUs | — | Sim | — | — | — |
| Configurar estágios por BU | — | Sim | — | — | — |
| Configurar canais e motivos de perda | — | Sim | — | — | — |
| Convidar e gerenciar usuários | — | Sim | — | — | — |
| Desativar usuários | — | Sim | — | — | — |
| Criar / editar parceiros | — | Sim | Sim (sua BU) | — | — |
| Ver visão de comissão do parceiro | — | Sim | Sim (sua BU) | — | — |
| Cadastrar metas por BU | — | Sim | Sim (sua BU) | — | — |
| Ver painel de metas e forecast | — | Sim (tenant) | Sim (sua BU) | Sim (as suas) | Escopo |
| Criar conta | — | Sim | Sim | Sim | — |
| Editar conta | — | Sim | Sim (sua BU) | Sim (suas opps) | — |
| Ver visão 360° da conta | — | Sim | Sim (suas BUs) | Sim (suas BUs) | Escopo |
| Criar oportunidade | — | Sim | Sim (sua BU) | Sim (owner = si) | — |
| Editar oportunidade | — | Sim | Sim (sua BU) | Sim (owner = si) | — |
| Mover oportunidade de estágio | — | Sim | Sim (sua BU) | Sim (owner = si) | — |
| Registrar / editar comissão de parceiro | — | Sim | Sim (sua BU) | Sim (owner = si) | — |
| Reabrir oportunidade Ganha / Perdida | — | Sim | Sim (sua BU) | — | — |
| Editar snapshot de comissão | Bloqueado | Bloqueado | Bloqueado | Bloqueado | Bloqueado |
| Ver oportunidades | — | Todas | Sua(s) BU(s) | Suas (owner) | Escopo |
| Criar / editar atividades | — | Sim | Sim (sua BU) | Sim (as suas) | — |
| Ver atividades | — | Sim | Sim (sua BU) | As suas | Escopo |
| Ver relatórios | — | Tenant | BU | Os seus | Escopo |
| Export CSV de relatórios | — | Sim | Sim (sua BU) | Sim (os seus) | — |
| Ver AuditLog | — | Sim (tenant) | Sim (sua BU) | — | — |
| Acesso a dados comerciais dos tenants | Bloqueado¹ | — | — | — | — |

> ¹ Acesso do Platform Operator a dados comerciais dos tenants é bloqueado por padrão; liberado apenas em suporte autorizado, com registro auditado.

---

## 16. Matriz de Rastreabilidade

| Item PRD | Descrição PRD | Requisitos FRD | Status |
|---|---|---|---|
| RF-01 | Autenticação e acesso | FRD-auth-01, FRD-auth-02, FRD-auth-03, FRD-auth-04, FRD-auth-05 | Coberto |
| RF-02 | Administração do tenant (white-label estrito) | FRD-admin-01, FRD-admin-02 | Coberto |
| RF-03 | BUs, usuários e papéis | FRD-org-01, FRD-org-02 | Coberto |
| RF-04 | Contas e contatos | FRD-account-01, FRD-account-02, FRD-account-03 | Coberto |
| RF-05 | Parceiros | FRD-partner-01, FRD-partner-02 | Coberto |
| RF-06 | Pipeline e oportunidades | FRD-pipeline-01..09 | Coberto |
| RF-07 | Atividades e follow-ups | FRD-activity-01..04 | Coberto |
| RF-08 | Metas e forecast | FRD-goal-01, FRD-goal-02 | Parcialmente Coberto (Fase 2 — projeções avançadas e painel Direção — não detalhados; aguardam RF-08 completo) |
| RF-09 | Digest diário por e-mail | FRD-digest-01..04 | Coberto |
| RF-10 | Notificações in-app | FRD-notif-01 | Coberto (Fase 2; detalhamento simplificado intencional) |
| RF-11 | Relatórios | FRD-report-01..06 | Coberto |
| RF-12 | Automações visuais | FRD-workflow-01, FRD-workflow-02 | Parcialmente Coberto (Fase 2; nós e templates não detalhados individualmente) |
| RF-13 | Agentes de IA | FRD-ai-01..04 | Coberto (Fase 3; detalhamento simplificado intencional — FRD Fase 3 a ser expandido antes do início da fase) |
| DEC-001 | Estágios configuráveis por BU | FRD-org-01, RN-001 | Coberto |
| DEC-002 | Comissão por componente + snapshot imutável | FRD-pipeline-05, FRD-pipeline-06, RN-007, RN-022, RN-026 | Coberto |
| DEC-003 | Metas por BU/responsável, granularidade mensal | FRD-goal-01, RN-027 | Coberto |
| DEC-004 | White-label estrito | FRD-admin-01, FRD-admin-02, RN-019, RN-020 | Coberto |
| DEC-006 | Multi-tenancy pooled + RLS | RN-012 (transversal a todos os módulos) | Coberto |
| DEC-007 | Lead como estágio do funil | OOS-04; seed de estágios em FRD-org-01 | Coberto |
| DEC-008 | Postmark / IEmailSender | FRD-digest-02; DEP-03 | Coberto |
| DEC-009 | Digest por fuso IANA + job horário UTC | FRD-digest-01, RN-009 | Coberto |
| DEC-010 | Import da planilha como requisito obrigatório | FRD-migration-01..03; UC-06 | Coberto |
| DEC-011 | Valores em centavos inteiros | RN-015 (transversal) | Coberto |
| DEC-012 | Parceiro sem login no MVP | RN-021; OOS-02 | Coberto |
| DEC-013 | Billing fora do MVP | OOS-01 | Coberto |
| LAC-01/VAL-001 | Percentuais de comissão dos 11 parceiros | VAL-01 (este FRD) | Ponto a Validar |
| LAC-02/VAL-002 | Owners das 64 oportunidades sem responsável | VAL-02 (este FRD); FRD-migration-02 | Ponto a Validar |
| LAC-03/VAL-003 | Provedor de e-mail: Postmark vs SendGrid | VAL-03 (este FRD); DEP-03 | Ponto a Validar |
| LAC-05/VAL-005 | Suporte a multimoeda | VAL-05 (este FRD); OOS-05 | Ponto a Validar |
| LAC-06/VAL-006 | Calendário de feriados no digest (Fase 2) | VAL-06 (este FRD) | Ponto a Validar |
| LAC-07 | Base legal LGPD | VAL-08 (este FRD); RN-025 | Ponto a Validar |

---

## 17. Dependências Funcionais

| Código | Dependência | Tipo | Impacto se não resolvida |
|---|---|---|---|
| DEP-01 | Identity Platform GCP multi-tenant provisionado (Fase 0) | Técnica | Módulo de autenticação (MOD-01) não funciona; bloqueante para todos os demais módulos |
| DEP-02 | RLS e global query filter de tenant configurados (Fase 0) | Técnica | Isolamento multi-tenant não garantido; incidente sev-1 em produção |
| DEP-03 | SPF/DKIM/DMARC do domínio `mail.azim.com.br` configurados e provider de e-mail selecionado (LAC-03) | Técnica / Operacional | Digest não entregue; principal diferencial do produto comprometido |
| DEP-04 | BUs e estágios configurados antes do import da planilha | Funcional | Triagem assistida e import não podem ser executados sem BUs e estágios mapeados |
| DEP-05 | Owners definidos para todas as oportunidades na triagem (LAC-02) | Operacional | Import definitivo bloqueado; critério de conclusão da Fase 1 não atingido |
| DEP-06 | Parceiros com percentuais definidos (LAC-01) | Operacional | Relatório de comissões incompleto; snapshots de Ganho gerados com campos em branco |
| DEP-07 | Testes de isolamento de tenant em CI como gate obrigatório | Técnica | Regressões de RLS podem passar para produção sem detecção |
| DEP-08 | Cloud Scheduler provisionado (Fase 0) | Técnica | Digest não é disparado; automações (Fase 2) não funcionam |

---

## 18. Premissas

| Código | Premissa | Impacto se Falhar |
|---|---|---|
| PRE-01 | GCP Identity Platform multi-tenant disponível na região `southamerica-east1` via Terraform sem limitações técnicas relevantes | Estratégia de autenticação multi-tenant exige revisão completa |
| PRE-02 | A Vellus define owners para as 64 oportunidades sem responsável antes do import definitivo | Critério de conclusão da Fase 1 não é atingido |
| PRE-03 | SPF/DKIM/DMARC do domínio `mail.azim.com.br` configurados antes do primeiro envio em produção | Taxa de entrega do digest abaixo de 98%; principal diferencial comprometido |
| PRE-04 | Todos os valores monetários das oportunidades da Vellus estão em BRL no MVP | Campo `currency` precisa ser adicionado ao modelo de dados antes do schema freeze |
| PRE-05 | A planilha `Pipeline Vellus.xlsx` é congelada (read-only) imediatamente após o import definitivo | Divergência entre planilha e Azim; necessidade de novo ciclo de import |

---

## 19. Pontos a Validar

| Código | Ponto | Origem | Impacto | Recomendação |
|---|---|---|---|---|
| VAL-01 | Percentuais de comissão dos 11 parceiros da Vellus (Montanaro, Salto, JV Korporate, Bus2, Finaya, Paytime, Nelson e outros) são inexistentes na planilha | LAC-01 / PRD | Relatório de comissões incompleto; snapshots de Ganho com campos em branco; KPI-07 em risco | Triagem obrigatória na interface de migração (FRD-migration-02); import deve aceitar percentuais em branco com flag; definir se percentual em branco bloqueia o fechamento como Ganho (VAL-07) |
| VAL-02 | Atribuição de owner para as 64 oportunidades sem responsável | LAC-02 / PRD | Bloqueia o critério de conclusão da Fase 1 ("100% com owner"); KPI-01 em risco | Triagem assistida em massa por BU (FRD-migration-02); interface deve permitir atribuição de um owner para todas as oportunidades de uma BU de uma vez |
| VAL-03 | Provedor de e-mail transacional: Postmark vs SendGrid (spike previsto no MVP) | LAC-03 / PRD | Pode confirmar ou reverter DEC-008; impacta RF-09, RF-01 e todas as notificações | Executar spike de entregabilidade + SPF/DKIM/DMARC + custo antes do primeiro envio em produção; critério: entregabilidade >= 93,8% |
| VAL-04 | Definição de pricing e planos do Azim como produto SaaS | LAC-04 / PRD | Não bloqueia o MVP; impacta o roadmap comercial pós-Fase 1 | Decidir antes do onboarding do segundo tenant |
| VAL-05 | Suporte a multimoeda: a Vellus pode fechar negócios em USD ou EUR antes da Fase 2 | LAC-05 / PRD | Exige campo `currency` no modelo de dados antes do schema freeze; impacta cálculos de forecast e comissão | Confirmar com equipe comercial da Vellus antes do schema freeze; se necessário, antecipar para Fase 1 |
| VAL-06 | Calendário de feriados para o digest na Fase 2: quais feriados, para quais países/estados e de qual fonte | LAC-06 / PRD | Impacta RF-09 na Fase 2; pode exigir integração com API de feriados | Decidir antes de iniciar a Fase 2; avaliar: lista curada por tenant vs API de feriados por país/estado |
| VAL-07 | Comportamento ao mover oportunidade para "Ganho" com parceiro associado e percentuais de comissão em branco | Lacuna funcional identificada neste FRD (MSG-028) | Se alerta: snapshot gerado com zeros, relatório de comissões incorreto. Se bloqueio: gestor precisa definir percentuais antes de fechar | Recomendação: exibir alerta com confirmação explícita (não bloqueio) e gerar snapshot com zeros marcados como "não definido" |
| VAL-08 | Base legal LGPD para tratamento de dados pessoais de contatos (nome, e-mail, celular) e política de retenção de dados | LAC-07 / PRD | Impacta FRD-account-02, RN-025 e conformidade LGPD | Confirmar com equipe jurídica da Vellus; documentar base legal antes do go-live; detalhar no NFRD |
| VAL-09 | Prazo de expiração do link de convite de usuário: 72 horas (inferência) | RN-030 / *Inferência Funcional* | Se prazo muito curto: usuários não ativam a tempo; se muito longo: risco de segurança | Confirmar com produto e segurança; documentar no NFRD como política de segurança |
| VAL-10 | Import parcial por BU (a confirmar): possibilidade de importar uma BU por vez para validação incremental antes do import completo | PRD J-04 ("a confirmar no frd.md") | Impacta o fluxo de migração (FRD-migration-02, FRD-migration-03); add complexity ao import transacional | Avaliar viabilidade com engenharia; se implementado, cada import parcial de BU deve ser atômico |

---

## 20. Anexos

### Anexo A — Seed de Estágios do Pipeline (Vellus)

| Ordem | Estágio | Probabilidade Default | Categoria | Observação |
|---|---|---|---|---|
| 1 | Lead | 10% | aberta | Primeiro contato; sem compromisso |
| 2 | Prospecção | 25% | aberta | Interesse qualificado identificado |
| 3 | Diagnóstico | 50% | aberta | Necessidade validada; solução apresentável |
| 4 | Proposta Enviada | 60% | aberta | `data_fechamento_esperada` obrigatória a partir daqui |
| 5 | Negociação | 75% | aberta | Proposta em discussão ativa |
| 6 | Fechamento Provável | 90% | aberta | Acordo verbal; pendente assinatura |
| 7 | Ganho | 100% | ganha | Contrato assinado; snapshot de comissão gerado |
| 8 | Perdido | 0% | perdida | `motivo_perda` obrigatório |

### Anexo B — Canais de Origem (Seed)

| Canal | `partner_id` Obrigatório | Observação |
|---|---|---|
| Parceiro | Sim | Exige vinculação de parceiro (RN-008) |
| Indicação | Não | Pessoa indica; sem estrutura de comissão automática |
| Prospecção ativa | Não | Outbound |
| Inbound | Não | Site, conteúdo, formulário |
| Evento | Não | Feiras, webinars |
| Base / Cliente existente | Não | Expansão de conta |
| Outro | Não | Catch-all configurável |

### Anexo C — Campos da Entidade Opportunity (resumo funcional)

| Campo | Tipo | Obrigatório | Regra |
|---|---|---|---|
| `id` | uuid | Sim | Gerado pelo sistema |
| `numero` | string | Sim | Sequencial por tenant; formato `AZ-NNNN`; imutável (RN-001) |
| `tenant_id` | uuid | Sim | Isolamento RLS (RN-012) |
| `bu_id` | uuid | Sim | BU à qual pertence |
| `owner_id` | uuid | Sim | Obrigatório; sem exceções (RN-002) |
| `account_id` | uuid | Sim | Conta vinculada |
| `stage_id` | uuid | Sim | Estágio atual |
| `canal_origem` | enum | Sim | Lista configurável por BU |
| `partner_id` | uuid | Condicional | Obrigatório se canal = Parceiro (RN-008) |
| `valor_setup` | integer | Não | Centavos inteiros (RN-015) |
| `valor_mensal` | integer | Não | Centavos inteiros |
| `duracao_meses` | integer | Condicional | Obrigatório se valor_mensal > 0 |
| `valor_total` | integer | Calculado | = valor_setup + valor_mensal × duracao_meses (RN-005) |
| `forecast_ponderado` | integer | Calculado | = valor_total × probabilidade do estágio (RN-006) |
| `data_fechamento_esperada` | date | Condicional | Obrigatória a partir de Proposta Enviada (RN-003) |
| `motivo_perda_id` | uuid | Condicional | Obrigatório ao mover para Perdido (RN-004) |
| `created_at` | timestamp | Sim | Gerado pelo sistema |
| `updated_at` | timestamp | Sim | Atualizado automaticamente |

### Anexo D — Entidade OpportunityPartnerCommission (snapshot)

| Campo | Tipo | Descrição |
|---|---|---|
| `id` | uuid | Identificador do snapshot |
| `opportunity_id` | uuid | Oportunidade à qual pertence |
| `partner_id` | uuid | Parceiro comissionado |
| `tenant_id` | uuid | Tenant (isolamento RLS) |
| `pct_setup` | decimal | Percentual sobre valor_setup no momento do fechamento |
| `pct_recorrente` | decimal | Percentual sobre valor_mensal no momento do fechamento |
| `valor_fixo` | integer | Centavos inteiros; opcional |
| `meses_comissionados` | integer | Meses comissionados no momento do fechamento |
| `valor_calculado` | integer | Comissão calculada em centavos inteiros (RN-026) |
| `fechado_em` | timestamp | Timestamp do fechamento como Ganho |
| `snapshot_imutavel` | boolean | Sempre `true`; nunca alterado após criação (RN-007) |

### Anexo E — Glossário Funcional

| Termo | Definição Funcional |
|---|---|
| Estagnação | Oportunidade aberta sem atividade registrada há > 14 dias corridos (RN-028) |
| Azimute da semana | Digest de segunda-feira com visão executiva de pipeline, variação semanal e metas |
| Graceful degradation | Painel exibe dados disponíveis sem erro quando dado opcional (ex.: meta) está ausente |
| Snapshot imutável | Registro de comissão criado ao fechar oportunidade como Ganho; nunca editável (RN-007) |
| Integer cents | Convenção: R$ 1.000,00 = 100.000 centavos; armazenados como inteiros (RN-015) |
| Dry-run | Execução de simulação de import sem efeitos colaterais no banco (FRD-migration-01) |
| EmailDigestLog | Registro de idempotência do digest por usuário+data (RN-010) |
| IEmailSender | Abstração de interface para troca de provider de e-mail sem mudança no código de negócio |
