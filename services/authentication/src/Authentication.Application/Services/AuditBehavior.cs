namespace Authentication.Application.Services;

/// <summary>
/// Documentação do comportamento de auditoria integrado aos serviços de aplicação.
///
/// Padrão implementado (DD-003 — caminho quente por middleware; coordenação por serviços):
///   O módulo authentication não usa um barramento CQRS/MediatR clássico.
///   A auditoria é injetada diretamente nos serviços de aplicação via <see cref="IAuditEventEmitter"/>.
///
/// Eventos auditáveis emitidos (RNF 10, design.md § 9.1):
///   - <c>session_revoked</c>     → <see cref="SessionRevocationService"/>
///   - <c>invite_activated</c>    → <see cref="InviteActivationService"/>
///   - <c>password_reset_requested</c> → <see cref="PasswordResetService"/>
///
/// Eventos pendentes de integração (dependem de callback do IdP/client-side):
///   - <c>user_authenticated</c>  → emitido pelo frontend ao concluir login no IdP;
///     o backend pode inferir via <c>AuthContextComposer</c>, mas o custo por requisição
///     é proibitivo para Tier 1 (RNF 2, DD-003). Gate de decisão: DD-010 (a registrar).
///   - <c>password_reset_completed</c> → ocorre no IdP (client-side) após o usuário
///     usar o link de reset; não há callback para o backend nesta versão (DD-004).
///
/// Integração com audit-log:
///   A porta <see cref="IAuditEventEmitter"/> é mantida abstrata em Application.
///   A integração com o módulo <c>audit-log</c> (via <c>IAuditWriter</c> de
///   <c>AuditLog.Contracts</c>) ocorre no adapter em Infrastructure via referência
///   ao package de contratos. Esta decisão evita dependência circular entre módulos
///   e respeita a regra de dependência (DD-001):
///     Application → {Domain, Contracts} apenas
///     Infrastructure → {Application, Domain, Contracts, AuditLog.Contracts (externo)}
///
/// Política de falha (fail-open):
///   Falha de auditoria NÃO bloqueia o fluxo principal (design.md § 6.6).
///   O operador monitora o módulo audit-log separadamente. O logout, convite e reset
///   já ocorreram com sucesso no IdP antes da tentativa de auditoria.
///
/// Mapeia: TASK-24, RNF 10, design.md § 4.4, § 9.1, DD-003.
/// </summary>
public static class AuditBehavior
{
    // Este tipo serve como âncora de documentação e rastreabilidade.
    // A lógica está implementada em cada serviço de aplicação.
}
