# Análise de vídeo — "SaaS Multi-Tenant COMPLETO com Claude Code: Next.js + Postgres + ReactFlow"

> **Fonte:** Helio Arreche – VibeCoding, YouTube, 12/03/2026, 1h10min57s
> <https://www.youtube.com/watch?v=3N8qMP1D5Tc>
> **Por que analisamos:** o vídeo constrói, do zero, um SaaS multi-tenant com workflow builder visual (React Flow), três níveis de permissão e branding por organização — três pilares que o **Azim CRM** também terá. Esta análise extrai o que é aproveitável e o que faremos diferente.

---

## 1. O que o vídeo constrói

Um SaaS de gestão de projetos chamado **TeamForge**, criado inteiramente via Claude Code (sem template/boilerplate), com:

- **Multi-tenancy** com isolamento por `organization_id` + **Row-Level Security (RLS) no PostgreSQL** + resolução de tenant por subdomínio — três empresas rodando na mesma plataforma, cada uma com seu brand.
- **Três níveis de papel** (Owner / Admin / Member) com navegação, cards e permissões distintas por papel — validados com testes manuais de isolamento (criar org do zero, convidar admin com senha provisória, trocar senha, validar que senha antiga é rejeitada).
- **Dashboard + Projetos + Kanban** com drag & drop de tarefas, prioridades e atribuição a membros.
- **Workflow builder visual com React Flow**: nós de **Trigger** (ex.: `task.created`) → **Action** (ex.: log, update priority, update status) → **Notify** (notificação in-app), com execução manual ("Run") e disparo automático por evento. Integrações demonstradas: **Telegram** (via BotFather token) e **webhook**.
- **Notificações in-app** geradas por automações.
- **Stripe** (esqueleto de billing por plano Free/Pro/Enterprise — não finalizado no vídeo).
- **Infra**: VPS Hostinger + Coolify, PostgreSQL 16, Redis, deploy via GitHub + Dockerfile com `prisma migrate deploy` automático no boot do container, firewall (UFW) e fail2ban.
- **Testes E2E** com TestSprite (MCP) ligado ao GitHub para rodar a cada PR.

Stack do vídeo: Next.js 14 App Router + TypeScript, Prisma, PostgreSQL com RLS, Redis, React Flow, Tailwind + shadcn/ui, NextAuth, Stripe, API Anthropic (IA ficou para um segundo vídeo, junto com Resend para e-mail).

## 2. Lições diretamente aplicáveis ao Azim

### 2.1 Arquitetura antes do código
O passo mais valioso do vídeo é o primeiro: **gerar um documento de arquitetura completo antes de qualquer linha de código** (overview, stack, estratégia de multi-tenancy, schema de banco com todas as tabelas/tipos). É exatamente o papel desta especificação do Azim — o autor do vídeo credita a esse documento a qualidade do resultado ("aquela estrutura de arquitetura ele já construiu muito bem estruturada").

### 2.2 Isolamento multi-tenant: pool + RLS
O vídeo valida na prática o modelo **pool (banco compartilhado) com `tenant_id` em todas as tabelas + RLS no PostgreSQL + middleware de resolução de tenant**. Para o Azim:

- Mesmo modelo, com `tenant_id` (empresa cliente) e `business_unit_id` (BU) como chaves de partição lógica.
- Resolução de tenant por **slug prefixo** (requisito do Azim) em vez de subdomínio — mais simples de servir com um único certificado/load balancer; o slug resolve tema + favicon + logo no bootstrap do frontend.
- RLS como **segunda linha de defesa** além do filtro na aplicação (defense in depth) — o vídeo mostra que confiar só no código da aplicação é o erro clássico de quem usa ferramentas low-code.

### 2.3 Papéis e navegação por perfil
Os três níveis (Owner/Admin/Member) com UI adaptada por papel funcionaram bem e o autor destaca que é o tipo de coisa "muito difícil de acertar" em ferramentas low-code. O Azim precisa de RBAC mais rico (papéis por BU, gestor vs vendedor vs parceiro), mas o padrão é o mesmo: **papel define rotas, menus e visibilidade de dados, e isso é testado criando usuários reais de cada nível**.

### 2.4 Redis com três papéis bem definidos
A justificativa do vídeo para Redis num multi-tenant é diretamente transplantável:

1. **Cache de sessão/membership** — validar "usuário pertence ao tenant X" em <1 ms em vez de query no Postgres a cada request.
2. **Rate limiting por plano** — contadores atômicos com TTL (ex.: N chamadas de IA/dia por tenant).
3. **Fila de execução de workflows** — automações disparadas por evento não podem travar o request do usuário; joga na fila e processa assíncrono.

> "Postgres é a fonte de verdade e o Redis é a memória rápida."

No Azim/GCP, o papel 3 (fila) tende a ir para **Pub/Sub ou Cloud Tasks** (gerenciados, IaC-friendly), e o Redis (Memorystore) fica com cache/rate-limit — a deep research confirma essa divisão.

### 2.5 Workflow builder visual como motor de automação (≈ 41:00–53:00, inclui o trecho marcado t=2710s/45:10)
A parte que você marcou no link é a demonstração do workflow em execução. Pontos a absorver:

- Modelo de nós **Trigger → Condições/Actions → Notify**, com painel de configuração por nó, botão **Run** para teste manual e disparo automático por evento de domínio (`task.created` no vídeo; no Azim: `opportunity.stage_changed`, `activity.overdue`, `opportunity.created` etc.).
- **Automação configurável substitui backend hardcoded por integração**: "posso configurar, desconfigurar, agregar mais nós... sem pedir um backend todo só para funcionar o Telegram". Para o Azim, é o argumento para investir cedo num motor de workflow genérico (e-mail, webhook, notificação, atualização de campo) em vez de codificar cada automação.
- A execução do workflow mostra **status por nó** ("evolução completada") — observabilidade da automação é parte do produto.
- Validação de contexto por tipo de trigger (nó Notify avisa que sem `task` no trigger manual não há destinatário) — o builder precisa validar consistência do fluxo.

### 2.6 Operação e deploy
- Migrations **automáticas no boot** do container (`migrate deploy`) eliminaram passo manual — no Azim, mesmo padrão via Cloud Run jobs/startup, orquestrado por Terraform + CI.
- O vídeo usa o assistente de IA como **guia de infraestrutura** (prints → instruções passo a passo, firewall, fail2ban). No Azim isso vira código: 100% Terraform, sem passos manuais — a lição é que tudo que o vídeo fez clicando deve existir como recurso IaC.
- **Testes E2E de isolamento multi-tenant a cada PR** (TestSprite no vídeo) — no Azim, suíte de testes de isolamento (tenant A não vê dados do tenant B) como gate de CI.

## 3. O que o Azim fará diferente (gaps do vídeo)

| Tema | Vídeo (TeamForge) | Azim |
|---|---|---|
| Infra | VPS manual + Coolify (cliques) | GCP 100% Terraform (Cloud Run, Cloud SQL, Memorystore, Pub/Sub, Scheduler) |
| Resolução de tenant | Subdomínio | Slug prefixo no path + branding por tenant (logo, favicon, 2 cores) |
| Backend | Next.js full-stack (Prisma) | C# .NET 10 (API) + funções Python para IA |
| Domínio | Projetos/tarefas | CRM: contas, contatos, oportunidades, pipeline, atividades, metas, comissões de parceiro |
| Hierarquia | Org → membros | Tenant → **múltiplas BUs** → times/membros |
| E-mail | Ficou de fora (Resend citado) | **Digest diário seg–sex** (to-do de follow-ups) + resumo de pipeline vs metas às segundas — requisito de primeira classe |
| IA | Prometida para o vídeo 2 | LangChain/LangGraph/Langfuse planejados desde a arquitetura (funções Python) |
| Billing | Stripe esboçado | Fora do MVP; comissão de **parceiro por oportunidade** é o requisito financeiro real |
| Segurança de auth | NextAuth + senha provisória | Identity Platform/OIDC, convites com fluxo de ativação |

## 4. Riscos que o vídeo expõe (e o Azim deve mitigar)

1. **Permissões "aparentemente funcionando"** — no vídeo, os três papéis viam o mesmo conteúdo até ser corrigido. Mitigação: matriz de permissões na especificação + testes automatizados por papel desde o primeiro sprint.
2. **Convite de usuário depende de e-mail** — o cadastro de admin falhou porque não havia provedor de e-mail configurado; a solução improvisada (senha provisória) é aceitável em dev, não em produção. Mitigação: e-mail transacional é dependência de fundação no Azim (já é requisito do digest), provisionado via Terraform desde o ambiente dev.
3. **Segredos manuseados manualmente** (URLs de banco em bloco de notas, token do Telegram colado em campo) — no Azim: Secret Manager + variáveis injetadas por Terraform, nunca segredo em tela/repositório.
4. **Workflow sem fila** trava request — o vídeo mesmo aponta; no Azim, execução de automação é sempre assíncrona (Pub/Sub → worker).

## 5. Conclusão

O vídeo confirma a viabilidade do padrão arquitetural que o Azim adota — pool multi-tenant com RLS, RBAC por papel, automação visual com React Flow, cache/fila fora do banco — e mostra que a qualidade do resultado vem do **documento de arquitetura escrito antes do código**. Os gaps (infra manual, e-mail ausente, billing incompleto, sem BUs nem domínio comercial) são exatamente os pontos onde a especificação do Azim precisa ser mais rigorosa que o material de referência.
