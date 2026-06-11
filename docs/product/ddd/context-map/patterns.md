# Padrões Estratégicos Utilizados — Azim CRM

| Padrão DDD | Onde é usado | Justificativa |
|---|---|---|
| Anti-Corruption Layer (ACL) | Identity & Access → GCP Identity Platform | GCP Identity Platform é um sistema externo com contrato próprio e evolutivo; ACL garante que mudanças no IdP não impactam o modelo interno de usuário |
| Anti-Corruption Layer (ACL) | Digest → Notification Delivery | Provider de e-mail (Postmark/SendGrid) pode mudar (LAC-03 pendente); ACL via IEmailSender protege o Digest de qualquer troca de provider |
| Customer/Supplier | Opportunity Pipeline → Account Management | Pipeline depende de contas criadas pelo Account Management; Account é upstream estável |
| Customer/Supplier | Opportunity Pipeline → Partner Management | Pipeline depende de parceiros e percentuais default criados pelo Partner Management |
| Customer/Supplier | Opportunity Pipeline → Activity Management | Pipeline consulta última atividade para detecção de estagnação |
| Customer/Supplier | Digest → Opportunity Pipeline | Digest lê dados de pipeline para compor conteúdo; Pipeline é upstream |
| Customer/Supplier | Digest → Activity Management | Digest lê atividades vencidas e de hoje |
| Customer/Supplier | Digest → Goal & Forecast | Digest inclui bloco de metas no azimute da semana |
| Customer/Supplier | Goal & Forecast → Opportunity Pipeline | Painel de metas precisa de realizado e pipeline; Pipeline é upstream |
| Customer/Supplier | Organization Management → Notification Delivery | Convites de usuário dependem do envio de e-mail |
| Customer/Supplier | Tenancy & Branding → Organization Management | Provisionamento de tenant cria estrutura organizacional inicial |
| Customer/Supplier | Data Migration → Opportunity Pipeline | Import escreve no modelo do Pipeline após conclusão |
| Conformist | Opportunity Pipeline → Organization Management | Pipeline aceita o modelo de BU, usuário e estágios como definido pelo Organization Management |
| Conformist | Todos os BCs → Tenancy & Branding | tenant_id como chave de isolamento RLS é definido pelo Tenancy e aceito por todos |
| Conformist | Digest → Organization Management | Digest usa modelo de usuário, papel e fuso conforme definido pelo Organization Management |
| Published Language | Opportunity Pipeline → Audit Log | Pipeline publica eventos de escrita em formato canônico consumido pelo AuditLog |
| Published Language | Todos os BCs de escrita → Audit Log | Formato AuditEvent(user_id, entity_type, entity_id, action, delta_json, timestamp, tenant_id) é a linguagem publicada |
| Published Language | Digest → KPI (externo) | Eventos de entrega/abertura publicados para métricas KPI-03/KPI-04 |
| Read Model | Reporting → Opportunity Pipeline | Relatórios consomem read models ou views materializadas; sem joins diretos entre schemas de contextos |
| Read Model | Reporting → Partner Management | Relatório de comissão via view derivada |
| Read Model | Reporting → Goal & Forecast | Relatório de forecast via view derivada |

---

## Decisões de Padrão

### Por que ACL para GCP Identity Platform e não Conformist?

O GCP Identity Platform é um sistema externo que pode ser substituído (por KeyCloak, Auth0 ou outro IdP) sem que o modelo interno de usuário precise mudar. A ACL garante essa flexibilidade. Se fossemos Conformist, o modelo interno de User dependeria da representação de claims do GCP IdP — o que criaria acoplamento difícil de remover.

### Por que Customer/Supplier e não Shared Kernel entre Pipeline e outros contextos?

Um Shared Kernel entre Pipeline e Account Management ou Partner Management criaria acoplamento de modelo que tornaria impossível evoluir cada contexto independentemente (ex.: adicionar atributos de conta sem impactar o Pipeline). Customer/Supplier garante que cada contexto tem seu próprio modelo e a dependência é explícita via contrato de API ou evento.

### Por que Conformist para tenant_id?

O tenant_id é definido pelo Tenancy & Branding na criação do tenant. Todos os outros contextos aceitam esse identificador sem negociação — não há razão para ACL aqui porque o modelo de tenant_id é estável e simples (UUID). Conformist é adequado.

### Por que Published Language para AuditLog?

O AuditLog precisa ser consumível por múltiplos contextos sem acoplamento. A linguagem publicada (formato AuditEvent) define o contrato uma vez; qualquer contexto que escreve implementa este formato. Isso garante imutabilidade e padronização sem que o AuditLog precise conhecer os detalhes de cada contexto.
