using MediatR;

namespace PartnerManagement.Application.Partners.Commands;

/// <summary>
/// Comando para reativar um parceiro.
/// Idempotente: operação repetida retorna sucesso sem emitir evento de transição (DD-006, PBT-02).
/// Auditoria sempre registrada, mesmo em tentativa sobre estado já ativo.
/// Mapeia: Req 3, PBT-02, design §5.1, DD-006.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro a ser reativado.</param>
/// <param name="TenantId">Tenant do contexto.</param>
/// <param name="ActorId">Usuário que solicitou a reativação.</param>
/// <param name="CorrelationId">Identificador de correlação da requisição.</param>
public sealed record ReactivatePartnerCommand(
    Guid PartnerId,
    Guid TenantId,
    Guid ActorId,
    string? CorrelationId = null
) : IRequest<ReactivatePartnerResult>;

/// <summary>
/// Resultado da reativação de um parceiro.
/// </summary>
/// <param name="TransitionEffective">
/// Verdadeiro se houve transição efetiva (parceiro estava inativo).
/// Falso se o parceiro já estava ativo (operação idempotente).
/// </param>
public sealed record ReactivatePartnerResult(bool TransitionEffective);
