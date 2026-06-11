# Linguagem Ubíqua — Azim CRM

## Visão Geral

Este documento consolida os termos de domínio por bounded context. Termos com o mesmo nome mas significados distintos em contextos diferentes são listados separadamente. Termos canônicos gerais ficam em `domain-glossary.md`.

---

## Opportunity Pipeline (BC-01 — Core Domain)

| Termo | Definição | Sinônimos / Evitar | Observações |
|---|---|---|---|
| Opportunity | Negociação comercial em andamento, com owner obrigatório, valor, estágio e parceiro opcional | "Lead", "deal", "negócio" | Lead é estágio configurável, não entidade separada (DEC-007) |
| Stage | Fase do funil comercial de uma BU, com probabilidade de fechamento e categoria (aberta/ganha/perdida) | "etapa", "coluna" | Configurável por BU; seed: Lead → Ganho |
| owner_id | Identificador do usuário responsável pela oportunidade — obrigatório, pré-preenchido com o criador | "responsável" (contexto técnico) | Nunca vazio (RN-002) |
| valor_total | Soma calculada: valor_setup + valor_mensal × duracao_meses, em centavos inteiros | "valor", "valor do negócio" | Nunca digitado diretamente (RN-005) |
| valor_setup | Componente único do contrato (implantação), em centavos inteiros | — | Parte do modelo Setup+Recorrente |
| valor_mensal | Componente recorrente mensal do contrato, em centavos inteiros | "mensalidade", "MRR" | Multiplicado por duracao_meses |
| duracao_meses | Duração do contrato em meses; obrigatório quando há valor_mensal | — | |
| forecast_ponderado | valor_total × probabilidade_do_estagio, em centavos inteiros | "forecast", "previsão" | Calculado; exibido em BRL pelo frontend |
| probabilidade | Percentual de probabilidade de fechamento configurado no estágio | — | Ex.: 60% para "Proposta Enviada" |
| OpportunityPartnerCommission | Entidade que registra percentuais e valor calculado de comissão de parceiro por oportunidade | "comissão" | Snapshot imutável ao ganhar (RN-007) |
| snapshot | Registro imutável dos valores de comissão no momento do fechamento como "Ganho" | — | Não editável por nenhum papel (RN-007, RN-022) |
| pct_setup | Percentual de comissão sobre valor_setup | — | Ex.: 10% |
| pct_recorrente | Percentual de comissão sobre valor_mensal × meses_comissionados | — | |
| meses_comissionados | Quantidade de meses sobre os quais incide a comissão recorrente | — | |
| stale (estagnado) | Oportunidade aberta sem atividade registrada nos últimos 14 dias corridos | "parada", "sem movimento" | RN-028; sinalizado no Kanban e no digest |
| motivo_perda | Razão selecionada da lista configurável da BU ao mover para "Perdido" | "motivo de fechamento perdido" | Obrigatório e bloqueante (RN-004) |
| data_fechamento_esperada | Data estimada de fechamento do negócio | "data de fechamento" | Obrigatória a partir de "Proposta Enviada" (RN-003) |
| OpportunityNumber | Número sequencial imutável no formato AZ-NNNN | "número da oportunidade" | Gerado atomicamente; nunca reutilizado (RN-001) |
| kanban | Visão das oportunidades da BU organizada por colunas de estágio com drag-and-drop | "funil visual" | Colunas exibem soma de valor_total e forecast_ponderado |
| origin_channel | Canal de origem da oportunidade (Parceiro, Inbound, Prospecção ativa, etc.) | "canal" | Configurável por BU; "Parceiro" exige partner_id (RN-008) |

---

## Account Management (BC-02)

| Termo | Definição | Sinônimos / Evitar | Observações |
|---|---|---|---|
| Account | Empresa cliente; pode ser vinculada a oportunidades de múltiplas BUs sem duplicação | "cliente", "empresa" | Compartilhada no tenant (não por BU) |
| Contact | Pessoa física vinculada a uma conta (nome, e-mail, celular, cargo) | "contato", "lead de pessoa" | PII protegida por LGPD (RN-025) |
| dedupe | Processo de deduplicação por nome normalizado antes de criar uma conta | "deduplicação" | Alerta, não bloqueio (RN-014) |
| normalized_name | Nome da conta sem acentos, em minúsculas e sem espaços extras, usado para dedupe | — | |
| visao_360 | Visão consolidada da conta: oportunidades de todas as BUs visíveis, contatos, atividades e histórico | "visão 360", "360° da conta" | Filtrada por BUs do usuário |

---

## Partner Management (BC-03)

| Termo | Definição | Sinônimos / Evitar | Observações |
|---|---|---|---|
| Partner | Empresa ou pessoa que origina oportunidades e recebe comissão por componente | "parceiro", "canal de parceiro" | Sem login no MVP (DEC-012, RN-021) |
| partner_type | Classificação do papel do parceiro no processo comercial | "tipo de parceiro" | Configurável pelo tenant |
| comissao_projetada | Soma das comissões calculadas de oportunidades abertas do parceiro (percentuais atuais, sem snapshot) | "comissão em pipeline" | Pode mudar com alteração de percentuais |
| comissao_consolidada | Soma dos snapshots imutáveis de oportunidades Ganhas do parceiro | "comissão realizada" | Baseada em OpportunityPartnerCommission |

---

## Activity Management (BC-04)

| Termo | Definição | Sinônimos / Evitar | Observações |
|---|---|---|---|
| Activity | Ação comercial registrada com tipo, data, responsável e status (pendente/concluída) | "tarefa", "follow-up" | Tipos: call, reunião, e-mail, tarefa |
| overdue | Atividade com data_vencimento passada e status não concluída | "vencida", "atrasada" | Alimenta o digest como pendência |
| to_do | Lista de atividades do usuário: vencidas, de hoje e próximas | "Meu dia", "agenda" | Visão "Meu dia / Minha semana" |
| stagnation | Ausência de atividade em uma oportunidade aberta por 14+ dias corridos | "estagnação" | Critério: RN-028; detectado por Activity Management |

---

## Goal & Forecast (BC-05)

| Termo | Definição | Sinônimos / Evitar | Observações |
|---|---|---|---|
| Goal | Meta mensal de valor comercial por BU ou responsável, em centavos inteiros | "meta", "objetivo" | Granularidade mensal (DEC-003) |
| realizado | Soma do valor_total das oportunidades Ganhas no período | "fechado", "won" | Derivado do Opportunity Pipeline |
| pipeline_disponivel | Soma do forecast_ponderado das oportunidades abertas no período | "pipeline" | Derivado do Opportunity Pipeline |
| graceful_degradation | Comportamento do painel ao exibir dados sem meta cadastrada: mostra realizado e pipeline sem erro | — | RN-017; sem placeholder vazio ou NaN |
| azimute_metas | Bloco do digest de segunda com realizado vs meta e gap análise | — | Omitido sem meta cadastrada (RN-018) |

---

## Digest (BC-06)

| Termo | Definição | Sinônimos / Evitar | Observações |
|---|---|---|---|
| digest | E-mail diário acionável enviado ao usuário com pendências comerciais | "newsletter", "resumo diário" | "O CRM vai ao vendedor" |
| azimute_da_semana | Conteúdo especial do digest de segunda-feira: pipeline por BU/estágio, variação, ganhos, fechamentos, metas | "azimute", "resumo semanal" | Apenas na segunda-feira (RN-029) |
| EmailDigestLog | Registro de envio por (user_id, date) que garante idempotência | — | RN-010; impede reenvio no mesmo dia |
| horario_digest | Hora local de envio do digest configurada no tenant (default 07:00) | — | Por fuso IANA do tenant (DEC-009) |
| pendencia | Atividade vencida, atividade de hoje, opp estagnada ou data de fechamento vencida | "pendência", "to-do" | Critério de inclusão no digest (RN-011) |
| link_autenticado | URL com token incluída no digest para executar ação em 1 clique sem login | "magic link", "1-click link" | Idempotente; expira |

---

## Reporting (BC-07)

| Termo | Definição | Sinônimos / Evitar | Observações |
|---|---|---|---|
| funil | Relatório de distribuição de oportunidades por estágio com valor total | "pipeline report" | |
| ranking | Classificação de responsáveis por valor total e oportunidades ganhas no período | "leaderboard" | Por período e BU |
| canal | Canal de origem da oportunidade para análise de desempenho por canal | "source", "origem" | Ex.: Parceiro, Inbound |
| comissao_projetada | Em relatórios: soma das comissões calculadas de oportunidades abertas | — | Pode variar se percentuais mudarem |
| comissao_consolidada | Em relatórios: soma dos snapshots imutáveis de oportunidades Ganhas | — | Imutável; fonte confiável para pagamento |

---

## Organization Management (BC-08)

| Termo | Definição | Sinônimos / Evitar | Observações |
|---|---|---|---|
| BU | Business Unit — unidade de negócio do tenant com pipeline, estágios e membros próprios | "área", "divisão", "unidade" | Ex.: Vellus, Axis, Vellus Tech |
| papel | Papel RBAC do usuário (Platform Operator, Tenant Admin, Gestor de BU, Vendedor, Viewer) | "perfil", "role" | Usuário pode ter papéis distintos por BU |
| membership | Vínculo entre um usuário e uma BU com papel específico | "membro da BU" | Múltiplos memberships por usuário |
| convite | Token temporário enviado por e-mail para ativação de conta de novo usuário | "invite" | Expiração configurável (RN-030; 72h = inferência) |
| desativacao | Remoção do acesso de um usuário sem exclusão física dos dados | "desligar usuário" | FKs e histórico preservados (RN-013) |

---

## Tenancy & Branding (BC-13)

| Termo | Definição | Sinônimos / Evitar | Observações |
|---|---|---|---|
| tenant | Empresa cliente do Azim como produto SaaS, com isolamento completo de dados | "cliente da plataforma" | Isolado por tenant_id via RLS |
| slug | Identificador único do tenant na URL canônica (app.azim.com.br/{slug}) | "subdomínio", "alias" | Imutável após definição (RN-019) |
| branding | Conjunto de elementos visuais configuráveis: logo, favicon, cor primária, cor secundária | "white-label", "identidade visual" | Estrito: apenas 5 elementos (DEC-004) |
| tenant_id | UUID que identifica o tenant em todas as tabelas (chave de RLS) | "id do tenant" | Cross-cutting a todos os contextos |
| horario_digest | Fuso horário IANA configurado no tenant, utilizado pelo serviço de digest | — | Default BRT (America/Sao_Paulo); DEC-009 |
