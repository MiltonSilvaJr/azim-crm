# Backlog Progress Tracking — Azim CRM

- **Última atualização:** 2026-06-11 (sync Jira em andamento)
- **Última ação executada:** Sync Jira — 13 épicos criados (AZIM-1..AZIM-13)
- **Próxima ação:** criar 82 user stories (parent = épico) → 321 subtasks (parent = story) → 15 sprints + atribuições
- **Status geral:** Sync Jira PARCIAL — épicos OK; stories/tasks/sprints pendentes
- **Projeto Jira:** `AZIM` (id 10314, team-managed/next-gen, cloudId e1b6371c-f985-4363-b691-818ac8f6c6fc, site prod-fintech.atlassian.net). Tipos: Epic=10418, User story=10420, Tarefa=10421, Bug=10422, Subtask=10419.

### Mapeamento de épicos (Local → Jira)

| Local | Jira | Módulo |
|---|---|---|
| EP-001 | AZIM-1 | audit-log |
| EP-002 | AZIM-2 | notification-delivery |
| EP-003 | AZIM-3 | authentication |
| EP-004 | AZIM-4 | tenant-administration |
| EP-005 | AZIM-5 | organization |
| EP-006 | AZIM-6 | account-management |
| EP-007 | AZIM-7 | partner-management |
| EP-008 | AZIM-8 | activity-management |
| EP-009 | AZIM-9 | goal-forecast |
| EP-010 | AZIM-10 | data-migration |
| EP-011 | AZIM-11 | digest |
| EP-012 | AZIM-12 | reporting |
| EP-013 | AZIM-13 | opportunity-pipeline |

---

## 1. Estado por Sprint

| Sprint | Status | Stories TO DO | IN PROGRESS | IN REVIEW | DONE | Issue Jira da sprint |
|---|---|---|---|---|---|---|
| Sprint 1 — foundation | Planejada | 5 | 0 | 0 | 0 | pendente |
| Sprint 2 — auth-tenant-domain | Planejada | 5 | 0 | 0 | 0 | pendente |
| Sprint 3 — auth-tenant-application | Planejada | 5 | 0 | 0 | 0 | pendente |
| Sprint 4 — generic-infra-audit-app | Planejada | 4 | 0 | 0 | 0 | pendente |
| Sprint 5 — auth-tenant-api-notif-app | Planejada | 4 | 0 | 0 | 0 | pendente |
| Sprint 6 — audit-api-notif-hardening | Planejada | 4 | 0 | 0 | 0 | pendente |
| Sprint 7 — organization-foundation | Planejada | 3 | 0 | 0 | 0 | pendente |
| Sprint 8 — org-app-account-partner-domain | Planejada | 4 | 0 | 0 | 0 | pendente |
| Sprint 9 — org-api-account-partner-app | Planejada | 4 | 0 | 0 | 0 | pendente |
| Sprint 10 — account-partner-api-activity-domain | Planejada | 5 | 0 | 0 | 0 | pendente |
| Sprint 11 — activity-app-goal-domain | Planejada | 4 | 0 | 0 | 0 | pendente |
| Sprint 12 — activity-api-goal-api-digest-domain | Planejada | 4 | 0 | 0 | 0 | pendente |
| Sprint 13 — migration-digest-api-reporting | Planejada | 7 | 0 | 0 | 0 | pendente |
| Sprint 14 — reporting-api-pipeline-domain | Planejada | 10 | 0 | 0 | 0 | pendente |
| Sprint 15 — pipeline-api-hitl-golive | Planejada | 12 | 0 | 0 | 0 | pendente |

---

## 2. Operações pendentes de sincronização com Jira

| Timestamp | Operação | Alvo local | Status |
|---|---|---|---|
| 2026-06-11 | Criar projeto `Azim` (AZIM) | — | ✅ existia (criado pelo usuário) |
| 2026-06-11 | Criar 13 épicos (EP-001..013 → AZIM-1..13) | product-backlog.md §5 | ✅ FEITO |
| 2026-06-11 | Criar 82 user stories (US-001..082, parent = épico) | product-backlog.md §2 | ⏳ pendente |
| 2026-06-11 | Criar 321 subtasks (TASK-NN, parent = story) | product-backlog.md §3 | ⏳ pendente |
| 2026-06-11 | Criar/ativar 15 sprints + atribuir issues | sprints-planning.md | ⏳ pendente |

**Procedimento de retomada:**
1. Autenticar MCP Atlassian (`mcp__atlassian__atlassianUserInfo`)
2. Obter `cloudId` (`mcp__atlassian__getAccessibleAtlassianResources`)
3. Verificar/criar projeto Scrum `Azim` (`mcp__atlassian__getVisibleJiraProjects`)
4. Processar cada linha desta seção em ordem (épicos → stories → tasks → sprints → atribuições)
5. Para cada operação bem-sucedida: remover da seção §2 e registrar em §3 com issue key retornada
6. Atualizar `product-backlog.md §5` com mapeamento Local ↔ Jira

---

## 3. Histórico de Ações

| Timestamp | Ação | Resultado | Detalhes |
|---|---|---|---|
| 2026-06-11T00:00:00Z | Leitura de 13 módulos Fase 1 (README + tasks.md) | OK | 321 TASKs lidas em 13 módulos |
| 2026-06-11T00:00:00Z | Geração de product-backlog.md | OK | 13 épicos, 82 stories, 321 tasks, 0 bugs |
| 2026-06-11T00:00:00Z | Geração de sprints-planning.md | OK | 15 sprints, 2 semanas cada |
| 2026-06-11T00:00:00Z | Geração de sprint-1-foundation.md até sprint-13-migration-digest-api-reporting.md | OK | 13 arquivos criados (sessão anterior) |
| 2026-06-11T03:00:00Z | Geração de sprint-14-reporting-api-pipeline-domain.md | OK | 46 pts — reporting API + goal-forecast multimoeda + op-pipeline domain |
| 2026-06-11T03:00:00Z | Geração de sprint-15-pipeline-api-hitl-golive.md | OK | 64 pts — op-pipeline infra+API+hardening + HITL#1 go-live (SPF/DKIM/DMARC, LGPD) |
| 2026-06-11T03:00:00Z | Backlog markdown Fase 1 COMPLETO | OK | 13 épicos, 82 stories, 321 tasks, 15 sprints — todos os arquivos criados |
| 2026-06-11T03:00:00Z | Sincronização Jira | PENDENTE | MCP Atlassian não conectado — ver §2 |

## 4. Retomada

Para retomar a execução, leia §1 (estado das sprints), §2 (operações Jira pendentes) e §3 (último checkpoint). Execute as operações de §2 antes de prosseguir com novas ações.
