# PRD Validation Report

- **Documento validado:** `docs/product/prd/prd.md`
- **Insumos de origem:** `docs/discovery/discovery-notes.md` (primário); `docs/product/azim-product-spec.md` (referência — não foi necessário abrir; discovery e PRD já consolidam seus pontos verificáveis: RPO/RTO, stack, RBAC)
- **Data:** 2026-06-10
- **Status geral:** Validado com ressalvas
- **Modo de execução:** auto-aprovação de correções derivadas dos insumos; achados que exigem decisão humana de produto registrados como pendência (não bloqueantes)

## Veredito

O PRD é fiel ao discovery e à evidência primária da planilha. Não foram encontrados requisitos, personas, decisões ou escopos fabricados sem origem. Não há contradição material com os insumos. Os achados concentram-se em: (a) mis-citações/traços de rastreabilidade, (b) uma inconsistência interna numérica, (c) uma lacuna real flagada inline mas não registrada formalmente, e (d) metas/números de referência (KPIs e NFRs) plausíveis porém não ancorados nos insumos — estes exigem confirmação humana de produto. 5 correções derivadas foram aplicadas; 6 itens permanecem como pendência de decisão.

## Resumo por severidade

| Severidade | Qtd | IDs |
|---|---|---|
| Crítica | 0 | — |
| Alta | 0 | — |
| Média | 5 | P1, P2, P3, P7, P8 |
| Baixa | 6 | P4, P5, P6, P9, P10, P11 |

- **Aplicados (correção derivada):** P1, P2, P3, P4, P5 (5)
- **Pendência de decisão humana:** P6, P7, P8, P9, P10, P11 (6)

---

## Problemas Identificados

- **P1** [APLICADO] [RASTREABILIDADE] Mis-citação de `DEC-010` para a regra `owner_id` obrigatório.
  - **Evidência:** Seção 7, RF-06 (antiga linha ~520): "`owner_id` obrigatório em toda oportunidade — ataca os 59% sem responsável (DEC-010)". No discovery, DEC-010 é estritamente "Vellus é o tenant nº 1 com 3 BUs; import da planilha é requisito obrigatório da Fase 1" — não trata de owner obrigatório. Owner obrigatório não é uma decisão numerada; deriva da evidência primária (64/108 sem dono).
  - **Impacto:** rastreabilidade incorreta; leitor pode buscar em DEC-010 uma fundamentação que não existe ali; enfraquece a confiança nas demais citações de DEC.
  - **Sugestão de correção:** substituir a citação por referência à evidência primária da planilha.
  - **Decisão do usuário:** Aprovado (correção derivada — auto-aprovada)
  - **Status de aplicação:** APLICADO — agora "ataca os 64/108 (59%) sem responsável identificados na planilha `Pipeline Vellus.xlsx` (evidência primária do problema)".

- **P2** [APLICADO] [CONSISTÊNCIA] Inconsistência interna no volume de oportunidades associado à meta de latência do Kanban (KPI-05).
  - **Evidência:** KPI-05 (§11.3) definia "<= 2 s (p95) ... (BU com até 200 oportunidades)", enquanto RISCO-T03 (§10.2) e a matriz de riscos atrelam o mesmo KPI-05 a "load test com 2.000 oportunidades". 200 vs 2.000 para a mesma meta de 2 s.
  - **Impacto:** ambiguidade sobre a condição de carga sob a qual a meta de 2 s deve ser garantida; pode gerar critério de aceite divergente entre QA/engenharia.
  - **Sugestão de correção:** harmonizar KPI-05 para cobrir carga normal e o load test de pico (2.000) coerente com RISCO-T03.
  - **Decisão do usuário:** Aprovado (correção derivada — auto-aprovada)
  - **Status de aplicação:** APLICADO — KPI-05 agora: "<= 2 s (p95) em carga normal; validado também em load test com até 2.000 oportunidades por tenant (consistente com o indicador de RISCO-T03)".

- **P3** [APLICADO] [LACUNA] Lacuna real (base legal LGPD e política de retenção) flagada inline, mas ausente da tabela consolidada de lacunas (§9.3).
  - **Evidência:** §8.4 "Base legal documentada ... (a confirmar com equipe jurídica — LAC implícita)" e §8.8 "(lacuna implícita registrada no `nfrd.md`)" — porém §9.3 (LAC-01..LAC-06) não registrava o item. Discovery §5 confirma exigências LGPD (consentimento, minimização, direito ao esquecimento) a detalhar no NFRD.
  - **Impacto:** lacuna de governança/compliance fica fora do inventário rastreável; risco de não ter responsável/owner explícito.
  - **Sugestão de correção:** registrar como LAC-07 com responsável e impacto, e apontar as menções inline para ela.
  - **Decisão do usuário:** Aprovado (correção derivada — auto-aprovada)
  - **Status de aplicação:** APLICADO — adicionado LAC-07 (Base legal LGPD + retenção; responsável: Jurídico Vellus / Produto); §8.4 e §8.8 agora citam "LAC-07".

- **P4** [APLICADO] [RASTREABILIDADE] Histórico de versão com intervalo malformado e renomeação VAL→LAC não documentada.
  - **Evidência:** linha de histórico v0.1: "DEC-001..013" e "LAC-01..006" (formato malformado); o discovery usa VAL-001..VAL-006, e o PRD os renomeou para LAC-01..LAC-06 sem registrar a correspondência.
  - **Impacto:** rastreabilidade entre discovery (VAL) e PRD (LAC) não explícita; leitor pode achar que são conjuntos distintos.
  - **Sugestão de correção:** corrigir o intervalo e declarar a correspondência LAC↔VAL.
  - **Decisão do usuário:** Aprovado (correção derivada — auto-aprovada)
  - **Status de aplicação:** APLICADO — "13 decisões aprovadas (DEC-001 a DEC-013) e 6 pontos a validar (LAC-01 a LAC-06, correspondentes a VAL-001 a VAL-006 do discovery)".

- **P5** [APLICADO] [ESCOPO] Lista "Won't (nesta versão)" incompleta frente às decisões de escopo.
  - **Evidência:** §7.2 "Won't (nesta versão): billing SaaS, portal do parceiro, multimoeda, app mobile". O DEC-007 (Lead como entidade separada) e o item "múltiplos parceiros por oportunidade" são explicitamente fora de escopo no MVP (§5.2) mas não constavam na linha Won't.
  - **Impacto:** inconsistência menor entre §5.2 (Fora do Escopo) e §7.2 (MoSCoW Won't); leitura parcial pode sugerir que esses itens estão no MVP.
  - **Sugestão de correção:** alinhar a lista Won't com §5.2.
  - **Decisão do usuário:** Aprovado (correção derivada — auto-aprovada)
  - **Status de aplicação:** APLICADO — Won't agora inclui "múltiplos parceiros por oportunidade, Lead como entidade separada (DEC-007)".

- **P6** [PENDENTE] [MÉTRICAS] Metas-alvo de KPIs não ancoradas nos insumos.
  - **Evidência:** alvos não presentes no discovery: KPI-03 entrega >= 98% (§11.2 e OBJ-03), KPI-04 abertura >= 30% (OBJ-03), KPI-07/OBJ-02 cobertura de comissão >= 80%, KPI-08/OBJ-04 WAU gestores >= 80%, KPI-09/KPI-10 DAU >= 80%. O único número de e-mail ancorado é "entregabilidade >= 93,8%" (DEC-008/Postmark, citado em RISCO-T02). Os demais limiares são definições novas do PRD.
  - **Impacto:** metas de sucesso plausíveis, porém são compromissos de produto não derivados dos insumos; se assumidas como fixas sem aval, podem gerar critérios de aceite arbitrários.
  - **Sugestão de correção:** o usuário confirma os alvos (98% / 30% / 80% / 80% / 80%) ou ajusta; marcar explicitamente como "alvo proposto — a ratificar" enquanto não houver decisão.
  - **Decisão do usuário:** Aguardando decisão
  - **Status de aplicação:** PENDENTE (não bloqueante)

- **P7** [PENDENTE] [NFR] Números de referência de disponibilidade e performance sem origem nos insumos.
  - **Evidência:** §8.1 tiers 99,5% / 99,0% / 98,0%; §8.2 latências <= 500 ms, <= 1 s, <= 2 s, <= 3 s; §8.7 estimativas de capacidade (5–20 tenants, 500/2.000 opps, 50/200 RPS). O discovery só fixa RPO <= 5 min e RTO <= 4 h. Os demais são propostos pelo PRD (declarados "referência" / "a validar").
  - **Impacto:** aceitável como alvo de referência num PRD, mas são números inventados; risco se tratados como SLA contratual antes do `nfrd.md`.
  - **Sugestão de correção:** manter como referência e garantir no `nfrd.md` que sejam ratificados via load test/baseline; opcionalmente rotular como "proposto" no PRD.
  - **Decisão do usuário:** Aguardando decisão
  - **Status de aplicação:** PENDENTE (não bloqueante)

- **P8** [PENDENTE] [ESCOPO] Prioridades do roadmap evolutivo (§5.3) não fundamentadas nos insumos.
  - **Evidência:** §5.3 atribui Alta/Média/Baixa (ex.: Portal do parceiro = Alta; Billing = Alta pós-MVP; multimoeda = Média). O discovery trata o horizonte como "não comprometido", sem priorização.
  - **Impacto:** priorização é juízo de produto; pode ser interpretada como compromisso de roadmap não decidido.
  - **Sugestão de correção:** confirmar/ajustar prioridades com o owner ou rotular a coluna como "prioridade indicativa, não comprometida".
  - **Decisão do usuário:** Aguardando decisão
  - **Status de aplicação:** PENDENTE (não bloqueante)

- **P9** [PENDENTE] [DETALHE] Critérios de aceite de US-02 com detalhe técnico/FRD e premissa não-sourced.
  - **Evidência:** §7.3 US-02 cita "validação na API retorna HTTP 422" (detalhe de contrato — pertence ao `frd.md`/`TRD.md`) e "campo owner pré-preenchido com o usuário autenticado" (comportamento não presente no discovery). US-01 também cita "HTTP" implícito e janela "07:00–07:05".
  - **Impacto:** detalhe técnico vaza para o PRD; o pré-preenchimento de owner é uma regra de FRD inferida sem origem.
  - **Sugestão de correção:** mover código HTTP para `frd.md`; manter o critério em nível de produto ("a API rejeita criação sem owner com erro identificável"); marcar pré-preenchimento como "a definir no `frd.md`".
  - **Decisão do usuário:** Aguardando decisão
  - **Status de aplicação:** PENDENTE (não bloqueante — ajuste cosmético; aguardando aval para não sobre-editar user stories)

- **P10** [PENDENTE] [CLAREZA] Generalização "07:00 BRT" vs design multi-tenant por fuso IANA (DEC-009).
  - **Evidência:** §1.4, OBJ-03, §5.1 e matriz RF-09 descrevem o digest como "07:00 BRT" de forma genérica, enquanto RF-09 e J-02 (corretamente) descrevem seleção por hora local IANA por tenant (DEC-009). Para a Fase 1 (somente Vellus, Brasil) "BRT" é exato; para tenants futuros o horário é o local do tenant.
  - **Impacto:** baixo no MVP; risco de leitura de que todos os tenants recebem em BRT.
  - **Sugestão de correção:** manter "07:00 BRT" no contexto Vellus/Fase 1, mas qualificar as menções genéricas como "07:00 no fuso local do tenant (BRT para a Vellus)".
  - **Decisão do usuário:** Aguardando decisão
  - **Status de aplicação:** PENDENTE (não bloqueante)

- **P11** [PENDENTE] [ORGANIZAÇÃO] Numeração de KPIs fora de ordem entre subseções.
  - **Evidência:** §11.1 traz KPI-01, KPI-02, KPI-07; §11.2 traz KPI-03, KPI-04, KPI-08; §11.3 KPI-05, KPI-06; §11.4 KPI-10. Todos os 10 constam na tabela consolidada (§11.5), mas a sequência intercalada dificulta leitura.
  - **Impacto:** cosmético; nenhuma informação faltante ou incorreta.
  - **Sugestão de correção:** opcional — reordenar por código dentro de cada bloco ou aceitar o agrupamento por natureza (produto/operacional/técnica/adoção).
  - **Decisão do usuário:** Aguardando decisão
  - **Status de aplicação:** PENDENTE (não bloqueante)

---

## Verificações sem achados (conformidade confirmada)

- Evidências quantitativas batem com o discovery: 108 opps (71+20+17), R$ 9.013.250 forecast, 64/108 (59%) sem owner, 6/108 (6%) com data, 24 opps / 11 parceiros, AZ-0095.
- KPI-01 "vs 41% atual" é coerente (100% − 59%).
- Estágios seed e probabilidades, canais de origem, modelo de valor Setup + Recorrente, snapshot de comissão em "Ganho", RBAC (5 papéis), defesa em profundidade (EF filter + RLS + CI), DEC-001 a DEC-013 — todos fielmente refletidos.
- Personas P-01..P-05 correspondem às 5 personas do discovery; parceiro corretamente excluído como persona (DEC-012).
- Escopo Fase 0–3 alinhado ao discovery; nível de abstração de PRD respeitado, com encaminhamento explícito a `frd.md`/`nfrd.md`/`TRD.md`/`ADR.md`/`UXD.md`.
- Nenhum requisito, integração, persona ou decisão fabricada sem origem foi identificado.

## Próximo ciclo

Itens P6–P11 aguardam decisão do usuário. Nenhum bloqueia o avanço. O `prd.md` permanece em status `Rascunho para revisão`.

