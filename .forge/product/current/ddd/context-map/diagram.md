# Diagrama do Context Map — Azim CRM

```mermaid
flowchart TB
    subgraph GENERIC["Generic Subdomains"]
        IdP["GCP Identity Platform\n(Externo)"]
        IA["Identity and Access\nBC-12"]
        TB["Tenancy and Branding\nBC-13"]
        ND["Notification Delivery\nBC-14"]
        AL["Audit Log\nBC-15"]
    end

    subgraph SUPPORTING_INFRA["Supporting — Infraestrutura"]
        OM["Organization Management\nBC-08"]
        DM["Data Migration\nBC-09\nFase 1"]
    end

    subgraph CORE_DOMAIN["Core Domain"]
        OP["Opportunity Pipeline\nBC-01"]
    end

    subgraph SUPPORTING_DATA["Supporting — Dados"]
        AM["Account Management\nBC-02"]
        PM["Partner Management\nBC-03"]
        ACT["Activity Management\nBC-04"]
        GF["Goal and Forecast\nBC-05"]
    end

    subgraph SUPPORTING_DELIVERY["Supporting — Entrega de Valor"]
        DG["Digest\nBC-06"]
        REP["Reporting\nBC-07"]
    end

    subgraph SUPPORTING_FUTURE["Supporting — Fases Futuras"]
        WA["Workflow Automation\nBC-10\nFase 2"]
        AII["AI Intelligence\nBC-11\nFase 3"]
    end

    %% Identity
    IdP -->|ACL| IA
    IA -->|Customer/Supplier| OM

    %% Tenancy
    TB -->|Conformist\ntenant_id cross-cutting| OP
    TB -->|Customer/Supplier| OM

    %% Organization cross-cutting
    OM -->|Conformist\ntenant_id bu_id user_id| OP
    OM -->|Conformist| AM
    OM -->|Conformist| PM
    OM -->|Conformist| ACT
    OM -->|Conformist| GF
    OM -->|Conformist| DG
    OM -->|Customer/Supplier| ND

    %% Core upstream
    AM -->|Customer/Supplier\naccount_id| OP
    PM -->|Customer/Supplier\npartner_id pct| OP
    ACT -->|Customer/Supplier\nlast_activity_at| OP

    %% Core downstream
    OP -->|Published Language| AL
    OP -->|Customer/Supplier| DG
    OP -->|Read Model| REP

    %% Goal and Forecast
    OP -->|Customer/Supplier\nrealizado pipeline| GF
    GF -->|Customer/Supplier\nmetas| DG
    GF -->|Read Model| REP

    %% Digest
    ACT -->|Customer/Supplier\natividades vencidas| DG
    DG -->|ACL via IEmailSender| ND
    DG -->|Published Language| AL

    %% Reporting
    PM -->|Read Model| REP
    AM -->|Published Language| AL
    PM -->|Published Language| AL
    ACT -->|Published Language| AL
    GF -->|Published Language| AL

    %% Migration
    DM -->|Customer/Supplier\nbulk write| OP
    DM -->|Customer/Supplier\nbulk write| AM
    DM -->|Customer/Supplier\nbulk write| PM
    OM -->|Conformist\nbu_id stage_id| DM

    %% Future
    OP -->|Customer/Supplier\nevents via PubSub| WA
    WA -->|Customer/Supplier| ACT
    WA -->|Customer/Supplier| ND
    OP -->|Customer/Supplier\nread only| AII
    AM -->|Customer/Supplier\nread only| AII
    ACT -->|Customer/Supplier\nread only| AII
```

---

## Legenda

| Cor / Estilo | Significado |
|---|---|
| Core Domain | Opportunity Pipeline — diferencial competitivo central |
| Supporting — Dados | Account, Partner, Activity, Goal: fornecem dados ao Core |
| Supporting — Entrega | Digest, Reporting: consomem e distribuem dados do Core |
| Supporting — Infraestrutura | Organization, Data Migration: habilitam a operação |
| Supporting — Futuro | Workflow Automation (Fase 2), AI Intelligence (Fase 3) |
| Generic | Identity, Tenancy, Notification, Audit: commodities e infraestrutura |

## Notas sobre o Diagrama

1. **ACL (Anti-Corruption Layer):** representado nas arestas GCP Identity Platform → Identity & Access e Digest → Notification Delivery. A ACL protege o modelo interno de mudanças no contrato externo.

2. **Conformist:** Organization Management é Conformist para todos os contextos que consomem tenant_id, bu_id e user_id. Simplificado no diagrama (mostrado apenas para o Core e contextos chave).

3. **tenant_id cross-cutting:** Tenancy & Branding define o tenant_id que é usado por RLS em todos os contextos — representado como "Conformist cross-cutting" no diagrama, pois todos os BCs aceitam o tenant_id sem tradução (Conformist). Não há contrato publicado como Published Language — apenas o UUID de isolamento.

4. **Fases futuras:** Workflow Automation e AI Intelligence são indicados explicitamente como Fase 2 e Fase 3 para deixar claro que são candidatos e não contratos confirmados.
