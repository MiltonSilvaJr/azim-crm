# Análise da planilha "Pipeline Vellus.xlsx" — o processo comercial que o Azim substitui

> **Fonte:** `OneDrive-Vellus/Comercial/Vellus/Pipeline Vellus.xlsx` (modificada em 01/06/2026, ~32 KB, 2 abas).
> **Objetivo:** extrair o modelo de domínio real da operação comercial da Vellus e as dores que o Azim precisa resolver. Os números abaixo foram computados da extração bruta do arquivo.
> **Privacidade:** nomes de contatos/e-mails/celulares presentes na planilha não são reproduzidos aqui — apenas os campos e estatísticas.

---

## 1. Estrutura

| Aba | Conteúdo | Equivalente no Azim |
|---|---|---|
| **Pipeline & Forecast** | 108 linhas de oportunidade, uma tabela única para as 3 BUs | Oportunidades + Contas + Contatos + Forecast |
| **Ações Comerciais** | Tarefas comerciais com data, responsável, prioridade e vínculo manual com o pipeline | Atividades / follow-ups (motor do digest diário) |

A planilha também mantém um "gerador de número de proposta" via fórmula `LARGE(B4:B500,1)+1` (próximo nº: **95**) — numeração sequencial de propostas é prática da casa e deve ser nativa no Azim.

## 2. Dicionário de campos — aba "Pipeline & Forecast"

| Coluna | Campo | Semântica observada | Mapeamento no Azim |
|---|---|---|---|
| B | `#` | Nº sequencial de proposta (43–94; nem toda linha tem) | Numeração automática por tenant (`AZ-0095`), imutável |
| C | `BU` | **Vellus** (71), **Axis** (20), **Vellus Tech** (17) | Entidade **Business Unit** (tenant → N BUs) |
| D | `Empresa / Cliente` | Conta; repete entre linhas e BUs | Entidade **Conta** única por tenant, compartilhada entre BUs |
| E | `Oportunidade / Descrição` | Nome do negócio (ex.: "Oversetter - Switch On-Us", "Payments") | **Oportunidade**.título |
| F | `Parceiro` | Texto livre; 24/108 oportunidades (22%) têm parceiro; 11 parceiros distintos (Montanaro 5, Salto 5, JV Korporate 4, Bus2 3, Finaya, Paytime, Nelson…) | Entidade **Parceiro** + vínculo com comissão na oportunidade |
| G | `Responsável` | Eduardo Haas (13), Milton Silva (16), Montanaro (15); **64 linhas (59%) sem responsável** | **Usuário** dono da oportunidade (obrigatório) |
| H | `Etapa` | **Lead (30), Prospecção (29), Diagnóstico (13), Proposta Enviada (11), Negociação (9), Fechamento Provável (3)**; 13 sem etapa; não há "Ganho"/"Perdido" — fechados somem da planilha | **Estágios de pipeline configuráveis por BU**, com este conjunto como padrão + Ganho/Perdido |
| I, J, K | `Nome`, `E-mail`, `Celular` | Contato da negociação inline na linha (só 27/108 têm) | Entidade **Contato** ligada à Conta, N por oportunidade |
| L | `Valor Potencial Setup (R$)` | Valor one-time (soma: R$ 4.372.000) | Oportunidade.valor_setup |
| M | `Valor Potencial Mensal (R$)` | Receita recorrente mensal (soma: R$ 353.250/mês) | Oportunidade.valor_mensal |
| N | `Meses` | Duração do contrato (12, 36…) | Oportunidade.duração_meses |
| O | `Probabilidade (%)` | Manual: 0,25 / 0,5 / 0,75 / 0,9 (correlacionada à etapa, mas digitada) | Default por estágio, editável por oportunidade |
| P | `Forecast (R$)` | Fórmula **`(Setup×Prob)+((Mensal×Meses)×Prob)`**; total atual: **R$ 9.013.250** | Forecast ponderado calculado pelo sistema |
| Q | `Data Esperada Fechamento` | Serial Excel; **apenas 6/108 preenchidas**; várias no passado (ex.: 20/11/2025, 10/12/2025) | Campo obrigatório a partir de certo estágio + alerta de atraso |
| R | `Status / Próximos Passos` | Texto livre (21/108), mistura status com tarefa ("Buscar agenda…", "Aguardando cliente…") | Vira **Atividade** estruturada com vencimento + histórico/notas |
| S | `Última Atualização` | Serial Excel, manual (13/10/2025 a 09/03/2026) | Automático (audit trail); base para alerta de oportunidade estagnada |

## 3. Dicionário de campos — aba "Ações Comerciais"

| Coluna | Campo | Observado | Mapeamento no Azim |
|---|---|---|---|
| B | `#` | Sequencial 1–5 | id |
| C | `Data Planejada` | Comentário na célula: "quando você precisa executar" | Atividade.data_prevista (motor do digest) |
| D | `Tipo de Ação` | Reunião, Follow-up | Tipo de atividade (Reunião, Follow-up, Ligação, E-mail, Tarefa) |
| E | `Cliente / Oportunidade` | Texto livre | FK real para Conta/Oportunidade |
| F | `Descrição / Detalhes` | Texto | Atividade.descrição |
| G | `Responsável` | Inclui typo "Miilton" (texto livre) | FK Usuário |
| H | `Status` | Pendente, Em Andamento | Pendente / Em andamento / Concluída / Cancelada |
| I | `Data Conclusão` | — | Atividade.concluída_em |
| J | `Prioridade` | "Alta" | Baixa/Média/Alta |
| K | `Relacionada ao Pipeline (#)` | Vínculo manual pelo nº da proposta | FK Oportunidade |
| L | `Observações / Próximos Passos` | Texto | Notas |

## 4. Retrato do pipeline atual (10/06/2026)

- **108 oportunidades** abertas nas 3 BUs; forecast ponderado de **R$ 9,01 mi**.
- Funil concentrado no topo: 59 em Lead/Prospecção (55%), 13 em Diagnóstico, 23 em Proposta/Negociação/Fechamento.
- **Contas com múltiplas oportunidades** são comuns: Autopass (4), Ger7 (3), Crefisa (3), Prospera (3), Dock, Pag.ai, Ethoca, Digio, Passaporte Educação, Condor (2 cada) — Pag.ai e Ethoca aparecem **em duas BUs diferentes** (Vellus e Axis), confirmando que Conta deve ser única no tenant e a oportunidade é que pertence à BU.
- Mistura de tipos de negócio na mesma tabela: venda direta, consultoria, **parcerias** e projetos com parceiro de distribuição.

## 5. Dores que justificam o Azim (evidências)

1. **Sem dono, sem cobrança:** 59% das oportunidades não têm responsável; o digest diário do Azim é impossível nesse modelo.
2. **Forecast frágil:** probabilidade e "última atualização" digitadas à mão; 6 datas de fechamento em 108 oportunidades, várias vencidas (ex.: Dock "Fechamento Provável" com data 10/12/2025, ainda aberta em 2026) — sem campo obrigatório nem alerta de estagnação.
3. **Vínculos quebráveis:** ação comercial liga ao pipeline por número digitado; renumerou, perdeu.
4. **Contato preso na linha:** dados de pessoas (nome/e-mail/celular) coexistem com a oportunidade; sem histórico por conta, sem reaproveitamento entre oportunidades — e sem nenhum controle de acesso (LGPD).
5. **Parceiro sem comissão:** o parceiro é citado por nome, mas não há percentual, base de cálculo nem valor — a comissão hoje vive fora do processo (requisito nº 6 do Azim).
6. **Metas inexistentes:** a aba se chama "Pipeline & Forecast", mas não há meta cadastrada para comparação (requisito nº 7 do Azim — comparação pipeline × metas no e-mail de segunda).
7. **Qualidade de dado:** typos ("Miilton", "Consulroria", "Ondoarding", "Prróximo"), BUs com espaço extra ("Sertão "), 13 linhas sem etapa — sem validação, sem listas controladas.
8. **Concorrência de edição e confiabilidade:** arquivo único no OneDrive, sem trilha de quem mudou o quê (a própria descrição do usuário: "nada profissional e confiável").

## 6. Implicações diretas para a especificação

- O modelo **Setup + Mensal × Meses com forecast ponderado** é a regra de valor nativa do Azim (não um plugin) — preserva a cabeça de cálculo que a equipe já usa.
- **Estágios padrão** semeados da planilha (decisão aprovada: configuráveis por BU) com probabilidade default por estágio.
- **Migração assistida:** o importador lê exatamente este layout (2 abas), normaliza contas duplicadas, converte datas seriais, sinaliza linhas incompletas para triagem manual em vez de importar silenciosamente.
- O **digest diário** nasce da aba "Ações Comerciais": atividades pendentes/vencidas por responsável; o e-mail de segunda agrega o retrato da seção 4 automaticamente (por BU e global, vs metas quando houver).
- **Numeração automática de proposta** por tenant substitui a fórmula `LARGE()+1`.
