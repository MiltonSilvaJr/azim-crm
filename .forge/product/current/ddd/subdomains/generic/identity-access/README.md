# Identity & Access — Generic Subdomain

## 1. Classificação
- **Tipo:** Generic Subdomain
- **Código:** SD-12

## 2. Descrição

Representa autenticação e gerenciamento de sessão via GCP Identity Platform multi-tenant. Suporta login por e-mail/senha e conta Google (OAuth), com sessão resolvida pelo slug do tenant. Inclui convite com link temporário, recuperação de senha, logout com invalidação global e expiração configurável de sessão.

## 3. Justificativa da Classificação

Generic porque: autenticação é commodity; GCP Identity Platform multi-tenant é um produto pronto e confiável; nenhuma lógica de autenticação proprietária é necessária. A integração é feita via Anti-Corruption Layer para proteger o modelo interno do contrato do GCP Identity Platform.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-14 | Autenticação e Identidade | Login, sessão por tenant, convite, recuperação |

## 5. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-12 Identity & Access | Implementa este subdomínio |
| BC-08 Organization Management | Downstream — após autenticação, carrega papéis e BUs do usuário |
| GCP Identity Platform | Upstream externo — provedor de identidade |

## 6. Pontos a Validar

- PRE-01: GCP Identity Platform disponível em southamerica-east1 sem limitações técnicas relevantes
- VAL-09: Prazo de expiração do link de convite (RN-030): 72h é inferência — confirmar com produto
