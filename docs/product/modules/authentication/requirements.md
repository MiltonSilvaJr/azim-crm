# BC-12 — Authentication
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § RF-01 (FRD-auth-01 a FRD-auth-05); docs/product/frd-nfrd/nfrd.md § NFR-SEG-02, NFR-PRIV-01; docs/product/trd/trd.md § 7.2, § 8.4, § 12.2 (DEC-005, FLOW-05)

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento de requisitos do módulo authentication (Application Module / Anti-Corruption Layer stateless sobre o GCP Identity Platform) |

## 1. Visão Geral

O módulo **authentication** é um **Application Module** do tipo **Anti-Corruption Layer** (camada anticorrupção — padrão tático de DDD que protege o modelo de domínio interno de contratos externos voláteis) que isola o Azim CRM do contrato do **GCP Identity Platform** (Firebase Authentication multi-tenant). É o módulo responsável pela autenticação de usuários e pela composição da sessão escopada a um único tenant.

O Azim CRM **não armazena nem processa senhas**: a autenticação é delegada integralmente ao Identity Platform (DEC-005, NFR-SEG-02). O frontend (`azim-web`, via Firebase SDK) realiza o login diretamente no Identity Platform e obtém um **session token** (ID token JWT — JSON Web Token). O backend (`azim-api`) recebe esse token em cada requisição e o valida via middleware de autenticação, sem expor um endpoint de login próprio que receba senha.

Cada **tenant** do Azim (empresa cliente do SaaS, isolada por `tenant_id`) corresponde a exatamente um **tenant de identidade** isolado no Identity Platform multi-tenant. A resolução do tenant ocorre pelo **slug** presente na URL canônica (`app.azim.com.br/{slug}`). A sessão resultante é **sempre escopada a um único tenant** e nunca cruza fronteiras de tenant.

O módulo é **stateless**: não possui modelo de domínio próprio nem tabela própria. O mapeamento de `identity_uid` (identificador do usuário no Identity Platform) para `user_id` (identificador interno) é resolvido por consulta ao módulo `organization`. O resultado é o objeto de valor `AuthContext`, injetado na requisição.

A criticidade é **Tier 1**: sem autenticação, nenhuma funcionalidade do CRM é acessível. A indisponibilidade do Identity Platform bloqueia todo o acesso (RISK-AUTH-01).

## 2. Escopo

### 2.1 Incluído

- Validação do **session token** (ID token JWT) emitido pelo Identity Platform em toda requisição autenticada.
- Resolução do **tenant** a partir do **slug** da URL canônica antes de qualquer acesso a dados.
- Garantia de que toda sessão é escopada a um único tenant e de que um token emitido para um tenant nunca autentica em contexto de outro tenant.
- Composição do objeto de valor **AuthContext** (`user_id`, `tenant_id`, `email`, papéis e memberships por BU) via tradução do `identity_uid` para o `user_id` interno (consulta ao módulo `organization`, com cache).
- Suporte aos métodos de login **e-mail/senha** e **Google (OIDC — OpenID Connect)**, ambos via Identity Platform multi-tenant.
- Coordenação do **convite por e-mail com ativação** de novo usuário (token temporário com expiração), em conjunto com `organization` e `notification-delivery`.
- Coordenação da **recuperação de senha por e-mail** (link temporário) para usuários do método e-mail/senha.
- **Encerramento de sessão** (logout) com invalidação global em todos os dispositivos e **revogação** de sessão.
- **Expiração** de sessão, de link de convite e de link de recuperação de senha.
- **Anti-enumeração de e-mail**: respostas observáveis indistinguíveis entre e-mail existente e inexistente nos fluxos públicos.
- Endpoint de **perfil do usuário autenticado** (`/v1/auth/me`).
- Telemetria de autenticação (logs estruturados sem PII em texto claro, métricas e suporte a alertas).
- Isolamento (ACL) reversível do modelo interno em relação ao contrato do Identity Platform, mantendo o `identity_uid` confinado ao adapter.

### 2.2 Excluído

- **Cadastro** de usuários e gestão de **papéis e permissões** (RBAC) — pertence ao módulo `organization` (RF-03).
- **Provisionamento** do tenant de identidade no Identity Platform — pertence ao módulo `tenant-administration` (RF-02).
- Definição e imutabilidade do **slug** do tenant — pertence ao `tenant-administration` (FRD-admin-02, RN-019); este módulo apenas **consome** o slug para resolver o tenant.
- **Autorização por recurso** (verificação RBAC em cada endpoint de negócio) — aplicada pelos próprios módulos de negócio (NFR-SEG-03).
- **Envio físico** do e-mail de convite e de recuperação — pertence ao `notification-delivery` (interface `IEmailSender`).
- Composição do conteúdo de negócio do e-mail de convite — pertence ao `organization`.

### 2.3 Fora do escopo do MVP

Ver seção 9.

## 3. Personas / Atores

| Ator | Tipo | Papel no módulo |
|------|------|-----------------|
| Usuário do tenant (Tenant Admin, Gestor de BU, Vendedor, Viewer) | Humano | Autentica-se via e-mail/senha ou Google; possui sessão escopada ao seu tenant; ativa conta a partir de convite; recupera senha. |
| Tenant Admin (ACT-04) | Humano | Convida novos usuários informando e-mail, papel e BU(s); aciona o fluxo de convite. |
| Frontend `azim-web` (Firebase SDK) | Sistema consumidor | Realiza o login diretamente no Identity Platform, obtém o session token e o envia ao backend em cada requisição. |
| GCP Identity Platform | Sistema externo (IdP) | Executa a autenticação, emite e assina o session token (JWT) por tenant de identidade, gerencia provedores e chaves de validação. |
| Módulo `organization` | Sistema interno | Resolve `identity_uid` → `user_id`, fornece memberships e papéis para compor o AuthContext; mantém o cadastro de usuários e convites. |
| Módulo `notification-delivery` | Sistema interno | Entrega fisicamente os e-mails de convite e de recuperação de senha via `IEmailSender`. |
| Engenharia de Plataforma / Operações | Humano | Configura o tenant de identidade e os segredos via Secret Manager, monitora SLA do IdP, atua em runbooks (RISK-AUTH-01). |

## 4. Lista canônica de estados de sessão

A sessão de um usuário, do ponto de vista deste módulo, transita entre os seguintes estados canônicos. O módulo é stateless quanto à persistência, mas a validação do token reflete deterministicamente estes estados.

| Estado | Descrição | Transições válidas a partir deste estado |
|--------|-----------|-------------------------------------------|
| `anonymous` | Requisição sem token ou com token ausente; nenhum AuthContext. | → `authenticated` (login bem-sucedido com token válido) |
| `authenticated` | Token válido, não expirado, escopado ao tenant resolvido; AuthContext composto. | → `expired` (TTL atingido), → `revoked` (logout/revogação), → `anonymous` (fim da requisição) |
| `expired` | Token cujo prazo de validade foi atingido; tratado como não autenticado (HTTP 401). | → `authenticated` (renovação válida do token) |
| `revoked` | Token invalidado por logout global ou revogação administrativa; tratado como não autenticado (HTTP 401). | → `authenticated` (novo login) |

Regra invariável: a partir de `expired` ou `revoked`, o acesso a dados protegidos é negado (HTTP 401) até nova autenticação válida. Não há transição direta de `expired`/`revoked` para acesso autorizado sem novo token válido.

## 5. Requisitos Funcionais

### Req 1 — Resolução de tenant pelo slug

**Como** usuário do tenant **quero** acessar o Azim pela URL canônica do meu tenant **para** que minha sessão seja resolvida e isolada no tenant correto antes de qualquer acesso a dados.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-auth-01; TRD § FLOW-05, § 12.2 (DEC-005); RN-001 |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 1.1 Toda requisição autenticada resolve o `tenant_id` a partir do **slug** (presente no path `app.azim.com.br/{slug}` ou no header `X-Tenant-Slug`) antes de qualquer acesso a dados protegidos.
- 1.2 Slug inexistente ou inativo resulta em resposta de erro orientativa (página 404, mensagem MSG-002) sem expor dados internos nem confirmar a existência de outros slugs.
- 1.3 O slug resolvido determina o **tenant de identidade** do Identity Platform usado na validação do token.
- 1.4 A resolução de slug é determinística: o mesmo slug ativo resolve sempre para o mesmo `tenant_id`.
- 1.5 O middleware de resolução de tenant é obrigatório e executa antes do middleware que carrega dados de negócio.

**Cross-ref:** Req 4, RNF 1, FRD-admin-02 (módulo `tenant-administration`, slug imutável RN-019), `.forge/rules/architecture/security-and-compliance.md`

### Req 2 — Login por e-mail e senha

**Como** usuário do tenant **quero** entrar no Azim informando e-mail e senha **para** acessar o CRM com identidade individual e sessão isolada no meu tenant.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-auth-01; NFR-SEG-02 |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 2.1 O login com e-mail e senha é realizado contra o **tenant de identidade** resolvido pelo slug (Req 1), via Identity Platform; o Azim não recebe nem armazena a senha.
- 2.2 Credencial válida produz um session token que, ao ser validado pelo backend, gera uma sessão escopada ao `tenant_id` e ao `user_id` (Req 4 e Req 5).
- 2.3 Credencial inválida exibe MSG-001 ("E-mail ou senha incorretos.") **sem revelar** qual campo está incorreto nem se o e-mail existe (ver Req 10).
- 2.4 Conta com convite pendente (não ativada) não autentica e orienta o usuário a verificar o e-mail de convite (MSG-004).
- 2.5 Um e-mail/senha válido para o tenant A nunca produz sessão válida em contexto do tenant B.

**Cross-ref:** Req 1, Req 4, Req 5, Req 10, PBT-01, PBT-02, RNF 1

### Req 3 — Login por conta Google (OIDC)

**Como** usuário do tenant **quero** entrar no Azim usando minha conta Google **para** acessar o CRM sem manter uma senha separada.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-auth-02; TRD § 12.2 |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 3.1 O login via Google ocorre pelo fluxo OAuth2/OIDC do Identity Platform usando o **tenant de identidade** correspondente ao slug resolvido (Req 1).
- 3.2 E-mail Google **associado** a um usuário ativo do tenant produz sessão válida escopada ao tenant.
- 3.3 E-mail Google **não associado** ao tenant é bloqueado e orienta o usuário a usar o e-mail correto ou solicitar convite (MSG-006).
- 3.4 O mesmo `user_id`, papéis e BUs são carregados independentemente do método de login (e-mail/senha ou Google).
- 3.5 Um e-mail Google associado ao tenant A nunca produz sessão válida em contexto do tenant B.

**Cross-ref:** Req 1, Req 2, Req 5, PBT-01, PBT-02

### Req 4 — Validação de session token em toda requisição

**Como** plataforma **quero** validar o session token em cada requisição protegida **para** garantir que nenhum acesso ocorra sem autenticação válida e escopada ao tenant.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-auth-01 (RN-012); NFR-SEG-02; README § 4 |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 4.1 Todo endpoint protegido valida o session token (ID token JWT) via middleware de autenticação, sem bypass.
- 4.2 Token ausente, malformado, com assinatura inválida, expirado ou revogado resulta em HTTP 401, sem expor detalhe interno na mensagem de erro.
- 4.3 A validação confirma que o token foi emitido para o **tenant de identidade** correspondente ao `tenant_id` resolvido pelo slug; divergência resulta em HTTP 401.
- 4.4 Token válido extrai `identity_uid` e `tenant_id` e prossegue para a composição do AuthContext (Req 5).
- 4.5 A validação utiliza as chaves públicas de validação do Identity Platform, cuja renovação é gerenciada pelo IdP, sem segredo embutido no repositório.

**Cross-ref:** Req 1, Req 5, Req 9, PBT-01, PBT-02, RNF 1, RNF 6, `.forge/rules/architecture/jwt-authentication.md`

### Req 5 — Composição do AuthContext via tradução do identity_uid

**Como** plataforma **quero** traduzir o identificador externo do usuário para o identificador interno e carregar seu contexto **para** que os módulos de negócio operem apenas sobre o modelo interno, isolados do contrato do Identity Platform.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README § 4, § 7; TRD § 8.4 (ACL); decisão arquitetural DEC-005 |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 5.1 Após validar o token (Req 4), o módulo traduz o `identity_uid` para o `user_id` interno por consulta ao módulo `organization`.
- 5.2 O resultado é o objeto de valor `AuthContext` contendo `user_id`, `tenant_id`, `email`, papéis e memberships por BU.
- 5.3 O `AuthContext` é injetado na requisição e disponibilizado aos módulos de negócio; o `identity_uid` **não** é exposto fora do adapter do Identity Platform.
- 5.4 `identity_uid` validado sem `user_id` correspondente no tenant (usuário desativado ou inexistente) resulta em acesso negado (HTTP 403), sem vazar a existência do `identity_uid`.
- 5.5 Os memberships podem ser servidos a partir de cache com expiração (TTL — time to live) configurável; mudança de papel invalida o cache explicitamente para evitar operação com papel desatualizado (RISK-AUTH-02).

**Cross-ref:** Req 4, Req 6, RNF 2, RNF 5, README § 19 (RISK-AUTH-02, RISK-AUTH-03)

### Req 6 — Isolamento e reversibilidade do tenant de identidade (ACL)

**Como** arquiteto **quero** que cada tenant Azim tenha um tenant de identidade isolado e que o acoplamento ao Identity Platform seja reversível **para** impedir vazamento entre tenants e proteger o modelo interno de mudanças no IdP externo.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | TRD § 8.4 (ACL), § 12.2 (DEC-005); README § 19 (RISK-AUTH-03) |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 6.1 Existe exatamente **um tenant de identidade** no Identity Platform por tenant Azim; usuários de um tenant de identidade não autenticam em outro.
- 6.2 Todo acesso ao Identity Platform ocorre através de uma interface estável (adapter); o contrato específico do IdP (formato de `identity_uid`, claims do token, SDK) não vaza para o modelo de domínio interno.
- 6.3 Nenhum identificador, claim ou tipo do Identity Platform aparece em entidades de domínio, eventos ou contratos de API de negócio.
- 6.4 A substituição do provedor de identidade (reversibilidade do ACL) impacta apenas o adapter, sem alteração nos módulos de negócio que consomem o `AuthContext`.
- 6.5 Uma falha de tradução ou contrato inesperado do IdP é mapeada para erro interno controlado, sem propagar exceção específica do SDK ao chamador.

**Cross-ref:** Req 4, Req 5, PBT-02, `.forge/rules/architecture/ddd.md`

### Req 7 — Convite por e-mail com ativação de conta

**Como** Tenant Admin **quero** convidar um novo usuário por e-mail com um link de ativação temporário **para** que ele crie credencial e passe a acessar o tenant com o papel e as BUs atribuídos.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-auth-03; RN-030 |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 7.1 O convite gera um token temporário de ativação com prazo de expiração configurável (referência: 72 horas — VAL-09/VAL-AUTH-02 a confirmar) associado ao tenant, ao e-mail, ao papel e às BUs.
- 7.2 O e-mail de ativação é entregue ao convidado via `notification-delivery` (MSG-009 em caso de falha de entrega).
- 7.3 O convidado ativa a conta pelo link, definindo senha **ou** vinculando conta Google no tenant de identidade correto.
- 7.4 Link de convite **expirado** não ativa a conta e exibe MSG-008 orientando solicitar novo convite.
- 7.5 Link de convite já consumido não pode ser reutilizado para ativar outra conta nem reativar (uso único).
- 7.6 Tentativa de convidar e-mail **já cadastrado e ativo** no tenant é bloqueada (MSG-007) sem expor dados do usuário existente além do necessário para orientar o admin.

**Cross-ref:** Req 8, Req 10, PBT-05, RNF 8; módulo `organization` (FRD-org-02, cadastro e papéis); módulo `notification-delivery` (`IEmailSender`)

### Req 8 — Recuperação de senha por e-mail

**Como** usuário do método e-mail/senha **quero** redefinir minha senha por um link enviado ao meu e-mail **para** recuperar o acesso sem intervenção do administrador.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-auth-04 |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 8.1 A solicitação de recuperação gera um link temporário de redefinição via Identity Platform, entregue por `notification-delivery`.
- 8.2 O link de redefinição expira após o uso (uso único) **ou** após o prazo configurado, o que ocorrer primeiro.
- 8.3 A resposta à solicitação é **indistinguível** entre e-mail existente e inexistente no tenant (ver Req 10): nenhuma mensagem confirma ou nega a existência do e-mail.
- 8.4 Usuário cujo método principal é Google não visualiza a opção de recuperação de senha.
- 8.5 A redefinição de senha ocorre integralmente no Identity Platform; o Azim não recebe nem armazena a nova senha.

**Cross-ref:** Req 2, Req 10, PBT-03, PBT-05, RNF 1, RNF 8; módulo `notification-delivery`

### Req 9 — Encerramento, expiração e revogação de sessão

**Como** usuário **quero** encerrar minha sessão com invalidação global e ter a sessão expirada por inatividade ou prazo **para** reduzir o risco de acesso indevido a partir de qualquer dispositivo.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-auth-05; NFR-SEG-02; README § 20 (VAL-AUTH-02) |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 9.1 O endpoint `POST /v1/auth/logout` invalida a sessão do usuário corrente **em todos os dispositivos** (invalidação global).
- 9.2 Após logout, tokens de sessão anteriores são tratados como inválidos (HTTP 401) em requisições subsequentes.
- 9.3 A sessão expira ao atingir o prazo de validade do token (TTL); a política de duração e renovação é configurável (VAL-AUTH-02, a confirmar antes do go-live).
- 9.4 Token expirado ou revogado nunca concede acesso a dados protegidos até nova autenticação válida (ver estados `expired` e `revoked` da seção 4).
- 9.5 O logout é **idempotente**: repetir a operação para uma sessão já encerrada produz o mesmo estado final (sessão inválida) sem erro adicional.

**Cross-ref:** Req 4, seção 4 (estados de sessão), PBT-04, RNF 10

### Req 10 — Anti-enumeração de e-mail nos fluxos públicos

**Como** responsável por segurança de aplicação **quero** que os fluxos públicos não revelem se um e-mail existe no tenant **para** impedir enumeração de usuários e reduzir a superfície de ataque.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | Decisão de segurança (AppSec); NFR-SEG-06; NFR-PRIV-01 |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 10.1 A resposta de **login** com credencial inválida não distingue "e-mail inexistente" de "senha incorreta" (MSG-001 única).
- 10.2 A resposta de **recuperação de senha** é idêntica para e-mail existente e inexistente (Req 8.3), incluindo tempo de resposta sem variação que permita inferência (timing).
- 10.3 A resposta de **login Google** para e-mail não associado usa mensagem genérica de orientação (MSG-006) sem confirmar quais e-mails existem no tenant.
- 10.4 Mensagens de erro dos fluxos públicos não incluem PII (e-mail, nome) nem detalhe interno (existência de conta, `identity_uid`, `user_id`).
- 10.5 Tentativas repetidas a partir do mesmo IP ou para o mesmo e-mail são limitadas por rate limiting (ver RNF 8) sem revelar o motivo do bloqueio de forma que indique existência de conta.

**Cross-ref:** Req 2, Req 3, Req 8, PBT-03, RNF 8, RNF 4

### Req 11 — Perfil do usuário autenticado

**Como** usuário autenticado **quero** consultar meu perfil e meus papéis **para** que o frontend exiba as funcionalidades e BUs às quais tenho acesso.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README § 9 (GET /v1/auth/me); FRD § FRD-auth-01 |
| **Módulo** | authentication |

**Critérios de Aceite:**

- 11.1 O endpoint `GET /v1/auth/me` retorna `user_id`, `email`, papéis e memberships por BU do usuário autenticado, a partir do `AuthContext` (Req 5).
- 11.2 O endpoint retorna apenas dados do usuário corrente e do tenant da sessão; nunca dados de outro usuário ou tenant.
- 11.3 Requisição sem token válido ao endpoint retorna HTTP 401.
- 11.4 A resposta não expõe `identity_uid` nem qualquer identificador específico do Identity Platform.

**Cross-ref:** Req 4, Req 5, Req 6, PBT-01

## 6. Requisitos Não-Funcionais

### RNF 1 — Delegação integral da autenticação (zero senhas no Azim)

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-02; TRD § 12.2 (DEC-005); REST-NF-02 |
| **Módulo** | authentication |

**Descrição:**

Nenhuma senha de usuário pode ser armazenada ou processada pelo sistema Azim. A autenticação é delegada integralmente ao GCP Identity Platform multi-tenant.

**Critérios de Aceite:**

- RNF-1.1 Zero senhas de usuário armazenadas no Cloud SQL do Azim (ausência de coluna de hash de senha no schema de usuários).
- RNF-1.2 100% dos fluxos de autenticação (e-mail/senha, Google, convite, recuperação) passam pelo Identity Platform.
- RNF-1.3 Requisição com token inválido ou expirado retorna HTTP 401, verificável por teste de integração.
- RNF-1.4 Token emitido para o tenant A não autentica em contexto do tenant B, verificável por teste de integração.

**Cross-ref:** Req 2, Req 4, Req 8, PBT-02

### RNF 2 — Latência de autenticação

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Must |
| **Origem** | NFR-PERF-04 |
| **Módulo** | authentication |

**Descrição:**

As operações de autenticação (validação de token, composição do AuthContext, renovação) devem responder dentro do SLO definido, medindo a latência no backend e excluindo a latência do Identity Platform externo, que é monitorada à parte.

**Critérios de Aceite:**

- RNF-2.1 Em teste de carga pré-release, as operações de auth do backend respondem com p95 ≤ 1 s.
- RNF-2.2 A latência do Identity Platform é monitorada separadamente via Cloud Monitoring como dependência externa.
- RNF-2.3 O carregamento de memberships usa cache (Redis/Memorystore) com TTL configurável para evitar consulta ao banco a cada requisição.

**Cross-ref:** Req 5, README § 13 (cache)

### RNF 3 — Disponibilidade Tier 1

| Campo | Valor |
|-------|-------|
| **Categoria** | Disponibilidade |
| **Prioridade** | Must |
| **Origem** | NFR-DISP-01; README § 19 (RISK-AUTH-01) |
| **Módulo** | authentication |

**Descrição:**

A autenticação é função Tier 1: sua indisponibilidade bloqueia todo o acesso ao CRM. A dependência crítica do Identity Platform deve ser monitorada e ter seu SLA acompanhado.

**Critérios de Aceite:**

- RNF-3.1 A autenticação compõe os módulos cobertos pela meta mínima de disponibilidade mensal de Tier 1 (NFR-DISP-01).
- RNF-3.2 Health checks verificam a conexão com o Identity Platform e o Redis na inicialização e na prontidão (readiness) do `azim-api`.
- RNF-3.3 Falha em massa de validação de token (HTTP 401) dispara alerta operacional para investigação do Identity Platform.

**Cross-ref:** RNF 9, README § 17 (health checks)

### RNF 4 — Observabilidade sem PII

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Must |
| **Origem** | NFR-OBS-01; NFR-PRIV-01; README § 17 |
| **Módulo** | authentication |

**Descrição:**

Todo evento de autenticação deve gerar log estruturado por requisição, com rastreabilidade por tenant e por requisição, sem expor PII (e-mail, nome) nem identificadores do Identity Platform em texto claro.

**Critérios de Aceite:**

- RNF-4.1 Logs de autenticação em formato JSON com campos obrigatórios `correlationId`, `tenantId`, `service`, `level`, `timestamp` e `endpoint`.
- RNF-4.2 Nenhum log emite e-mail em texto claro; PII é mascarada (NFR-PRIV-01).
- RNF-4.3 Nenhum log ou mensagem de erro expõe `identity_uid` fora do adapter, senha, token completo ou claim sensível.
- RNF-4.4 Em contexto autenticado, 100% dos logs contêm `tenantId`.

**Cross-ref:** Req 10, RNF 5, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/security-and-compliance.md`

### RNF 5 — Métricas e alerta de falha de validação de token

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Should |
| **Origem** | README § 17 |
| **Módulo** | authentication |

**Descrição:**

O módulo deve emitir métricas de sucesso e falha de validação de token e suportar alerta quando a taxa de falha cresce acima do baseline, sinal de possível ataque.

**Critérios de Aceite:**

- RNF-5.1 Métricas `auth_token_validation_success_total` e `auth_token_validation_failure_total` emitidas e rotuladas por tenant.
- RNF-5.2 Alerta configurado quando `auth_token_validation_failure_total` cresce acima do baseline definido.
- RNF-5.3 As métricas permitem distinguir falha por token expirado, assinatura inválida e divergência de tenant.

**Cross-ref:** RNF 3, RNF 4, Req 4

### RNF 6 — TLS em todo tráfego com o Identity Platform

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-04 |
| **Módulo** | authentication |

**Descrição:**

Todo tráfego entre o `azim-api` e o GCP Identity Platform, e entre o `azim-api` e o Redis/Memorystore usado para sessão, deve ocorrer cifrado em trânsito.

**Critérios de Aceite:**

- RNF-6.1 Comunicação API → Identity Platform e API → Memorystore ocorre exclusivamente sobre TLS.
- RNF-6.2 As chaves públicas de validação de token são obtidas por canal cifrado e têm sua renovação gerenciada pelo Identity Platform.

**Cross-ref:** Req 4, NFR-SEG-04

### RNF 7 — Segredos exclusivamente via Secret Manager

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-05; TRD § 12.2 (DEC-005); README § 14.3 |
| **Módulo** | authentication |

**Descrição:**

Toda credencial necessária à integração com o Identity Platform (service account key do Firebase Admin SDK) deve ser lida exclusivamente do GCP Secret Manager, sem segredo em repositório, imagem ou variável de ambiente em texto claro.

**Critérios de Aceite:**

- RNF-7.1 Zero segredos do Identity Platform em repositório Git, imagem Docker ou código.
- RNF-7.2 Credenciais lidas em runtime a partir do Secret Manager.
- RNF-7.3 Verificação automatizada (scan) bloqueia o build na presença de segredo versionado.

**Cross-ref:** NFR-SEG-05, `.forge/rules/architecture/security-and-compliance.md`

### RNF 8 — Rate limiting e proteção contra abuso de autenticação

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-06; decisão de segurança (AppSec) |
| **Módulo** | authentication |

**Descrição:**

Os fluxos públicos de autenticação (login, recuperação de senha, ativação de convite) devem ser protegidos contra força bruta e enumeração por rate limiting por IP e por tenant, aplicado na borda (Cloud Armor) e/ou na aplicação.

**Critérios de Aceite:**

- RNF-8.1 Tentativas de login e de recuperação de senha são limitadas por IP e por tenant; excesso resulta em bloqueio temporário (HTTP 429).
- RNF-8.2 O bloqueio por rate limiting não revela, na resposta, se um e-mail existe no tenant (coerente com Req 10).
- RNF-8.3 Eventos de bloqueio por rate limiting são observáveis (métrica/log) para alerta de possível ataque.

**Cross-ref:** Req 10, Req 2, Req 8, NFR-SEG-06

### RNF 9 — Resiliência à indisponibilidade do Identity Platform

| Campo | Valor |
|-------|-------|
| **Categoria** | Resiliência |
| **Prioridade** | Should |
| **Origem** | README § 19 (RISK-AUTH-01) |
| **Módulo** | authentication |

**Descrição:**

A indisponibilidade do Identity Platform deve ser tratada de forma controlada, com circuit breaker na validação de token e mensagem de erro que não exponha detalhe interno, sem efeito de cascata que degrade outros serviços além do esperado para uma função Tier 1.

**Critérios de Aceite:**

- RNF-9.1 Falha de comunicação com o Identity Platform retorna erro controlado (HTTP 503) com mensagem orientativa, sem stack trace nem detalhe do SDK.
- RNF-9.2 Circuit breaker evita esgotamento de recursos do `azim-api` durante indisponibilidade prolongada do IdP.
- RNF-9.3 A recuperação da disponibilidade do IdP restabelece a validação de token sem reinício do serviço.

**Cross-ref:** RNF 3, Req 6 (mapeamento de falha do ACL)

### RNF 10 — Auditoria de eventos de autenticação

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Should |
| **Origem** | README § 16 (LGPD); NFR-AUD; `.forge/rules/domain/audit-immutability.md` |
| **Módulo** | authentication |

**Descrição:**

Eventos relevantes de autenticação (login bem-sucedido, logout/revogação global, ativação de convite, recuperação de senha) devem ser rastreáveis de forma imutável (append-only), informando autor, tenant e momento, sem armazenar PII sensível em texto claro.

**Critérios de Aceite:**

- RNF-10.1 Cada evento auditável registra `user_id`, `tenant_id`, tipo de evento e timestamp, de forma append-only.
- RNF-10.2 O registro de auditoria não armazena senha, token completo nem `identity_uid` em texto claro.
- RNF-10.3 Logout/revogação global e ativação de convite são eventos auditáveis distinguíveis.

**Cross-ref:** Req 7, Req 8, Req 9, `.forge/rules/domain/audit-immutability.md`; módulo `audit-log`

## 7. Property-Based Testing

### PBT-01 — Sessão sempre escopada a um único tenant

**Mapeia para:** Req 1, Req 2, Req 3, Req 4, Req 11
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer sessão autenticada gerada (qualquer combinação de usuário válido, método de login e slug resolvido), o `AuthContext` resultante contém exatamente um `tenant_id`, igual ao tenant resolvido pelo slug da requisição, e todo dado acessível na sessão pertence a esse `tenant_id`.

### PBT-02 — Isolamento cross-tenant do token

**Mapeia para:** Req 2, Req 3, Req 4, Req 6, RNF 1
**Tipo:** Anti-enumeração

**Propriedade:**

> Para qualquer par de tenants distintos (A, B) e qualquer token válido emitido para o tenant de identidade de A, a validação desse token em contexto do tenant B sempre falha (HTTP 401) e nunca produz `AuthContext` algum.

### PBT-03 — Resposta indistinguível nos fluxos públicos (anti-enumeração de e-mail)

**Mapeia para:** Req 8, Req 10
**Tipo:** Anti-enumeração

**Propriedade:**

> Para qualquer e-mail gerado (existente ou inexistente no tenant), a resposta observável do fluxo de recuperação de senha — corpo, código HTTP e categoria de mensagem — é indistinguível entre os dois casos, não permitindo inferir a existência da conta.

### PBT-04 — Idempotência do logout / revogação

**Mapeia para:** Req 9
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer sessão e qualquer número N ≥ 1 de chamadas consecutivas de logout sobre ela, o estado final é o mesmo (sessão inválida; tokens anteriores tratados como inválidos), e nenhuma chamada após a primeira altera esse estado nem produz erro adicional.

### PBT-05 — Link de convite ou recuperação expirado/consumido nunca concede acesso

**Mapeia para:** Req 7, Req 8
**Tipo:** State machine

**Propriedade:**

> Para qualquer link de ativação de convite ou de recuperação de senha que esteja expirado (prazo atingido) ou já consumido (uso único), qualquer tentativa de uso falha sem ativar conta nem redefinir senha; não existe sequência de operações que reabilite um link nesses estados.

## 8. Glossário local

| Termo | Definição |
|-------|-----------|
| session token | ID token JWT (JSON Web Token) emitido e assinado pelo GCP Identity Platform, válido por tenant de identidade; transportado como `Authorization: Bearer <jwt>`. |
| identity_uid | Identificador único do usuário no GCP Identity Platform; confinado ao adapter (ACL), nunca exposto ao modelo de domínio interno. |
| user_id | Identificador interno (UUID) do usuário no Azim, mantido pelo módulo `organization`. |
| tenant | Empresa cliente do Azim como produto SaaS, isolada por `tenant_id` (linguagem ubíqua BC-13). |
| tenant_id | UUID que identifica o tenant em todo o sistema; chave de isolamento (RLS). |
| tenant de identidade | Tenant isolado no GCP Identity Platform multi-tenant; há exatamente um por tenant Azim. |
| slug | Identificador único do tenant na URL canônica `app.azim.com.br/{slug}`; imutável após definição (RN-019); consumido por este módulo para resolver o tenant. |
| AuthContext | Objeto de valor interno com `user_id`, `tenant_id`, `email`, papéis e memberships por BU; isolado do modelo do Identity Platform. |
| ACL (Anti-Corruption Layer) | Camada anticorrupção que traduz o modelo do Identity Platform (`identity_uid`) para o modelo interno (`user_id`) e protege o domínio de mudanças no IdP. |
| membership | Vínculo entre um usuário e uma BU com papel específico (linguagem ubíqua BC-08). |
| convite | Token temporário enviado por e-mail para ativação de conta de novo usuário (RN-030). |
| OIDC (OpenID Connect) | Camada de identidade sobre OAuth2 usada no login Google via Identity Platform. |
| TTL (time to live) | Prazo de validade de um token, link ou entrada de cache. |

## 9. Fora do escopo do MVP

- **Autenticação máquina a máquina (M2M)** entre workers internos (ex.: `digest-worker` → `azim-api`) — em avaliação (VAL-AUTH-01; sugestão: Service Account GCP + IAM / Workload Identity Federation; ver `.forge/rules/architecture/mtls-internal-services.md`).
- **Refresh token** de longa duração e política avançada de renovação de sessão — duração e política a definir antes do go-live (VAL-AUTH-02).
- **Múltiplos provedores de login social** além do Google (ex.: Microsoft, Apple).
- **Autenticação multifator (MFA)** explícita gerenciada pelo Azim — depende de configuração do Identity Platform; não detalhada nesta versão.
- **Single Sign-On (SSO) corporativo** por tenant (SAML/OIDC federado com o IdP do cliente).
- **Portal e login de parceiro comissionado** — parceiro é entidade de dados sem login no MVP (DEC-012, RN-021).

## 10. Referências cruzadas

| Referência | Origem | Relação |
|------------|--------|---------|
| RF-01 (FRD-auth-01 a FRD-auth-05) | docs/product/frd-nfrd/frd.md | Fonte funcional: login, convite, recuperação, logout, sessão por slug |
| NFR-SEG-02 | docs/product/frd-nfrd/nfrd.md | Autenticação via Identity Platform; zero senhas no Azim |
| NFR-SEG-04 | docs/product/frd-nfrd/nfrd.md | TLS em todo tráfego (RNF 6) |
| NFR-SEG-05 | docs/product/frd-nfrd/nfrd.md | Segredos via Secret Manager (RNF 7) |
| NFR-SEG-06 | docs/product/frd-nfrd/nfrd.md | WAF e rate limiting de borda (RNF 8) |
| NFR-PERF-04 | docs/product/frd-nfrd/nfrd.md | Latência de autenticação (RNF 2) |
| NFR-DISP-01 | docs/product/frd-nfrd/nfrd.md | Disponibilidade Tier 1 (RNF 3) |
| NFR-OBS-01 | docs/product/frd-nfrd/nfrd.md | Logs estruturados com tenant_id e correlationId (RNF 4) |
| NFR-PRIV-01 | docs/product/frd-nfrd/nfrd.md | PII ausente de logs (RNF 4) |
| DEC-005 | docs/product/trd/trd.md § 12.2 | Identity Platform multi-tenant; secrets via Secret Manager; ACL `authentication` |
| FLOW-05 | docs/product/trd/trd.md § 7 | Autenticação e resolução de tenant por slug |
| RN-001, RN-012 | docs/product/frd-nfrd/frd.md | Sessão associada ao tenant resolvido pelo slug; validação em toda requisição |
| RN-030 | docs/product/frd-nfrd/frd.md | Expiração configurável do link de convite (VAL-09) |
| RN-019 | docs/product/frd-nfrd/frd.md | Slug imutável (módulo `tenant-administration`) |
| Módulo `organization` | docs/product/modules/organization/ | Resolve `identity_uid` → `user_id`; cadastro, papéis e memberships |
| Módulo `tenant-administration` | docs/product/modules/tenant-administration/ | Provisiona o tenant de identidade e define o slug |
| Módulo `notification-delivery` | docs/product/modules/notification-delivery/requirements.md | Entrega física dos e-mails de convite e recuperação (`IEmailSender`) |
| Módulo `audit-log` | docs/product/modules/audit-log/requirements.md | Registro imutável dos eventos auditáveis de autenticação (RNF 10) |
| `.forge/rules/architecture/jwt-authentication.md` | regra transversal | Validação de JWT em toda requisição (Req 4) |
| `.forge/rules/architecture/jwt-permissions.md` | regra transversal | Claims e papéis no token (composição do AuthContext) |
| `.forge/rules/architecture/ddd.md` | regra transversal | Padrão ACL e objeto de valor |
| `.forge/rules/architecture/security-and-compliance.md` | regra transversal | Segredos, anti-enumeração, exposição indevida |
| `.forge/rules/architecture/observability.md` | regra transversal | correlationId e tenant_id em logs e traces |
| `.forge/rules/domain/audit-immutability.md` | regra transversal | Auditoria append-only |
