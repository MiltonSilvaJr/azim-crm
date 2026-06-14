using MediatR;

namespace PartnerManagement.Application.Partners.Commands;

/// <summary>
/// Comando para inativar um parceiro (soft-delete lógico via <c>active = false</c>).
/// Idempotente: operação repetida retorna sucesso sem emitir evento de transição (DD-006, PBT-02).
/// Auditoria sempre registrada, mesmo em tentativa sobre estado já inativo.
/// Mapeia: Req 3, PBT-02, design §5.1, DD-006.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro a ser inativado.</param>
/// <param name="TenantId">Tenant do contexto.</param>
/// <param name="ActorId">Usuário que solicitou a inativação.</param>
/// <param name="CorrelationId">Identificador de correlação da requisição.</param>
public sealed record DeactivatePartnerCommand(
    Guid PartnerId,
    Guid TenantId,
    Guid ActorId,
    string? CorrelationId = null
) : IRequest<DeactivatePartnerResult>;

/// <summary>
/// Resultado da inativação de um parceiro.
/// </summary>
/// <param name="TransitionEffective">
/// Verdadeiro se houve transição efetiva (parceiro estava ativo).
/// Falso se o parceiro já estava inativo (operação idempotente).
/// </param>
public sealed record DeactivatePartnerResult(bool TransitionEffective);
