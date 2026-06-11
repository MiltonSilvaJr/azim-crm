# TA — Tenant Administration
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § RF-02 (Administração do Tenant — white-label estrito) e docs/product/modules/tenant-administration/README.md

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento a partir do README do módulo, FRD (RF-02), NFRD (NFR-SEG/USA/INT), TRD e Data Model |

## 1. Visão Geral

O módulo **tenant-administration** (Bounded Context BC-13 — *Tenancy & Branding*) é responsável pelo ciclo de vida completo de um **tenant** (empresa cliente do Azim CRM como produto SaaS) e por sua identidade visual *white-label*. É um *Generic Subdomain* candidato ao deployable `azim-api`, porém classificado como **Tier 1**: o `tenant_id` é *cross-cutting* a todos os módulos e constitui a chave de isolamento *Row-Level Security* (RLS — segurança em nível de linha) de toda a plataforma.

O módulo cobre: provisionamento inicial de tenant pelo *Platform Operator* (operador da plataforma); definição de `slug` único e imutável; configuração de fuso horário IANA (*Internet Assigned Numbers Authority*) e horário do digest; gestão do estado do tenant (provisionado, suspenso, reativado); e configuração de branding *white-label* estrito (logo, favicon, cor primária e cor secundária) com validação de contraste WCAG (*Web Content Accessibility Guidelines*) 2.1 nível AA.

Este documento define **o que** o módulo deve fazer. Decisões de implementação (nomes de tabelas, bibliotecas, endpoints detalhados, algoritmos de cálculo de contraste, estratégia de cache) pertencem ao `design.md` e a documentos técnicos correlatos.

## 2. Escopo

### 2.1 Incluído

- Provisionamento de novo tenant pelo *Platform Operator* como rotina de aplicação (não como rotina de infraestrutura/Terraform).
- Definição de `slug` único globalmente e imutável após a criação, em minúsculas e hífens.
- Definição de nome de exibição (`display_name`) do tenant.
- Configuração de fuso horário IANA e de horário local do digest (padrão 07:00).
- Gestão de estado do tenant: provisionado, suspenso e reativado pelo *Platform Operator*.
- Criação do tenant de identidade correspondente no provedor de identidade externo, de forma atômica em relação à criação do tenant na plataforma.
- Configuração de branding *white-label* estrito: logo, favicon, cor primária e cor secundária.
- Validação de formato e tamanho de logo (PNG ou SVG, até 1 MB) e de favicon.
- Validação de contraste WCAG 2.1 AA das cores no momento do upload.
- Derivação automática de tons a partir das cores primária e secundária (sem entrada manual de tons adicionais).
- Exposição das configurações de branding para o frontend (`azim-web`), de forma cacheável.
- Garantia de isolamento estrito por tenant via `tenant_id` (chave de RLS) para os dados próprios do módulo.
- Publicação dos eventos de domínio `TenantProvisioned` e `BrandingChanged`.

### 2.2 Excluído

- Autenticação e gestão de sessão de usuários (pertence ao módulo `authentication`).
- Criação de *Business Units* (BUs) e convite de usuários (pertence ao módulo `organization`).
- Gestão de planos, cobrança e faturamento do Azim como SaaS.
- CSS, fontes ou layout customizável por tenant — o branding é **estrito** (apenas os quatro elementos visuais, sobre o `slug`).

### 2.3 Fora do escopo do MVP

Consolidado na seção 9.

## 3. Personas / Atores

| Persona | Código | Tipo | Papel no módulo |
|---|---|---|---|
| Platform Operator (Operador da Plataforma) | P-05 / PlatOp | Ator operacional | Provisiona, suspende e reativa tenants; não acessa dados comerciais dos tenants |
| Tenant Admin (Administrador do Tenant) | P-04 / TAdmin | Usuário de configuração | Configura branding, fuso horário e horário do digest do próprio tenant |
| azim-web | — | Sistema consumidor | Consome as configurações de branding para aplicar *design tokens* (variáveis CSS) no frontend |
| Provedor de identidade externo | — | Sistema externo | Recebe a criação do tenant de identidade correspondente ao `slug` |
| Módulo `organization` | — | Sistema consumidor | Consome o evento `TenantProvisioned` para criar a BU inicial |

## 4. Lista canônica de estados do tenant

| Estado | Descrição | Transição de entrada | Ator |
|---|---|---|---|
| `provisioned` (ativo) | Tenant criado e operacional; acesso liberado | Provisionamento bem-sucedido | Platform Operator |
| `suspended` (suspenso) | Tenant temporariamente impedido de operar; dados preservados | Suspensão a partir de `provisioned` | Platform Operator |
| `provisioned` (reativado) | Tenant reativado a partir de `suspended` | Reativação a partir de `suspended` | Platform Operator |

Transições válidas:

- (inexistente) → `provisioned` — via provisionamento.
- `provisioned` → `suspended` — via suspensão.
- `suspended` → `provisioned` — via reativação.

Transições inválidas (devem ser rejeitadas): qualquer transição que não conste acima, incluindo provisionar um tenant já existente com o mesmo `slug`, suspender um tenant já suspenso ou reativar um tenant já ativo.

> Observação: no MVP não há exclusão física de tenant. A remoção definitiva de dados está fora do escopo do MVP (ver seção 9).

## 5. Requisitos Funcionais

### Req 1 — Provisionar novo tenant

**Como** Platform Operator **quero** provisionar um novo tenant informando `slug`, nome de exibição e fuso horário **para** disponibilizar a plataforma a um novo cliente SaaS.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § RF-02 (FRD-admin-02); README § 4; PRD ACT-05 |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 1.1 O provisionamento aceita, no mínimo, `slug`, nome de exibição (`display_name`) e fuso horário IANA, e é restrito ao papel Platform Operator.
- 1.2 Provisionamento bem-sucedido cria o tenant no estado `provisioned` e atribui um `tenant_id` (identificador único e imutável).
- 1.3 O provisionamento exige confirmação explícita do `slug` antes de efetivar, dado que o `slug` é imutável (RISK-TENANT-01).
- 1.4 Ao concluir, o evento `TenantProvisioned` é publicado contendo, no mínimo, `tenant_id` e `slug`.
- 1.5 O provisionamento é executado como rotina de aplicação, não como rotina de infraestrutura/Terraform.
- 1.6 Tentativa de provisionar com `slug` já existente é rejeitada com mensagem orientando a escolha de `slug` alternativo, sem criar o tenant.

**Cross-ref:** Req 2, Req 3, Req 11, Req 12; RNF 3; FRD-admin-02; README RISK-TENANT-01

### Req 2 — Slug único global e imutável

**Como** Platform Operator **quero** que o `slug` do tenant seja único globalmente e imutável após a criação **para** garantir a estabilidade das URLs canônicas e do tenant de identidade.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § RF-02 (RN-019); README § 4 (slug); Linguagem Ubíqua BC-13 |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 2.1 O `slug` é único em toda a plataforma (unicidade global, não por tenant).
- 2.2 O `slug` aceita apenas letras minúsculas e hífens; qualquer outro caractere é rejeitado com mensagem informando o formato aceito.
- 2.3 Após a definição, o `slug` não pode ser alterado por nenhuma interface ou API; toda tentativa de alteração é bloqueada.
- 2.4 O `slug` resolve corretamente o tenant na URL canônica (`app.azim.com.br/{slug}`).
- 2.5 Não existe operação de plataforma de alteração de `slug` no MVP.

**Cross-ref:** Req 1; PBT-01, PBT-02; RN-019; FRD-admin-02

### Req 3 — Configurar fuso horário IANA e horário do digest

**Como** Tenant Admin **quero** configurar o fuso horário IANA e o horário local do digest **para** que o digest diário seja entregue no horário correto do meu tenant.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § RF-02 (FRD-admin-02); README § 4; Linguagem Ubíqua BC-06/BC-13 (DEC-009) |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 3.1 O fuso horário é um identificador IANA válido (ex.: `America/Sao_Paulo`); valores não reconhecidos são rejeitados.
- 3.2 O fuso horário padrão na criação é `America/Sao_Paulo` (BRT).
- 3.3 O horário do digest é um horário local no fuso do tenant, com padrão 07:00.
- 3.4 As configurações de fuso e horário do digest são editáveis pelo Tenant Admin após o provisionamento.
- 3.5 O fuso horário configurado é a fonte usada pelo módulo de digest para selecionar destinatários e momento de entrega (consumo *downstream*).

**Cross-ref:** Req 4 de outro módulo (digest — seleção de destinatários por fuso); DEC-009; Linguagem Ubíqua `horario_digest`, `digest_time`

### Req 4 — Gerir estado do tenant (suspensão e reativação)

**Como** Platform Operator **quero** suspender e reativar um tenant **para** controlar o acesso à plataforma sem excluir dados.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | README § 4 (lista canônica de estados); decisão arquitetural (estado `active` em tenants) |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 4.1 A suspensão e a reativação são restritas ao papel Platform Operator.
- 4.2 Suspender um tenant transita seu estado de `provisioned` para `suspended` sem exclusão física de dados.
- 4.3 Reativar um tenant transita seu estado de `suspended` para `provisioned`.
- 4.4 Transições inválidas (ex.: suspender tenant já suspenso, reativar tenant já ativo) são rejeitadas conforme a lista canônica da seção 4.
- 4.5 Um tenant suspenso não permite operação por seus usuários enquanto permanecer nesse estado.

**Cross-ref:** Seção 4 (lista canônica de estados); PBT-08

### Req 5 — Configurar branding white-label estrito

**Como** Tenant Admin **quero** configurar a identidade visual do meu tenant dentro dos limites do white-label estrito **para** apresentar a marca do tenant sem expor a infraestrutura compartilhada.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § RF-02 (FRD-admin-01, RN-019); DEC-004; README § 4 |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 5.1 Os únicos elementos configuráveis são: logo, favicon, cor primária e cor secundária — sobre o `slug` já definido.
- 5.2 Não há configuração de CSS, fontes ou layout por tenant; tentativas de configurar elementos fora da lista são rejeitadas.
- 5.3 As cores primária e secundária são informadas em formato hexadecimal `#RRGGBB`.
- 5.4 Ao salvar o branding com sucesso, o evento `BrandingChanged` é publicado.
- 5.5 O branding atualizado é refletido no portal em menos de 30 segundos após salvar e é aplicado no digest seguinte ao da configuração.

**Cross-ref:** Req 6, Req 7, Req 8, Req 9, Req 11; PBT-03; RN-019; DEC-004; FRD-admin-01

### Req 6 — Validar contraste WCAG AA no upload de cores

**Como** Tenant Admin **quero** que o sistema valide o contraste das cores no momento do upload **para** garantir acessibilidade WCAG 2.1 AA da interface do tenant.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § RF-02 (RN-020); NFRD NFR-USA-02, NFR-COMP-02; DEC-004 |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 6.1 No upload de cor primária e secundária, o sistema calcula a razão de contraste e a compara com o mínimo WCAG 2.1 AA (4,5:1 para texto normal; 3:1 para texto grande).
- 6.2 Combinação com razão de contraste insuficiente para texto normal é sinalizada com o valor de contraste calculado exibido ao usuário.
- 6.3 O resultado da validação é registrado de forma persistente como indicador de conformidade (`wcag_contrast_ok`).
- 6.4 Valores limítrofes são tratados de forma consistente: 4,4:1 reprovado; 4,5:1 aprovado; 4,6:1 aprovado.
- 6.5 A validação ocorre antes de a configuração ser considerada conforme; o tenant não fica marcado como conforme com cores fora do limite.

**Cross-ref:** Req 5; PBT-04; RN-020; NFR-USA-02; NFR-COMP-02; RNF 2

### Req 7 — Restringir formato e tamanho do logo e do favicon

**Como** Tenant Admin **quero** enviar apenas logos e favicons em formatos e tamanhos permitidos **para** preservar a integridade visual e operacional da plataforma.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § RF-02 (RN-020); README § 4 |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 7.1 O logo é aceito apenas nos formatos PNG ou SVG.
- 7.2 O logo é aceito apenas com tamanho de até 1 MB; upload acima do limite é rejeitado sem salvar.
- 7.3 Upload de arquivo com formato inválido é rejeitado com mensagem informando os formatos aceitos.
- 7.4 O favicon é aceito conforme as restrições de formato definidas para favicon e validado antes do armazenamento.
- 7.5 Rejeições de upload não alteram o branding previamente válido.

**Cross-ref:** Req 5; RN-020; FRD-admin-01

### Req 8 — Derivar tons automaticamente a partir das cores base

**Como** Tenant Admin **quero** que os tons derivados sejam gerados automaticamente a partir das cores primária e secundária **para** manter o white-label estrito sem entrada manual de paleta.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | DEC-004 (white-label estrito); README § 4 |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 8.1 Os tons derivados (variações de cor para estados de interface) são gerados automaticamente a partir das cores primária e secundária.
- 8.2 Não há entrada manual de tons adicionais além da cor primária e da cor secundária.
- 8.3 A derivação é determinística: as mesmas cores base produzem sempre os mesmos tons derivados.

**Cross-ref:** Req 5; PBT-05; DEC-004

### Req 9 — Expor branding para o frontend de forma cacheável

**Como** sistema azim-web **quero** obter as configurações de branding do tenant de forma cacheável **para** aplicar os *design tokens* (variáveis CSS) sem comprometer a performance.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | TRD § RF-02; README § 9 (GET branding); Data Model (tenant_brandings → azim-web) |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 9.1 O módulo expõe consulta de branding (logo, favicon, cor primária, cor secundária) para o frontend.
- 9.2 A resposta de branding contém apenas os elementos do white-label estrito, sem dados sensíveis do tenant.
- 9.3 Após `BrandingChanged`, a configuração exposta reflete o novo branding em menos de 30 segundos (coerente com Req 5.5), respeitando a estratégia de cache/invalidations.

**Cross-ref:** Req 5; evento `BrandingChanged`; TRD `tenant.branding_changed.v1`

### Req 10 — Isolamento estrito por tenant

**Como** plataforma **quero** que os dados próprios do módulo sejam isolados por `tenant_id` **para** impedir vazamento de dados entre tenants.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | NFRD NFR-SEG-01, NFR-MAN-02; DEC-006; README § 2 (Tier 1, RLS) |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 10.1 Toda consulta a dados próprios de configuração e branding é restrita ao `tenant_id` do contexto autenticado.
- 10.2 Uma requisição em contexto do tenant A nunca retorna configurações ou branding do tenant B.
- 10.3 O `tenant_id` é a chave de isolamento *cross-cutting* exposta aos demais módulos como base do RLS.
- 10.4 O isolamento é verificado por testes de isolamento de tenant como gate obrigatório de CI (integração contínua).

**Cross-ref:** PBT-06; RNF 1; NFR-SEG-01; NFR-MAN-02

### Req 11 — Publicar eventos de domínio do tenant

**Como** plataforma **quero** publicar eventos de domínio de provisionamento e de branding **para** que módulos consumidores reajam de forma desacoplada.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README § 10; TRD (`tenant.branding_changed.v1`) |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 11.1 O evento `TenantProvisioned` é publicado após provisionamento bem-sucedido, com `tenant_id` e `slug`.
- 11.2 O evento `BrandingChanged` é publicado após atualização de branding bem-sucedida.
- 11.3 Os eventos são consumíveis pelo módulo `organization` (criação de BU inicial) e pelo módulo de auditoria.
- 11.4 Falha na publicação do evento não deixa o tenant em estado inconsistente com o que foi persistido.

**Cross-ref:** Req 1, Req 5; README § 10; TRD § Eventos

### Req 12 — Atomicidade do provisionamento com o provedor de identidade

**Como** Platform Operator **quero** que a criação do tenant na plataforma e do tenant de identidade no provedor externo seja atômica **para** evitar estado inconsistente em caso de falha.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README RISK-TENANT-02; FRD § RF-02; decisão arquitetural (compensação) |
| **Módulo** | tenant-administration |

**Critérios de Aceite:**

- 12.1 O provisionamento cria o tenant de identidade correspondente ao `slug` no provedor de identidade externo.
- 12.2 Se a criação no provedor de identidade falhar, a criação do tenant na plataforma é desfeita (compensação/rollback), não deixando tenant órfão.
- 12.3 Se a persistência do tenant na plataforma falhar após a criação no provedor, o procedimento garante consistência por compensação.
- 12.4 Ao final, ou ambos os recursos existem, ou nenhum dos dois existe.

**Cross-ref:** Req 1; PBT-07; README RISK-TENANT-02

## 6. Requisitos Não-Funcionais

### RNF 1 — Isolamento multi-tenant com defesa em profundidade

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFRD NFR-SEG-01, NFR-MAN-02; DEC-006 |
| **Módulo** | tenant-administration |

**Descrição:**

O isolamento dos dados próprios do módulo entre tenants deve ser garantido por camadas independentes (filtro de consulta na aplicação e RLS no banco), de modo que a falha de uma camada não resulte em vazamento. Vazamento entre tenants é incidente severidade 1.

**Critérios de Aceite:**

- RNF-1.1 A suite de testes de isolamento de tenant é gate obrigatório de CI, com 100% de aprovação por build.
- RNF-1.2 Requisição em contexto do tenant A não retorna dados do tenant B via API; acesso direto ao banco sem contexto de tenant retorna vazio ou erro.
- RNF-1.3 Mudanças no middleware de resolução de tenant exigem revisão de código obrigatória.

**Cross-ref:** Req 10; NFR-SEG-01; NFR-MAN-02; PBT-06

### RNF 2 — Validação de contraste WCAG 2.1 AA das cores do tenant

| Campo | Valor |
|-------|-------|
| **Categoria** | Acessibilidade |
| **Prioridade** | Must |
| **Origem** | NFRD NFR-USA-02, NFR-COMP-02; FRD RN-020; DEC-004 |
| **Módulo** | tenant-administration |

**Descrição:**

O upload de cores primária e secundária deve validar automaticamente a razão de contraste contra o mínimo WCAG 2.1 AA antes de marcar o branding como conforme, garantindo que a interface dinâmica do tenant permaneça acessível.

**Critérios de Aceite:**

- RNF-2.1 Razão de contraste mínima de 4,5:1 para texto normal e 3:1 para texto grande é aplicada na validação.
- RNF-2.2 Valores limítrofes são determinísticos: 4,4:1 reprovado; 4,5:1 aprovado; 4,6:1 aprovado.
- RNF-2.3 O indicador de conformidade (`wcag_contrast_ok`) reflete o resultado da última validação.

**Cross-ref:** Req 6; NFR-USA-02; NFR-COMP-02; PBT-04

### RNF 3 — Provisionamento como rotina de aplicação de baixa frequência

| Campo | Valor |
|-------|-------|
| **Categoria** | Operabilidade |
| **Prioridade** | Should |
| **Origem** | NFRD NFR-ESC-01; README § 3 e § 5; FRD RF-02 |
| **Módulo** | tenant-administration |

**Descrição:**

O provisionamento de tenant é uma rotina de aplicação executada pelo Platform Operator, e não uma rotina de infraestrutura/Terraform. Por ser operação de baixa frequência, não há SLO de latência crítica, mas o onboarding deve ser concluído dentro de uma janela operacional previsível.

**Critérios de Aceite:**

- RNF-3.1 O onboarding de um novo tenant é concluído em até 30 minutos por processo automatizado da aplicação.
- RNF-3.2 O provisionamento não depende de execução de Terraform para criar o tenant.
- RNF-3.3 A arquitetura suporta crescimento até pelo menos 50 tenants ativos sem retrabalho estrutural.

**Cross-ref:** Req 1; NFR-ESC-01

### RNF 4 — Propagação rápida do branding ao frontend

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Should |
| **Origem** | FRD § RF-02 (FRD-admin-01, critério de aceite < 30 s) |
| **Módulo** | tenant-administration |

**Descrição:**

A atualização de branding deve refletir no portal de forma percebida como imediata pelo usuário, respeitando a estratégia de cache e invalidação.

**Critérios de Aceite:**

- RNF-4.1 O branding atualizado é refletido no portal em menos de 30 segundos após salvar.
- RNF-4.2 O branding é aplicado no digest seguinte ao da configuração.

**Cross-ref:** Req 5; Req 9

### RNF 5 — Auditoria imutável de provisionamento e branding

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Must |
| **Origem** | NFRD NFR-AUD-01; README § 17; `.forge/rules/domain/audit-immutability.md` |
| **Módulo** | tenant-administration |

**Descrição:**

Os eventos `TenantProvisioned` e `BrandingChanged` devem gerar entrada de auditoria *append-only* (somente adição), preservando autor, momento e `tenant_id`, sem possibilidade de alteração posterior.

**Critérios de Aceite:**

- RNF-5.1 Provisionamento de tenant gera entrada de auditoria imutável com `tenant_id`, `slug` e ator.
- RNF-5.2 Alteração de branding gera entrada de auditoria imutável com `tenant_id` e ator.
- RNF-5.3 Entradas de auditoria não podem ser editadas ou removidas após a gravação.

**Cross-ref:** Req 11; NFR-AUD-01; `.forge/rules/domain/audit-immutability.md`

### RNF 6 — Observabilidade do provisionamento

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Must |
| **Origem** | NFRD NFR-OBS-01; README § 17; `.forge/rules/architecture/observability.md` |
| **Módulo** | tenant-administration |

**Descrição:**

As operações do módulo devem emitir logs estruturados com `tenant_id`, `slug`, identificador de correlação (`correlationId`) e resultado, permitindo rastreabilidade da plataforma. Falha no provisionamento de tenant é evento crítico de plataforma e deve gerar alerta.

**Critérios de Aceite:**

- RNF-6.1 Log de provisionamento contém `correlationId`, `slug`, `tenant_id` e resultado.
- RNF-6.2 Falha no provisionamento de tenant dispara alerta operacional.
- RNF-6.3 Métricas de provisionamento e de atualização de branding são expostas para monitoramento.

**Cross-ref:** Req 1; Req 5; NFR-OBS-01; `.forge/rules/architecture/observability.md`

### RNF 7 — Bloqueio de acesso do Platform Operator a dados comerciais

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFRD NFR-PRIV-05; PRD § 8.3/§ 8.4 (P-05) |
| **Módulo** | tenant-administration |

**Descrição:**

O Platform Operator provisiona e administra tenants, mas deve ser bloqueado por padrão de acessar dados comerciais dos tenants (oportunidades, contatos, atividades, metas, comissões). Acesso de suporte excepcional deve ser autorizado e auditado.

**Critérios de Aceite:**

- RNF-7.1 Requisições do Platform Operator a dados comerciais de qualquer tenant são bloqueadas por padrão.
- RNF-7.2 Qualquer acesso de suporte autorizado gera entrada de auditoria.

**Cross-ref:** RNF 5; NFR-PRIV-05

## 7. Property-Based Testing

### PBT-01 — Imutabilidade e unicidade global do slug

**Mapeia para:** Req 2, Req 1
**Tipo:** State machine

**Propriedade:**

> Para qualquer tenant provisionado e qualquer sequência de tentativas de alteração de `slug`, o `slug` permanece igual ao definido na criação; e para qualquer par de tenants distintos, seus `slug` são diferentes.

### PBT-02 — Normalização e formato do slug

**Mapeia para:** Req 2
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer `slug` aceito pelo sistema, ele contém apenas letras minúsculas e hífens; qualquer `slug` gerado com outros caracteres é rejeitado.

### PBT-03 — White-label estrito (apenas quatro elementos)

**Mapeia para:** Req 5
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer configuração de branding aceita, o conjunto de elementos persistidos é subconjunto de { logo, favicon, cor primária, cor secundária }; qualquer elemento fora desse conjunto é rejeitado.

### PBT-04 — Monotonicidade da validação de contraste WCAG AA

**Mapeia para:** Req 6, RNF 2
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer par de cores com razão de contraste R, a validação aprova se e somente se R ≥ 4,5:1 (texto normal); o limite é determinístico e não decrescente em R.

### PBT-05 — Determinismo dos tons derivados

**Mapeia para:** Req 8
**Tipo:** Idempotência

**Propriedade:**

> Para quaisquer cores primária e secundária, derivar os tons múltiplas vezes produz sempre o mesmo resultado (função determinística sem efeitos colaterais).

### PBT-06 — Isolamento estrito por tenant (anti-cross-tenant)

**Mapeia para:** Req 10, RNF 1
**Tipo:** Anti-enumeração

**Propriedade:**

> Para qualquer par de tenants A e B distintos, nenhuma consulta executada no contexto de A retorna configuração ou branding pertencente a B.

### PBT-07 — Atomicidade do provisionamento

**Mapeia para:** Req 12
**Tipo:** Atomicidade

**Propriedade:**

> Para qualquer tentativa de provisionamento, ao término ou existem ambos (tenant na plataforma e tenant de identidade) ou não existe nenhum dos dois; nunca um estado parcial.

### PBT-08 — Transições válidas do estado do tenant

**Mapeia para:** Req 4
**Tipo:** State machine

**Propriedade:**

> Para qualquer sequência de operações de suspensão e reativação, o estado do tenant percorre apenas transições da lista canônica da seção 4; transições inválidas são rejeitadas e não alteram o estado.

## 8. Glossário local

| Termo | Definição |
|---|---|
| tenant | Empresa cliente do Azim como produto SaaS, com isolamento completo de dados por `tenant_id`. |
| tenant_id | Identificador único (UUID) e imutável do tenant em todas as tabelas; chave de RLS *cross-cutting*. |
| slug | Identificador textual único globalmente e imutável do tenant (minúsculas e hífens); usado na URL canônica e no tenant de identidade. |
| branding (white-label estrito) | Conjunto de exatamente quatro elementos visuais configuráveis: logo, favicon, cor primária e cor secundária (DEC-004). |
| iana_timezone | Fuso horário no padrão IANA (ex.: `America/Sao_Paulo`) usado para cálculo do horário do digest. |
| horario_digest / digest_time | Horário local, no fuso do tenant, em que o digest deve ser entregue (padrão 07:00). |
| wcag_contrast_ok | Indicador de que as cores primária e secundária atendem ao contraste mínimo WCAG 2.1 AA. |
| Platform Operator (PlatOp) | Operador da plataforma; provisiona, suspende e reativa tenants; sem acesso a dados comerciais dos tenants. |
| Tenant Admin (TAdmin) | Administrador do tenant; configura branding, fuso horário e horário do digest. |
| RLS (Row-Level Security) | Segurança em nível de linha no banco, aplicada por `tenant_id`. |
| design tokens | Variáveis CSS derivadas do branding aplicadas no frontend `azim-web`. |
| objeto de valor | Elemento de domínio sem identidade própria, definido por seus atributos (ex.: cor, fuso horário). |

> Termos canônicos de domínio derivam de `docs/product/glossary/ubiquitous-language.md` (BC-13). Identificadores técnicos em inglês; o termo "objeto de valor" é sempre escrito por extenso.

## 9. Fora do escopo do MVP

- Self-service de provisionamento pelo cliente (o cliente cria o próprio tenant) — no MVP, o Platform Operator provisiona manualmente (VAL-TENANT-01).
- Operação de alteração de `slug` após a criação.
- Gestão de planos, cobrança e faturamento do Azim como SaaS (OOS-01).
- CSS, fontes ou layout customizável por tenant (OOS-08; DEC-004).
- Exclusão física de tenant e remoção definitiva de dados (direito ao esquecimento em nível de tenant).
- Consolidação de *Tenancy & Branding* com *Organization Management* em um único módulo (VAL-TENANT-02 / DDD-VAL-04).

## 10. Referências cruzadas

| Referência | Origem | Relação |
|---|---|---|
| FRD § RF-02 (FRD-admin-01, FRD-admin-02) | docs/product/frd-nfrd/frd.md | Requisitos funcionais de branding, slug e fuso |
| RN-019, RN-020 | docs/product/frd-nfrd/frd.md | Slug imutável; white-label estrito; logo PNG/SVG ≤ 1 MB; contraste WCAG AA |
| NFR-SEG-01, NFR-MAN-02 | docs/product/frd-nfrd/nfrd.md | Isolamento multi-tenant e gate de CI |
| NFR-USA-02, NFR-COMP-02 | docs/product/frd-nfrd/nfrd.md | Validação de contraste WCAG 2.1 AA |
| NFR-ESC-01 | docs/product/frd-nfrd/nfrd.md | Capacidade de tenants e janela de onboarding |
| NFR-AUD-01, NFR-OBS-01, NFR-PRIV-05 | docs/product/frd-nfrd/nfrd.md | Auditoria, observabilidade e bloqueio do Platform Operator |
| DEC-004, DEC-006, DEC-009 | docs/product/trd/trd.md; PRD | White-label estrito; pool + RLS; fuso IANA do digest |
| Data Model § Tenancy & Branding (BC-13) | docs/product/data-model/data-model.md | Tabelas `tenants` e `tenant_brandings` |
| Linguagem Ubíqua § BC-13 | docs/product/glossary/ubiquitous-language.md | Termos `tenant`, `slug`, `branding`, `tenant_id`, `horario_digest` |
| README do módulo | docs/product/modules/tenant-administration/README.md | Responsabilidades, eventos, riscos (RISK-TENANT-01/02), pontos a validar |
| `.forge/rules/domain/audit-immutability.md` | regras transversais | Auditoria *append-only* |
| `.forge/rules/architecture/observability.md` | regras transversais | `correlationId` e `tenant_id` em logs/traces |
| ADR-0001 (a ser criado) | docs/product/adr/ | Propagação de `correlationId` e `tenant_id` |
