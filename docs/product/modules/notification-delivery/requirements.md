# BC-14 — Notification Delivery
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/nfrd.md § NFR-INT-02; docs/product/frd-nfrd/frd.md § RF-09 (FRD-digest-02); docs/product/trd/trd.md § 11.1

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento de requisitos do módulo notification-delivery (adapter/ACL stateless) |

## 1. Visão Geral

O módulo **notification-delivery** é um adapter do tipo **Anti-Corruption Layer** (camada anticorrupção — padrão tático de DDD que protege o modelo interno de contratos externos voláteis) responsável por isolar os módulos consumidores (`digest` e `organization`/`authentication`) do contrato específico de cada provedor de e-mail transacional (Postmark como candidato primário, SendGrid como alternativa).

O acoplamento entre o modelo de domínio e o mundo externo ocorre exclusivamente através da interface estável `IEmailSender`, cujo contrato é `Send(EmailMessage) → SendResult`. O módulo é **stateless**: não possui modelo de domínio próprio, não persiste dados, não publica nem consome eventos de domínio. O registro de resultado de envio é retornado ao chamador, que decide o que persistir (o `digest` persiste em `EmailDigestLog`).

O módulo é empacotado como biblioteca compartilhada e referenciado por injeção de dependência nos deployables `azim-digest-worker` (uso primário — envio do digest diário) e `azim-api` (envio de convites e recuperação de senha via `organization`/`authentication`).

A criticidade é Tier 2: a entregabilidade alvo é ≥ 98%, mas uma falha do provedor não pode derrubar a operação transacional principal nem bloquear o chamador.

## 2. Escopo

### 2.1 Incluído

- Definição e exposição da interface `IEmailSender` com contrato estável `Send(EmailMessage) → SendResult`.
- Definição dos objetos de valor `EmailMessage` (entrada) e `SendResult` (saída).
- Implementações concretas intercambiáveis `PostmarkEmailSender` e `SendGridEmailSender`.
- Aplicação de configuração de branding do tenant (logo, cor primária, cor secundária) no template antes do envio (`BrandingEmailDecorator`).
- Renderização de corpo HTML responsivo a partir do conteúdo fornecido pelo chamador.
- Mapeamento (ACL) da resposta do provedor para os estados canônicos de `SendResult`, incluindo sucesso, falha transiente, falha permanente (bounce) e supressão.
- Política de novas tentativas com backoff exponencial e circuit breaker para falhas transientes, sem bloquear o chamador.
- Leitura de credenciais do provedor exclusivamente via GCP Secret Manager.
- Emissão de telemetria de envio (logs estruturados sem PII, métricas e suporte a alertas).
- Verificação de disponibilidade do provedor configurado (ping de health check).

### 2.2 Excluído

- Decisão de **quem** e **quando** receber e-mail — pertence ao `digest` (seleção de destinatários, RN-009/RN-011) e ao `organization`/`authentication` (convites e recuperação de senha).
- Composição do **conteúdo de negócio** do e-mail (texto do digest, azimute da semana, blocos de metas) — pertence ao `digest` e ao `organization`.
- Persistência do registro de envio e idempotência de negócio por (usuário, data) — pertence ao `digest` via `EmailDigestLog` (RN-010).
- Configuração de DNS (SPF, DKIM, DMARC) do domínio `mail.azim.com.br` — é tarefa operacional da Fase 0; este módulo apenas depende dela como pré-condição.

### 2.3 Fora do escopo do MVP

Ver seção 9.

## 3. Personas / Atores

| Ator | Tipo | Papel no módulo |
|------|------|-----------------|
| Módulo `digest` (azim-digest-worker) | Sistema consumidor | Chama `IEmailSender.Send` para entregar o digest diário e o azimute da semana; persiste o `SendResult` em `EmailDigestLog`. |
| Módulo `organization` / `authentication` (azim-api) | Sistema consumidor | Chama `IEmailSender.Send` para enviar convites de usuário (FRD-auth-03) e recuperação de senha (FRD-auth-04). |
| Provedor de e-mail transacional (ACT-08) | Sistema externo | Executa a entrega física do e-mail e retorna status de aceitação, bounce e eventos de entregabilidade. Postmark (primário) ou SendGrid (alternativa). |
| Engenharia de Plataforma / Operações | Humano | Configura credenciais no Secret Manager, monitora entregabilidade, atua em runbooks de falha de provedor (RISK-NOTIF-01/02). |

## 4. Lista canônica de estados de resultado de envio (SendResult)

O objeto de valor `SendResult` retornado ao chamador deve assumir exatamente um dos estados canônicos abaixo. O mapeamento ACL de qualquer resposta do provedor (Postmark ou SendGrid) para esta lista é total: nenhuma resposta pode resultar em estado indefinido.

| Estado | Significado | Acionável pelo chamador |
|--------|-------------|-------------------------|
| `Sent` | Provedor aceitou a mensagem para entrega; `message_id` presente. | Registrar como enviado (status `sent`). |
| `TransientFailure` | Falha temporária (timeout, 5xx do provedor, rate limit). Elegível a nova tentativa. | Permitir nova tentativa com backoff; após esgotar, tratar como falha. |
| `PermanentFailure` | Falha definitiva não relacionada ao destinatário (credencial inválida, payload rejeitado, 4xx não recuperável). | Registrar falha; não re-tentar; alertar operação. |
| `Bounced` | Endereço rejeitado pelo destino (hard bounce). | Registrar bounce; sinalizar para supressão futura. |
| `Suppressed` | Envio não realizado porque o endereço consta na lista de supressão do provedor. | Registrar como suprimido; não contar como falha de provedor. |

> Os identificadores de estado são técnicos e permanecem em inglês. A semântica de negócio (por exemplo, alimentar o KPI-03 de entregabilidade) é responsabilidade do chamador.

## 5. Requisitos Funcionais

### Req 1 — Interface estável IEmailSender como único ponto de acoplamento

**Como** módulo consumidor (`digest`, `organization`) **quero** enviar e-mail por uma interface estável **para** não depender do contrato específico de nenhum provedor externo.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | DEC-008; TOBJ-07; NFR-INT-02 |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 1.1 O módulo expõe a interface `IEmailSender` com a operação `Send(EmailMessage) → SendResult`.
- 1.2 Nenhum tipo, exceção ou símbolo específico de provedor (Postmark, SendGrid) é exposto na assinatura pública da interface.
- 1.3 Os módulos consumidores referenciam apenas `IEmailSender`, `EmailMessage` e `SendResult`, nunca uma implementação concreta.
- 1.4 A interface é injetada por inversão de dependência; o consumidor não constrói implementação concreta diretamente.
- 1.5 Um teste unitário de consumidor consegue substituir `IEmailSender` por uma duplicata de teste (mock) sem referenciar provedor.

**Cross-ref:** NFR-INT-02, RNF 1, ADR-0005, README §7

### Req 2 — Objeto de valor EmailMessage como contrato de entrada

**Como** módulo consumidor **quero** descrever a mensagem a enviar por um objeto de valor estável **para** entregar destinatário, assunto, conteúdo e branding sem conhecer o formato do provedor.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README §7; FRD-digest-02 |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 2.1 O objeto de valor `EmailMessage` contém, no mínimo: endereço do destinatário, assunto, corpo HTML e configuração de branding do tenant (logo, cor primária, cor secundária).
- 2.2 `EmailMessage` carrega um `correlation_id` e o `tenant_id` para fins de rastreabilidade.
- 2.3 `EmailMessage` é imutável após construção (objeto de valor por definição).
- 2.4 `EmailMessage` carrega uma chave de idempotência fornecida pelo chamador, quando aplicável (ver Req 9).
- 2.5 Uma `EmailMessage` sem destinatário válido é rejeitada antes de qualquer chamada ao provedor, retornando `PermanentFailure` com motivo de validação.

**Cross-ref:** Req 5, Req 9, glossário local

### Req 3 — Objeto de valor SendResult como contrato de saída

**Como** módulo consumidor **quero** receber o resultado do envio em um objeto de valor padronizado **para** decidir o registro e o tratamento de falha independentemente do provedor.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README §7; FRD-digest-04 (campos do EmailDigestLog) |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 3.1 `SendResult` informa exatamente um estado canônico da seção 4.
- 3.2 Em caso de `Sent`, `SendResult` inclui o `message_id` retornado pelo provedor.
- 3.3 Em caso de falha, `SendResult` inclui um motivo legível e classificável, sem expor PII do destinatário.
- 3.4 `SendResult` propaga o `correlation_id` recebido na `EmailMessage`.
- 3.5 `SendResult` nunca lança exceção de provedor ao chamador; toda falha é representada como estado de `SendResult`.

**Cross-ref:** Req 8, seção 4, FRD-digest-02 (registro de message_id)

### Req 4 — Implementações concretas intercambiáveis por provedor

**Como** Engenharia de Plataforma **quero** trocar o provedor de e-mail alterando apenas a implementação concreta e a configuração **para** migrar de Postmark para SendGrid (ou vice-versa) sem tocar em código de negócio.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | NFR-INT-02; DEC-008; LAC-03 / VAL-NOTIF-01 |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 4.1 Existem implementações concretas `PostmarkEmailSender` e `SendGridEmailSender` da interface `IEmailSender`.
- 4.2 A seleção da implementação ativa é resolvida por configuração, sem alteração de código dos consumidores.
- 4.3 Ambas as implementações produzem `SendResult` com a mesma semântica de estados (seção 4) para entradas equivalentes.
- 4.4 Nenhum SDK ou tipo específico de provedor aparece fora da respectiva implementação concreta (verificável por revisão de código).
- 4.5 Trocar o provedor ativo não exige mudança em `digest` nem em `organization`, apenas configuração e injeção de dependência.

**Cross-ref:** RNF 1, PBT-01, NFR-INT-02

### Req 5 — Aplicação de branding do tenant no template

**Como** módulo consumidor **quero** que o e-mail saia com o branding do tenant **para** entregar comunicação white-label coerente com a identidade visual configurada.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-digest-02 (passo 2); FRD-admin-01 (branding aplicado ao digest); DEC-004 |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 5.1 Quando a `EmailMessage` traz configuração de branding, o módulo injeta logo, cor primária e cor secundária no template antes do envio.
- 5.2 O branding aplicado restringe-se aos cinco elementos do branding estrito (DEC-004); nenhum CSS arbitrário por tenant é aceito.
- 5.3 Na ausência de configuração de branding, o módulo aplica um tema padrão sem falhar o envio.
- 5.4 A aplicação de branding não altera o destinatário, o assunto nem o `correlation_id` da mensagem.

**Cross-ref:** Req 6, glossário local (branding), FRD-admin-01

### Req 6 — Corpo HTML responsivo

**Como** destinatário do e-mail **quero** ler o e-mail corretamente em desktop e dispositivos móveis **para** agir pelo digest sem abrir o portal.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | JRN-02; UC-04; FRD-digest-02 |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 6.1 O corpo HTML enviado renderiza de forma legível em largura de tela de dispositivo móvel e desktop.
- 6.2 O template preserva os links autenticados de 1 clique fornecidos pelo chamador sem alterá-los.
- 6.3 O e-mail inclui versão alternativa em texto puro quando o corpo HTML é fornecido, para clientes que não renderizam HTML.
- 6.4 A renderização do template é determinística para a mesma `EmailMessage` (mesma entrada produz mesma saída).

**Cross-ref:** Req 5, FRD-digest-02 (links de 1 clique)

### Req 7 — Tratamento de bounce e supressão

**Como** Engenharia de Plataforma **quero** que bounces e endereços suprimidos sejam classificados e informados ao chamador **para** preservar a reputação do domínio e a entregabilidade ≥ 98%.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | NFR-INT-03; FRD-digest-02 (FE-digest02-01); RISK-NOTIF-01; README §5 |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 7.1 Resposta de hard bounce do provedor é mapeada para `SendResult = Bounced`, com motivo classificável e sem PII.
- 7.2 Endereço constante na lista de supressão do provedor resulta em `SendResult = Suppressed`, sem tentativa de entrega.
- 7.3 `Bounced` e `Suppressed` não são contabilizados como falha de provedor para fins de circuit breaker (Req 8), pois não indicam indisponibilidade.
- 7.4 O módulo não decide o que fazer com o destinatário bounced — apenas classifica e retorna; a decisão de negócio pertence ao chamador.
- 7.5 A taxa de bounce é exposta como métrica para suportar o alerta de entregabilidade (RNF 5).

**Cross-ref:** seção 4, RNF 2, RNF 5, NFR-INT-03

### Req 8 — Novas tentativas com backoff e proteção do chamador

**Como** módulo consumidor **quero** que falhas transientes do provedor sejam re-tentadas sem me bloquear **para** que uma instabilidade do provedor não derrube o worker nem a API.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | NFR-RES-03; TRD §11.3 (retry/circuit breaker); TRD §17 (timeout 10 s, 5 tentativas) |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 8.1 Falhas transientes (timeout, 5xx, rate limit) disparam novas tentativas com backoff exponencial, até um limite configurável de tentativas.
- 8.2 Esgotadas as tentativas, o módulo retorna `SendResult = TransientFailure` ou `PermanentFailure`, sem lançar exceção ao chamador.
- 8.3 Após um número configurável de falhas consecutivas do provedor, um circuit breaker abre e o módulo passa a falhar rápido, emitindo alerta operacional, sem bloquear o chamador.
- 8.4 A chamada `Send` respeita um timeout máximo por tentativa, de modo que o chamador nunca fica bloqueado indefinidamente.
- 8.5 A falha de envio a um destinatário não impede o envio aos demais (isolamento por mensagem).

**Cross-ref:** RNF 3, PBT-05, TRD §11.3, RISK-NOTIF-01

### Req 9 — Segurança de reenvio (suporte à idempotência do chamador)

**Como** módulo consumidor **quero** que reenviar a mesma mensagem em uma retentativa não gere entrega duplicada **para** preservar a garantia de no máximo um digest por usuário por dia.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | NFR-RES-02; RN-010; FRD-digest-04 |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 9.1 Quando a `EmailMessage` carrega uma chave de idempotência, o módulo a propaga ao provedor usando o mecanismo de deduplicação suportado, quando disponível.
- 9.2 Reenviar uma `EmailMessage` com a mesma chave de idempotência resulta em no máximo uma entrega efetiva.
- 9.3 A decisão de não reenviar por (usuário, data) permanece no chamador (`EmailDigestLog`, RN-010); o módulo não mantém estado próprio de idempotência.
- 9.4 Na ausência de chave de idempotência, o módulo documenta que a deduplicação é responsabilidade integral do chamador.

**Cross-ref:** NFR-RES-02, PBT-02, FRD-digest-04, README §5

### Req 10 — Credenciais do provedor exclusivamente via Secret Manager

**Como** Engenharia de Plataforma **quero** que a chave de API do provedor venha apenas do GCP Secret Manager **para** evitar segredos no repositório ou em variável hard-coded.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README §14.3; TRD §12.6 (segredos); DEC-005 |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 10.1 A credencial do provedor (`POSTMARK_API_KEY` ou `SENDGRID_API_KEY`) é lida exclusivamente do GCP Secret Manager.
- 10.2 Nenhuma credencial de provedor é lida de variável de ambiente hard-coded, arquivo versionado ou constante de código.
- 10.3 A alternância de chave do provedor ocorre sem novo deploy do código (apenas atualização do segredo).
- 10.4 A credencial nunca aparece em logs, mensagens de erro ou telemetria.

**Cross-ref:** RNF 6, TRD §12.6, RISK-NOTIF-02

### Req 11 — Verificação de disponibilidade do provedor (health check)

**Como** plataforma de operação **quero** verificar a disponibilidade do provedor configurado **para** que o readiness do worker reflita a capacidade real de envio.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | README §17; TRD §14.5 (readiness com IEmailSender ping) |
| **Módulo** | notification-delivery |

**Critérios de Aceite:**

- 11.1 O módulo expõe uma verificação de disponibilidade (ping) do provedor configurado, consumível pelo readiness do deployable.
- 11.2 A verificação não envia e-mail real nem consome cota de envio significativa.
- 11.3 A falha do ping é reportada de forma estruturada, sem PII e sem expor a credencial.

**Cross-ref:** RNF 5, TRD §14.5

## 6. Requisitos Não-Funcionais

### RNF 1 — Portabilidade de provedor sem impacto no domínio

| Campo | Valor |
|-------|-------|
| **Categoria** | Manutenibilidade |
| **Prioridade** | Must |
| **Origem** | NFR-INT-02; DEC-008; TOBJ-07 |
| **Módulo** | notification-delivery |

**Descrição:**

A troca do provedor de e-mail transacional deve exigir mudança apenas na implementação concreta de `IEmailSender` e na configuração, sem qualquer alteração em código de negócio dos módulos consumidores.

**Critérios de Aceite:**

- RNF-1.1 Revisão de código não encontra nenhuma referência a SDK ou tipo de provedor fora da implementação concreta correspondente.
- RNF-1.2 Os testes unitários do `digest` usam `IEmailSender` substituído por duplicata de teste, sem dependência de provedor.
- RNF-1.3 A migração de provedor estimada não excede 1 sprint e não altera arquivos dos módulos consumidores.

**Cross-ref:** Req 1, Req 4, PBT-01, NFR-INT-02

### RNF 2 — Entregabilidade e pré-condições de domínio de envio

| Campo | Valor |
|-------|-------|
| **Categoria** | Disponibilidade |
| **Prioridade** | Must |
| **Origem** | NFR-INT-03; KPI-03; PRM-03; RISK-NOTIF-01 |
| **Módulo** | notification-delivery |

**Descrição:**

O envio deve operar sobre domínio com SPF, DKIM e DMARC válidos, sustentando entregabilidade ≥ 98% (KPI-03). A configuração de DNS é pré-condição operacional externa; o módulo deve monitorar e expor a taxa de bounce.

**Critérios de Aceite:**

- RNF-2.1 O módulo não realiza o primeiro envio em produção sem que SPF, DKIM e DMARC do domínio `mail.azim.com.br` estejam validados (gate de go-live da Fase 0).
- RNF-2.2 A taxa de bounce é exposta como métrica e mantém-se < 5% nos primeiros 30 dias.
- RNF-2.3 A entregabilidade efetiva, medida a partir dos `SendResult` retornados, é ≥ 98% em janela de medição operacional.

**Cross-ref:** Req 7, NFR-INT-03, RNF 5

### RNF 3 — Resiliência e não bloqueio do chamador

| Campo | Valor |
|-------|-------|
| **Categoria** | Resiliência |
| **Prioridade** | Must |
| **Origem** | NFR-RES-03; TRD §11.3; TRD §17 |
| **Módulo** | notification-delivery |

**Descrição:**

Uma falha transiente ou indisponibilidade do provedor não pode bloquear nem derrubar o chamador. O módulo aplica novas tentativas com backoff exponencial e circuit breaker, sempre devolvendo um `SendResult`.

**Critérios de Aceite:**

- RNF-3.1 Cada tentativa de envio respeita timeout máximo configurável (referência TRD: 10 s).
- RNF-3.2 Falhas transientes geram até N novas tentativas (referência TRD: 5) com backoff exponencial.
- RNF-3.3 Após N falhas consecutivas, o circuit breaker abre, dispara alerta operacional e o módulo falha rápido sem bloquear o chamador.
- RNF-3.4 Teste de caos com provedor indisponível confirma que o worker re-tenta, alerta e segue sem downtime da API.

**Cross-ref:** Req 8, PBT-05, NFR-RES-03

### RNF 4 — Ausência de PII em logs e mensagens de erro

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFR-PRIV-01; RN-025; README §16/§17; LGPD |
| **Módulo** | notification-delivery |

**Descrição:**

O endereço de e-mail do destinatário é dado pessoal (PII). Nenhum log, métrica, trace ou mensagem de erro produzido pelo módulo pode conter o e-mail completo ou outro dado pessoal do destinatário.

**Critérios de Aceite:**

- RNF-4.1 Scan automatizado de logs (regex de e-mail) não encontra endereço de destinatário em produção ou staging.
- RNF-4.2 Logs identificam o envio por `correlation_id`, `tenant_id`, `provider`, `status` e `message_id` — nunca pelo e-mail.
- RNF-4.3 Mensagens de erro de `SendResult` não expõem o endereço do destinatário nem a credencial do provedor.
- RNF-4.4 Quando necessário identificar o destinatário em telemetria, usa-se identificador mascarado ou pseudônimo, nunca o e-mail em claro.

**Cross-ref:** Req 3, NFR-PRIV-01, PBT-03, RN-025

### RNF 5 — Observabilidade de envio

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Must |
| **Origem** | README §17; TRD §14; NFR-OBS |
| **Módulo** | notification-delivery |

**Descrição:**

Cada tentativa de envio deve ser observável por log estruturado e métricas, suportando alerta de degradação de entregabilidade.

**Critérios de Aceite:**

- RNF-5.1 Cada tentativa registra log estruturado com `correlation_id`, `tenant_id`, `provider`, `status` e `message_id` (sem PII).
- RNF-5.2 São expostas as métricas `email_send_attempts_total`, `email_send_success_total` e `email_send_failure_total`.
- RNF-5.3 Existe suporte a alerta quando a taxa de falha excede 2% em janela de 1 hora.
- RNF-5.4 O envio do digest é correlacionável fim a fim (Cloud Scheduler → worker → provedor) via `correlation_id`.

**Cross-ref:** Req 7, Req 11, RNF 4, TRD §14

### RNF 6 — Gestão de segredos do provedor

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | TRD §12.6; DEC-005; README §14.3; RISK-NOTIF-02 |
| **Módulo** | notification-delivery |

**Descrição:**

A credencial do provedor deve residir exclusivamente no GCP Secret Manager, ser rotacionável sem deploy e nunca aparecer em repositório, log ou telemetria.

**Critérios de Aceite:**

- RNF-6.1 A credencial é injetada a partir do Secret Manager; o scan de segredos do CI (gitleaks) não encontra credencial no repositório.
- RNF-6.2 A rotação ou alternância de chave ocorre sem novo deploy do código.
- RNF-6.3 A credencial não aparece em nenhum log, métrica, trace ou mensagem de erro.

**Cross-ref:** Req 10, TRD §12.6, RISK-NOTIF-02

### RNF 7 — Conformidade LGPD na transferência de PII ao provedor

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Should |
| **Origem** | README §16; NFR-PRIV-04; RISK-NOTIF-03; LGPD |
| **Módulo** | notification-delivery |

**Descrição:**

O e-mail do destinatário é PII trafegado a um operador externo (provedor). A transferência deve estar amparada por acordo de tratamento de dados (DPA — Data Processing Agreement, contrato de processamento de dados) com o provedor antes do go-live.

**Critérios de Aceite:**

- RNF-7.1 DPA assinado com o provedor selecionado é pré-condição de go-live em produção (gate documental).
- RNF-7.2 O provedor configurado opera em conformidade com a base legal documentada para tratamento de dados de contatos (NFR-PRIV-04).
- RNF-7.3 Apenas os dados estritamente necessários ao envio (destinatário, assunto, corpo) são transmitidos ao provedor.

**Cross-ref:** RNF 4, NFR-PRIV-04, RISK-NOTIF-03

## 7. Property-Based Testing

### PBT-01 — Reversibilidade do adapter (substituição de provedor)

**Mapeia para:** Req 1, Req 4, RNF 1
**Tipo:** Round-trip

**Propriedade:**

> Para qualquer `EmailMessage` válida, enviar via `PostmarkEmailSender` e via `SendGridEmailSender` produz `SendResult` com a mesma forma e o mesmo conjunto de estados canônicos (seção 4), de modo que nenhum chamador consiga distinguir o provedor a partir do tipo ou da estrutura do resultado.

### PBT-02 — Idempotência de envio sob reenvio

**Mapeia para:** Req 9, NFR-RES-02
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer `EmailMessage` com chave de idempotência fixa, enviar a mensagem N ≥ 1 vezes resulta em no máximo uma entrega efetiva e em `SendResult` equivalentes entre as chamadas.

### PBT-03 — Anti-vazamento de PII em telemetria

**Mapeia para:** RNF 4, Req 3
**Tipo:** Invariante

**Propriedade:**

> Para qualquer `EmailMessage` com endereço de destinatário gerado, todo log, métrica e mensagem de erro produzido durante o envio não contém o endereço de e-mail em claro nem a credencial do provedor.

### PBT-04 — Totalidade da classificação de SendResult

**Mapeia para:** Req 3, Req 7, seção 4
**Tipo:** State machine

**Propriedade:**

> Para qualquer resposta gerada do provedor (sucesso, 4xx, 5xx, timeout, bounce, supressão), o mapeamento ACL produz exatamente um estado canônico de `SendResult` (seção 4); nenhuma resposta resulta em estado indefinido ou em exceção propagada ao chamador.

### PBT-05 — Backoff não duplica entrega

**Mapeia para:** Req 8, RNF 3
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer sequência de falhas transientes seguida de eventual sucesso, com a mesma chave de idempotência, o número de entregas efetivas ao destinatário é menor ou igual a 1.

## 8. Glossário local

| Termo | Definição | Origem |
|-------|-----------|--------|
| IEmailSender | Interface de contrato estável para envio de e-mail; único ponto de acoplamento permitido entre o modelo de domínio e os provedores externos. Operação: `Send(EmailMessage) → SendResult`. | README §7; glossário ubíquo (BC-06) |
| EmailMessage | Objeto de valor de entrada: destinatário, assunto, corpo HTML, configuração de branding, `correlation_id`, `tenant_id` e chave de idempotência. | README §7 |
| SendResult | Objeto de valor de saída: estado canônico (seção 4), `message_id`, motivo de falha (sem PII) e `correlation_id`. | README §7 |
| ACL (Anti-Corruption Layer) | Camada anticorrupção; padrão tático de DDD que protege o modelo interno do contrato externo instável. | README §7; TRD §11.3 |
| BrandingEmailDecorator | Componente que injeta logo e cores do tenant no template antes do envio. | README §8 |
| branding | Conjunto estrito de cinco elementos visuais configuráveis (logo, favicon, slug, cor primária, cor secundária). | Glossário ubíquo (BC-13); DEC-004 |
| EmailDigestLog | Registro de envio por (user_id, date) mantido pelo `digest` que garante idempotência de negócio. Externo a este módulo. | Glossário ubíquo (BC-06); RN-010 |
| PII | Dado pessoal identificável (Personally Identifiable Information); aqui, o e-mail do destinatário. | NFRD §glossário |
| DPA | Acordo de tratamento de dados (Data Processing Agreement) com o operador externo. | README §16; LGPD |
| message_id | Identificador da mensagem retornado pelo provedor em caso de aceitação. | FRD-digest-04 |

## 9. Fora do escopo do MVP

- Gestão de templates visuais avançados e editor de templates por tenant (README §5).
- Webhooks de tracking de abertura e clique do provedor — Ponto a Validar LAC-03 / VAL-NOTIF-01 (a atualização de status `opened` no `EmailDigestLog`, FRD-digest-04, é responsabilidade do `digest`, não deste módulo).
- Canais de notificação além de e-mail (notificação in-app, SMS, push) — fora da Fase 1.
- Promoção do módulo a microservice independente; na Fase 1 permanece como biblioteca compartilhada (VAL-NOTIF-02 / VAL-MOD-02).
- Lista de supressão própria do módulo; na Fase 1 a supressão é a do provedor.

## 10. Referências cruzadas

| Referência | Origem | Relação |
|------------|--------|---------|
| NFR-INT-02 | docs/product/frd-nfrd/nfrd.md | Abstração IEmailSender para portabilidade de provedor (Req 1, Req 4, RNF 1) |
| NFR-INT-03 | docs/product/frd-nfrd/nfrd.md | SPF/DKIM/DMARC e entregabilidade (RNF 2, Req 7) |
| NFR-RES-02 | docs/product/frd-nfrd/nfrd.md | Idempotência de eventos e digest (Req 9, PBT-02) |
| NFR-RES-03 | docs/product/frd-nfrd/nfrd.md | Degradação graciosa / falha de provedor (Req 8, RNF 3) |
| NFR-PRIV-01 | docs/product/frd-nfrd/nfrd.md | PII ausente de logs (RNF 4, PBT-03) |
| NFR-PRIV-04 | docs/product/frd-nfrd/nfrd.md | Base legal LGPD (RNF 7) |
| RF-09 / FRD-digest-01..04 | docs/product/frd-nfrd/frd.md | Envio do digest diário (Req 5–9) |
| FRD-auth-03 / FRD-auth-04 | docs/product/frd-nfrd/frd.md | Convites e recuperação de senha por e-mail (Req 1, Req 2) |
| DEC-008 | docs/product/trd/trd.md §4.4 | IEmailSender como ACL; Postmark primário (Req 1, Req 4) |
| TRD §7.3 / §11.1 / §11.3 / §17 | docs/product/trd/trd.md | Deployable digest worker; integração ACL; retry/circuit breaker; timeouts (Req 8, RNF 3) |
| ADR-0005 | docs/product/trd/trd.md §21 | Escolha do provedor primário após spike de entregabilidade (Req 4, VAL-NOTIF-01) |
| RN-010 / RN-025 | docs/product/frd-nfrd/frd.md §RN | Idempotência do digest; PII fora de logs (Req 9, RNF 4) |
| README do módulo | docs/product/modules/notification-delivery/README.md | Classificação, responsabilidades, riscos e pontos a validar |
