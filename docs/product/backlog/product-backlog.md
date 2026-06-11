# Product Backlog — Azim CRM

- **Versão:** 0.1.0
- **Data:** 2026-06-11
- **Status:** Em planejamento
- **Total de épicos (módulos):** 13
- **Total de user stories:** 82
- **Total de tasks:** 321
- **Total de bugs ativos:** 0
- **Sprint length:** 2 semanas
- **Total de sprints planejadas:** 15
- **Aprovação HITL:** HITL #1 aprovado em 2026-06-11 — todas as 321 TASKs de Fase 1 aprovadas para entrar no backlog

---

## 1. Épicos (Módulos)

| ID Local | Épico (Módulo) | Subdomínio | Compliance | Stories | Tasks | Deployable | Status |
|---|---|---|---|---|---|---|---|
| EP-001 | audit-log | Generic | LGPD (delta_json mascarado) | 6 | 20 | azim-api | Em planejamento |
| EP-002 | notification-delivery | Generic | LGPD (e-mail hash) | 5 | 21 | azim-digest-worker / azim-api | Em planejamento |
| EP-003 | authentication | Generic | LGPD (e-mail, display_name) | 7 | 25 | azim-api | Em planejamento |
| EP-004 | tenant-administration | Generic | — | 5 | 22 | azim-api | Em planejamento |
| EP-005 | organization | Supporting | LGPD (e-mail, display_name em users) | 6 | 28 | azim-api | Em planejamento |
| EP-006 | account-management | Supporting | LGPD (nome, e-mail, telefone em contacts) | 5 | 18 | azim-api | Em planejamento |
| EP-007 | partner-management | Supporting | — | 4 | 29 | azim-api | Em planejamento |
| EP-008 | activity-management | Supporting | LGPD (link digest sem PII) | 5 | 24 | azim-api | Em planejamento |
| EP-009 | goal-forecast | Supporting | — | 6 | 30 | azim-api | Em planejamento |
| EP-010 | data-migration | Supporting | LGPD (PII em planilha legada) | 5 | 28 | azim-api (temporário Fase 1) | Em planejamento |
| EP-011 | digest | Supporting | LGPD (e-mail, tokens) | 5 | 27 | azim-digest-worker | Em planejamento |
| EP-012 | reporting | Supporting | LGPD (PiiMinimization ranking) | 6 | 25 | azim-reporting-worker | Em planejamento |
| EP-013 | opportunity-pipeline | Core Domain | LGPD (PII em title proibido) | 13 | 24 | azim-api | Em planejamento |

**Caminho crítico:** EP-001 → EP-002 → EP-003 → EP-004 → EP-005 → EP-006, EP-007, EP-008, EP-009 (paralelo) → EP-011, EP-012 → EP-013

**Nota sobre dependência de EP-013:** opportunity-pipeline depende de EP-005 (organização/stages), EP-006 (accounts), EP-007 (partners), EP-008 (activities).

---

## 2. User Stories (todas)

### Épico EP-001 — audit-log

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-001 | Como Administrador, quero que toda escrita em entidade de negócio seja registrada automaticamente via IAuditWriter, para garantir trilha de auditoria imutável e rastreável | REQ-001, REQ-006 | 5 | Sprint 1 | TO DO |
| US-002 | Como TAdmin, quero consultar a trilha de auditoria com filtros por entidade, usuário e período com paginação, para investigar mudanças críticas | REQ-007 | 3 | Sprint 6 | TO DO |
| US-003 | Como DPO, quero que dados PII (nome, e-mail, telefone de Contact) sejam automaticamente mascarados no delta antes de persistir, para conformidade LGPD | REQ-004 | 5 | Sprint 4 | TO DO |
| US-004 | Como Arquiteto de Segurança, quero que audit_logs seja append-only enforçado por trigger PostgreSQL e REVOKE no role app, para garantir integridade inviolável | REQ-002, REQ-005 | 5 | Sprint 4 | TO DO |
| US-005 | Como GestorBU, quero ver apenas registros de auditoria das entidades da minha BU, para isolamento de dados multi-tenant | REQ-005, REQ-008 | 3 | Sprint 6 | TO DO |
| US-006 | Como Arquiteto, quero que falha na inserção de auditoria reverta também a escrita de negócio na mesma transação (fail-closed), para consistência garantida | REQ-001 | 5 | Sprint 4 | TO DO |

### Épico EP-002 — notification-delivery

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-007 | Como módulo consumidor (digest, authentication), quero um IEmailSender único e portável que abstraia o provedor (Resend/Postmark/SendGrid), para não ter acoplamento ao fornecedor | Req 1, Req 4 | 5 | Sprint 1 | TO DO |
| US-008 | Como TAdmin, quero que o branding (logo, cores) do tenant seja aplicado automaticamente em todos os e-mails transacionais, para identidade visual consistente | Req 5, Req 6 | 3 | Sprint 5 | TO DO |
| US-009 | Como Operador de TI, quero retry automático com circuit breaker e backoff exponencial no envio de e-mail, para resiliência contra falhas de provedor | Req 8, Req 9 | 5 | Sprint 5 | TO DO |
| US-010 | Como DPO, quero que o endereço de e-mail do destinatário nunca apareça em logs em texto claro (hash SHA-256 truncado via EmailHasher), para conformidade LGPD | Req 3, RNF 4 | 3 | Sprint 6 | TO DO |
| US-011 | Como Operador de TI, quero health check do provedor de e-mail ativo exposto em /health sem consumir cota, para monitoramento de SLA | Req 11 | 2 | Sprint 5 | TO DO |

### Épico EP-003 — authentication

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-012 | Como Usuário, quero fazer login com Google SSO ou e-mail/senha pelo GCP Identity Platform com sessão escopada ao tenant do slug, para acesso seguro ao CRM | Req 1, Req 4 | 5 | Sprint 2 | TO DO |
| US-013 | Como TAdmin, quero convidar usuários por e-mail com link de ativação de 72h (uso único), para onboarding controlado sem expor senhas | Req 7 | 5 | Sprint 5 | TO DO |
| US-014 | Como Usuário, quero solicitar redefinição de senha recebendo resposta idêntica para e-mail existente ou inexistente (anti-enumeração), para segurança | Req 8, Req 10 | 3 | Sprint 5 | TO DO |
| US-015 | Como Usuário, quero fazer logout que revoga refresh tokens no IdP de forma idempotente, para encerrar a sessão com segurança | Req 9 | 3 | Sprint 5 | TO DO |
| US-016 | Como Arquiteto, quero que tokens JWT sejam verificados localmente via JWKS com cache de chaves (sem round-trip ao IdP por requisição), para latência < 100ms por request | Req 4, RNF 2 | 5 | Sprint 3 | TO DO |
| US-017 | Como Arquiteto, quero que memberships de BU sejam resolvidas em cache Redis com TTL e invalidação por evento user.role_changed, para p95 ≤ 100ms no middleware de auth | Req 5, RNF 2 | 5 | Sprint 4 | TO DO |
| US-018 | Como Operador de TI, quero que tentativas excessivas de login/reset por IP sejam bloqueadas com 429 sem revelar existência de conta, para proteção contra bruteforce | Req 10 | 3 | Sprint 5 | TO DO |

### Épico EP-004 — tenant-administration

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-019 | Como PlatformOperator, quero provisionar novo tenant com slug único, fuso IANA, e-mail de admin e configuração inicial em operação atômica (saga compensatória), para onboarding seguro de clientes | Req 1, Req 2 | 8 | Sprint 2 | TO DO |
| US-020 | Como PlatformOperator, quero suspender e reativar tenants com transições de estado válidas, para gestão do ciclo de vida de assinaturas | Req 4 | 3 | Sprint 3 | TO DO |
| US-021 | Como TAdmin, quero configurar branding do tenant (logo SVG/PNG ≤ 1MB, favicon, cores com validação WCAG AA), para identidade visual white-label | Req 5, Req 6, Req 7, Req 8 | 8 | Sprint 3 | TO DO |
| US-022 | Como TAdmin, quero configurar horário e fuso do digest diário por tenant (IANA tz), para personalização do envio | Req 3 | 3 | Sprint 3 | TO DO |
| US-023 | Como Frontend / CDN, quero obter brand.json público por slug sem autenticação com cache CDN, para carregamento de tema em < 200ms | Req 9 | 3 | Sprint 5 | TO DO |

### Épico EP-005 — organization

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-024 | Como TAdmin, quero criar, renomear e desativar Business Units com nome único por tenant, para estruturar equipes de vendas | RF org-BU | 5 | Sprint 7 | TO DO |
| US-025 | Como TAdmin, quero convidar membros para BUs atribuindo papéis (Vendedor, GestorBU, Viewer) com link de convite idempotente, para controle de acesso RBAC | RF org-invite | 5 | Sprint 7 | TO DO |
| US-026 | Como TAdmin, quero desativar usuários sem excluí-los, preservando histórico de atividades e oportunidades, para auditoria e LGPD | RF org-deactivate | 3 | Sprint 7 | TO DO |
| US-027 | Como TAdmin, quero configurar estágios do pipeline por BU (nome, categoria, probabilidade, ordem), para personalizar funil de vendas | RF org-pipeline | 5 | Sprint 8 | TO DO |
| US-028 | Como TAdmin, quero configurar canais de origem e motivos de perda por tenant, para análise de CRM | RF org-config | 3 | Sprint 8 | TO DO |
| US-029 | Como Arquiteto, quero que o provisionamento inicial de organização seja orquestrado por evento TenantProvisioned de forma idempotente, para integridade de setup | RF org-provision | 5 | Sprint 9 | TO DO |

### Épico EP-006 — account-management

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-030 | Como Vendedor, quero criar e pesquisar contas com nome normalizado para detecção automática de duplicatas, para manter base limpa | RF acc-create, RF acc-dedupe | 5 | Sprint 8 | TO DO |
| US-031 | Como Vendedor, quero vincular contatos PII (nome, e-mail, telefone, CNPJ, razão social, nome fantasia) a contas, para CRM B2B completo (inclui delta HITL#1) | RF acc-contact | 5 | Sprint 10 | TO DO |
| US-032 | Como Vendedor, quero consultar visão 360° de uma conta (atividades, oportunidades em aberto, contatos) com degradação graciosa se módulo indisponível, para contexto completo | RF acc-360 | 5 | Sprint 10 | TO DO |
| US-033 | Como DPO, quero exercer direito de esquecimento de contato (apagamento irreversível de PII) com registro de auditoria, para conformidade LGPD | RF acc-forget | 5 | Sprint 10 | TO DO |
| US-034 | Como TAdmin, quero que contas incluam CNPJ, razão social e nome fantasia como campos PII gerenciados, para CRM focado em pessoa jurídica (delta HITL#1 — ADR-0006) | RF acc-cnpj | 3 | Sprint 10 | TO DO |

### Épico EP-007 — partner-management

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-035 | Como TAdmin, quero cadastrar parceiros com nome, papel, contatos e percentuais de comissão default por componente (MRR, setup, hardware), para gestão de canal | RF prtn-create | 5 | Sprint 8 | TO DO |
| US-036 | Como TAdmin, quero desativar/reativar parceiros sem excluir histórico de comissões, para gestão de ciclo de vida | RF prtn-lifecycle | 3 | Sprint 9 | TO DO |
| US-037 | Como GestorBU, quero consultar elegibilidade de parceiro para vínculo com oportunidade (ativo, não desativado), para validação antes de vinculação | RF prtn-eligibility | 2 | Sprint 9 | TO DO |
| US-038 | Como Sistema (opportunity-pipeline), quero ler comissões default de parceiro via porta IPartnerCommissionReadPort com resiliência, para cálculo correto de comissão | RF prtn-commission-port | 3 | Sprint 9 | TO DO |

### Épico EP-008 — activity-management

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-039 | Como Vendedor, quero criar atividades (call, e-mail, reunião, tarefa) vinculadas a oportunidades e contas com data/hora e prioridade, para gestão do dia a dia de vendas | RF act-create | 5 | Sprint 11 | TO DO |
| US-040 | Como Vendedor, quero visualizar Meu Dia e Minha Semana com atividades do período filtradas por mim ou por BU, para produtividade | RF act-myview | 3 | Sprint 11 | TO DO |
| US-041 | Como Vendedor, quero concluir ou reagendar atividades pelo e-mail digest via link de 1 clique com token de uso único, para eficiência sem abrir o CRM | RF act-digest-action | 5 | Sprint 11 | TO DO |
| US-042 | Como GestorBU, quero detectar oportunidades estagnadas (sem atividade em X dias) e publicar evento OpportunityStale, para alerta proativo de risco | RF act-stagnation | 5 | Sprint 12 | TO DO |
| US-043 | Como Sistema (digest-worker), quero varrer atividades vencidas periodicamente e publicar ActivityOverdue para composição do digest, para conteúdo atualizado | RF act-overdue | 3 | Sprint 12 | TO DO |

### Épico EP-009 — goal-forecast

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-044 | Como GestorBU, quero definir meta mensal de receita por escopo (BU ou vendedor individual) por período, para acompanhamento de desempenho | RF gf-upsert | 5 | Sprint 11 | TO DO |
| US-045 | Como GestorBU, quero visualizar painel realizado vs meta com forecast ponderado e forecast líquido (após comissões), para tomada de decisão | RF gf-panel | 5 | Sprint 12 | TO DO |
| US-046 | Como Vendedor, quero ver minha meta individual e meu realizado do mês em formato compacto, para autogestão | RF gf-individual | 3 | Sprint 12 | TO DO |
| US-047 | Como Digest, quero obter bloco de meta/forecast estruturado via endpoint interno mTLS para composição do e-mail diário, para conteúdo relevante | RF gf-digest-block | 3 | Sprint 12 | TO DO |
| US-048 | Como GestorBU, quero ver agregação de metas por BU com breakdown por vendedor, para visão gerencial consolidada | RF gf-aggregate | 3 | Sprint 12 | TO DO |
| US-049 | Como Sistema, quero que metas e valores de forecast sejam representados em centavos com campo currency (BRL/USD/EUR), para suporte multimoeda (delta HITL#1 — ADR-0008) | RF gf-multicurrency | 5 | Sprint 14 | TO DO |

### Épico EP-010 — data-migration

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-050 | Como TAdmin, quero fazer upload de planilha Excel legada (.xlsx com até 500 linhas), para iniciar migração de dados do sistema anterior | RF mig-upload | 5 | Sprint 13 | TO DO |
| US-051 | Como TAdmin, quero executar dry-run com relatório de triagem (mapeamentos de owner, estágio, parceiro, duplicatas) antes de importar, para validar dados | RF mig-dryrun | 5 | Sprint 13 | TO DO |
| US-052 | Como TAdmin, quero importar dados transacionalmente (tudo-ou-nada com rollback garantido) após confirmação explícita, para integridade | RF mig-execute | 8 | Sprint 13 | TO DO |
| US-053 | Como TAdmin, quero acompanhar status do job de migração em tempo real (PENDING, RUNNING, COMPLETED, FAILED), para visibilidade de progresso | RF mig-status | 3 | Sprint 13 | TO DO |
| US-054 | Como Arquiteto, quero que o módulo data-migration seja desativado por feature flag migration.import_enabled após Fase 1, para remoção arquitetural planejada | RF mig-lifecycle | 3 | Sprint 13 | TO DO |

### Épico EP-011 — digest

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-055 | Como GestorBU, quero receber às 07:00 no meu fuso horário um e-mail digest com atividades do dia, oportunidades estagnadas e meta do mês, para gestão matinal | RF dg-content | 8 | Sprint 12 | TO DO |
| US-056 | Como Vendedor, quero receber atividades do meu dia no digest com links de ação de 1 clique (concluir/reagendar), para eficiência sem abrir o app | RF dg-links | 5 | Sprint 13 | TO DO |
| US-057 | Como Operador de TI, quero que o digest seja disparado via Cloud Scheduler com idempotência por tenant (um digest por recipient por dia), para confiabilidade | RF dg-trigger | 5 | Sprint 13 | TO DO |
| US-058 | Como DPO, quero que e-mails do digest não exponham PII de contato em logs nem em tokens de ação, para conformidade LGPD | RF dg-pii | 3 | Sprint 13 | TO DO |
| US-059 | Como DPO, quero que digest_action_tokens e email_digest_logs sejam purgados após 90 dias conforme política de retenção LGPD, para compliance | RF dg-retention | 3 | Sprint 13 | TO DO |

### Épico EP-012 — reporting

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-060 | Como GestorBU, quero relatório de funil de vendas (entrada, avanço, fechamento, tempo médio por estágio) filtrado por BU e período, para análise de pipeline | Req 1 | 5 | Sprint 14 | TO DO |
| US-061 | Como GestorBU, quero relatório de forecast consolidado vs meta por período com degradação graciosa se goal-forecast indisponível, para revisão de negócio | Req 6 | 5 | Sprint 14 | TO DO |
| US-062 | Como GestorBU, quero ranking de desempenho por vendedor com PiiMinimizationPolicy (mínimo de registros para exibir nome), para gestão com proteção de privacidade | Req 2, RNF 4 | 5 | Sprint 14 | TO DO |
| US-063 | Como GestorBU, quero relatório de canal de origem com distribuição percentual por canal, para análise de efetividade de marketing | Req 3 | 3 | Sprint 14 | TO DO |
| US-064 | Como TAdmin, quero relatório de comissões com distinção entre snapshot imutável (oportunidades ganhas) e projetado (em aberto), para gestão financeira | Req 4 | 5 | Sprint 14 | TO DO |
| US-065 | Como GestorBU, quero exportar qualquer relatório como CSV para análise offline em ferramentas externas, para flexibilidade analítica | Req 5 | 3 | Sprint 14 | TO DO |

### Épico EP-013 — opportunity-pipeline

| ID Local | Story | RF rastreado | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-066 | Como Vendedor, quero criar oportunidade por formulário completo ou criação rápida com conta inline, para registrar negociações no pipeline com mínimo atrito | Req 1 | 5 | Sprint 14 | TO DO |
| US-067 | Como Vendedor, quero mover oportunidade entre estágios com transição registrada na linha do tempo e probabilidade herdada/editável, para controle de pipeline | Req 5, Req 6, Req 7 | 5 | Sprint 14 | TO DO |
| US-068 | Como GestorBU, quero visualizar Kanban de oportunidades por BU com somas por estágio em tempo real (p95 ≤ 2.000ms para 2.000 cards), para gestão visual | Req 18 | 8 | Sprint 15 | TO DO |
| US-069 | Como GestorBU, quero encerrar oportunidade como Ganha com snapshot imutável de comissão por componente registrado no banco, para rastreabilidade financeira inviolável | Req 14 | 8 | Sprint 15 | TO DO |
| US-070 | Como GestorBU, quero encerrar oportunidade como Perdida com motivo obrigatório selecionado de lista configurada, para análise de perdas estruturada | Req 10 | 5 | Sprint 15 | TO DO |
| US-071 | Como GestorBU, quero reabrir oportunidade fechada com registro completo de auditoria e re-cálculo de snapshot, para correção de registros históricos | Req 15 | 5 | Sprint 15 | TO DO |
| US-072 | Como Vendedor, quero vincular parceiro e definir comissão por componente (MRR, setup, hardware) à oportunidade com visualização de forecast líquido, para gestão de canal | Req 11, Req 12, Req 13 | 8 | Sprint 15 | TO DO |
| US-073 | Como Sistema, quero calcular valor_total, forecast_ponderado e forecast_líquido em centavos com precisão garantida (PBT), para relatórios financeiros consistentes | Req 8, Req 13 | 5 | Sprint 14 | TO DO |
| US-074 | Como Vendedor, quero vincular contatos à oportunidade com exatamente um principal marcado, para rastreabilidade de relacionamento | Req 16 | 3 | Sprint 15 | TO DO |
| US-075 | Como GestorBU, quero que oportunidades sem atividade por N dias sejam marcadas como estagnadas e incluídas no digest, para acompanhamento proativo | Req 17 | 5 | Sprint 15 | TO DO |
| US-076 | Como Vendedor, quero salvar filtros customizados na lista de oportunidades e aplicá-los rapidamente, para produtividade | Req 19 | 3 | Sprint 15 | TO DO |
| US-077 | Como Sistema, quero que eventos de domínio (OpportunityCreated, StageChanged, Won, Lost, Stale, Reopened, CommissionCalculated) sejam publicados via Outbox+Pub/Sub, para integração entre módulos | Req 20 | 5 | Sprint 15 | TO DO |
| US-078 | Como Sistema, quero que oportunidades e comissões suportem multimoeda BRL/USD/EUR via objeto de valor Money com campo currency, para expansão internacional (delta HITL#1 — ADR-0008) | Req 8 | 8 | Sprint 15 | TO DO |

### Deltas HITL #1 (transversais)

| ID Local | Story | Origem | Story Points | Sprint | Status |
|---|---|---|---|---|---|
| US-079 | Como Arquiteto, quero formalizar ADRs 0001-0008 como documentos arquivados e revisados, para governança de decisões arquiteturais | HITL#1 ADRs | 3 | Sprint 1 | TO DO |
| US-080 | Como Operador de TI, quero configurar registros SPF, DKIM e DMARC no domínio de envio de e-mail para entregabilidade e proteção contra phishing | HITL#1 go-live | 3 | Sprint 15 | TO DO |
| US-081 | Como Operador de TI, quero migrar o provider de e-mail transacional para Resend conforme ADR-0005, com fallback documentado para SendGrid | HITL#1 ADR-0005 | 5 | Sprint 5 | TO DO |
| US-082 | Como DPO, quero definir formalmente base legal e prazo de retenção de dados pessoais (contacts, audit_logs, digest_logs) para LGPD, para conformidade antes do go-live | HITL#1 LGPD | 3 | Sprint 15 | TO DO |

---

## 3. Tasks (por módulo — referência canônica)

As tasks são identificadas pelo ID canônico `TASK-NN` dentro de cada módulo. A tabela abaixo lista as referências; o detalhe completo está em `docs/product/modules/<modulo>/tasks.md`.

| Módulo | Tasks | Sprint(s) primárias | Ondas |
|---|---|---|---|
| audit-log | TASK-01 a TASK-20 | Sprint 1, 4, 6 | Onda 1 (TASK-01..02) → Onda 2 (TASK-03..06) → Onda 3 (TASK-07..10) → Onda 4 (TASK-11..14) → Onda 5 (TASK-15..17) → Onda 6 (TASK-18..20) |
| notification-delivery | TASK-01 a TASK-21 | Sprint 1, 5, 6 | Onda 1 (TASK-01..02) → Onda 2 (TASK-03..06) → Onda 3 (TASK-07..10) → Onda 4 (TASK-11..16) → Onda 5 (TASK-17..21) |
| authentication | TASK-01 a TASK-25 | Sprint 2, 3, 4, 5 | Onda 1 (TASK-01..02) → Onda 2 (TASK-03..04) → Onda 3 (TASK-05..09) → Onda 4 (TASK-10..14) → Onda 5 (TASK-15..20) → Onda 6 (TASK-21..25) |
| tenant-administration | TASK-01 a TASK-22 | Sprint 2, 3, 4, 5 | Onda 1 (TASK-01) → Onda 2 (TASK-02..06) → Onda 3 (TASK-07..11) → Onda 4 (TASK-12..16) → Onda 5 (TASK-17..20) → Onda 6 (TASK-21..22) |
| organization | TASK-01 a TASK-28 | Sprint 7, 8, 9 | Onda 1 (TASK-01..02) → Onda 2 (TASK-03..06) → Onda 3 (TASK-07..14) → Onda 4 (TASK-15..19) → Onda 5 (TASK-20..24) → Onda 6 (TASK-25..28) |
| account-management | TASK-01 a TASK-18 | Sprint 8, 10 | Onda 1 (TASK-01) → Onda 2 (TASK-02..03) → Onda 3 (TASK-04..07) → Onda 4 (TASK-08..12) → Onda 5 (TASK-13..15) → Onda 6 (TASK-16..18) |
| partner-management | TASK-01 a TASK-29 | Sprint 8, 9, 10 | Onda 1 (TASK-01..02) → Onda 2 (TASK-03..08) → Onda 3 (TASK-09..14) → Onda 4 (TASK-15..21) → Onda 5 (TASK-22..25) → Onda 6 (TASK-26..29) |
| activity-management | TASK-01 a TASK-24 | Sprint 11, 12 | Onda 1 (TASK-01) → Onda 2 (TASK-02..05) → Onda 3 (TASK-06..12) → Onda 4 (TASK-13..17) → Onda 5 (TASK-18..21) → Onda 6 (TASK-22..24) |
| goal-forecast | TASK-01 a TASK-30 | Sprint 11, 12, 14 | Onda 1 (TASK-01..02) → Onda 2 (TASK-03..07) → Onda 3 (TASK-08..14) → Onda 4 (TASK-15..20) → Onda 5 (TASK-21..26) → Onda 6 (TASK-27..30) |
| data-migration | TASK-01 a TASK-28 | Sprint 13 | Onda 1 (TASK-01..02) → Onda 2 (TASK-03..07) → Onda 3 (TASK-08..13) → Onda 4 (TASK-14..20) → Onda 5 (TASK-21..23) → Onda 6 (TASK-24..28) |
| digest | TASK-01 a TASK-27 | Sprint 12, 13 | Onda 1 (TASK-01..02) → Onda 2 (TASK-03..08) → Onda 3 (TASK-09..13) → Onda 4 (TASK-14..20) → Onda 5 (TASK-21..23) → Onda 6 (TASK-24..27) |
| reporting | TASK-01 a TASK-25 | Sprint 14 | Onda 1 (TASK-01..02) → Onda 2 (TASK-03..04) → Onda 3 (TASK-05..12) → Onda 4 (TASK-13..19) → Onda 5 (TASK-20..22) → Onda 6 (TASK-23..25) |
| opportunity-pipeline | TASK-01 a TASK-24 | Sprint 14, 15 | Onda 1 (TASK-01) → Onda 2 (TASK-02..07) → Onda 3 (TASK-08..12) → Onda 4 (TASK-13..18) → Onda 5 (TASK-19..21) → Onda 6 (TASK-22..24) |

---

## 4. Bugs ativos

| ID Local | Épico | Bug | Severidade | Sprint | Status |
|---|---|---|---|---|---|
| — | — | Nenhum bug ativo no momento da criação do backlog | — | — | — |

---

## 5. Mapeamento Local ↔ Jira

> Tabela pendente de sincronização. MCP Atlassian não conectado nesta sessão. Todas as operações registradas em `progress-tracking.md §2`.

| ID Local | Issue Key Jira | Sincronizado em | Última sync |
|---|---|---|---|
| EP-001 | pendente (Azim-?) | — | — |
| EP-002 | pendente | — | — |
| EP-003 | pendente | — | — |
| EP-004 | pendente | — | — |
| EP-005 | pendente | — | — |
| EP-006 | pendente | — | — |
| EP-007 | pendente | — | — |
| EP-008 | pendente | — | — |
| EP-009 | pendente | — | — |
| EP-010 | pendente | — | — |
| EP-011 | pendente | — | — |
| EP-012 | pendente | — | — |
| EP-013 | pendente | — | — |
| US-001 a US-082 | pendente | — | — |
| Sprint 1 a 15 | pendente | — | — |
