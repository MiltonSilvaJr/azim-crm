# Relações entre Bounded Contexts — Azim CRM

| Origem | Destino | Tipo de Relação | Padrão DDD | Contrato | Observações |
|---|---|---|---|---|---|
| Opportunity Pipeline | Account Management | Consome dados de conta | Customer/Supplier | account_id, nome, contatos | Pipeline (Customer) depende de Account (Supplier) |
| Opportunity Pipeline | Partner Management | Consome dados de parceiro | Customer/Supplier | partner_id, pct_setup, pct_recorrente | Pipeline usa percentuais default ao vincular parceiro |
| Opportunity Pipeline | Activity Management | Consulta última atividade para stale | Customer/Supplier | opportunity_id → last_activity_at | Pipeline detecta estagnação com dados de Activity |
| Opportunity Pipeline | Organization Management | Consome BUs, estágios, usuários e RBAC | Conformist | tenant_id, bu_id, user_id, stage_id | Pipeline não influencia o modelo de Organization |
| Opportunity Pipeline | Tenancy & Branding | Usa tenant_id como chave de isolamento | Conformist | tenant_id | Cross-cutting RLS |
| Opportunity Pipeline | Audit Log | Publica eventos de escrita | Published Language | AuditEvent(entity_type, entity_id, action, delta_json) | Append-only; imutável |
| Digest | Opportunity Pipeline | Lê opps estagnadas e fechamentos esperados | Customer/Supplier | opportunity.stale, opportunity.closing_soon | Digest (Customer) consome dados do Pipeline (Supplier) |
| Digest | Activity Management | Lê atividades vencidas e de hoje | Customer/Supplier | activity.overdue, activity.today | Digest compõe pendências do usuário |
| Digest | Goal & Forecast | Lê bloco de metas para azimute | Customer/Supplier | goal{period, bu_id} → valor_meta, realizado | Omitido quando não há meta (RN-018) |
| Digest | Organization Management | Lê usuários, papéis e fuso do tenant | Conformist | users, roles, tenant.iana_timezone | Filtragem de destinatários por papel e fuso |
| Digest | Notification Delivery | Envia e-mail via IEmailSender | Anti-Corruption Layer | IEmailSender.Send(to, subject, html, branding) | ACL protege Digest da API do provider |
| Digest | Audit Log | Registra EmailDigestLog | Published Language | DigestSentEvent(user_id, date, status) | |
| Reporting | Opportunity Pipeline | Lê oportunidades via read model | Read Model | opp_read_model(tenant_id, bu_id, stage, period) | Sem joins diretos; view ou materialized view |
| Reporting | Partner Management | Lê dados de parceiro para relatório de comissão | Read Model | partner_commission_view | |
| Reporting | Goal & Forecast | Lê metas para forecast | Read Model | goal_period_view | |
| Reporting | Organization Management | Filtra por RBAC | Conformist | tenant_id, bu_id, user_id | |
| Account Management | Organization Management | Valida tenant_id e escopo de visibilidade | Conformist | tenant_id | Conta visível por tenant, não por BU |
| Account Management | Audit Log | Publica eventos de escrita | Published Language | AuditEvent | |
| Partner Management | Organization Management | Valida tenant_id e bu_id | Conformist | tenant_id, bu_id | |
| Partner Management | Audit Log | Publica eventos de escrita | Published Language | AuditEvent | |
| Activity Management | Organization Management | Valida tenant_id, bu_id e user_id | Conformist | tenant_id, bu_id, owner_id | |
| Activity Management | Audit Log | Publica eventos de escrita | Published Language | AuditEvent | |
| Goal & Forecast | Organization Management | Valida tenant_id e bu_id | Conformist | tenant_id, bu_id | |
| Goal & Forecast | Opportunity Pipeline | Lê realizado e pipeline para painel comparativo | Customer/Supplier | won_total, available_pipeline por período | |
| Goal & Forecast | Audit Log | Publica eventos de escrita | Published Language | AuditEvent | |
| Organization Management | Identity & Access | Valida token de sessão; carrega usuário | Anti-Corruption Layer | session_token → user_id, tenant_id | ACL protege modelo interno do GCP IdP |
| Organization Management | Notification Delivery | Envia e-mails de convite | Customer/Supplier | IEmailSender.Send(invite_email) | |
| Organization Management | Audit Log | Publica eventos de escrita | Published Language | AuditEvent | |
| Identity & Access | GCP Identity Platform | Autentica usuário por e-mail/senha ou Google | Anti-Corruption Layer | OAuth2/OIDC | ACL garante que mudanças do GCP não impactam modelo interno |
| Tenancy & Branding | Identity & Access | Cria tenant de identidade no GCP | Customer/Supplier | tenant_slug → identity_tenant_id | |
| Tenancy & Branding | Organization Management | Downstream — fornece tenant_id para criação de BUs | Customer/Supplier | tenant_id | |
| Data Migration | Opportunity Pipeline | Escreve oportunidades após import | Customer/Supplier | CreateOpportunity (bulk) | Após import bem-sucedido |
| Data Migration | Account Management | Escreve contas após import | Customer/Supplier | CreateAccount (bulk) | |
| Data Migration | Partner Management | Escreve parceiros após import | Customer/Supplier | CreatePartner (bulk) | |
| Data Migration | Organization Management | Lê BUs e estágios antes do import | Conformist | bu_id, stage_id | DEP-04 |
| Workflow Automation | Opportunity Pipeline | Consome eventos de domínio como triggers | Customer/Supplier | opportunity.* events via Pub/Sub | Fase 2; DDD-VAL-05 |
| Workflow Automation | Activity Management | Cria atividades como ação | Customer/Supplier | CreateActivity | Fase 2 |
| Workflow Automation | Notification Delivery | Envia e-mails como ação | Customer/Supplier | IEmailSender | Fase 2 |
| AI Intelligence | Opportunity Pipeline | Lê dados para scoring | Customer/Supplier | opp_data_for_scoring | Fase 3; sem escrita |
| AI Intelligence | Account Management | Lê dados de conta para briefing | Customer/Supplier | account_360_data | Fase 3 |
| AI Intelligence | Activity Management | Lê histórico de atividades | Customer/Supplier | activity_history | Fase 3 |
