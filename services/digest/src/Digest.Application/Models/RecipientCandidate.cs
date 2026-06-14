using Digest.Domain.Enums;
using Digest.Domain.Policies;

namespace Digest.Application.Models;

/// <summary>
/// Candidato a destinatário do digest selecionado por <c>SelectRecipientsQuery</c>.
/// Carrega o resultado da <see cref="RecipientSelectionPolicy"/> para cada usuário (design §5.2, Req 3).
/// </summary>
/// <param name="UserId">Identificador do usuário.</param>
/// <param name="TenantId">Identificador do tenant.</param>
/// <param name="Papel">Papel do usuário no tenant.</param>
/// <param name="SelectionResult">Resultado da política — indica quais blocos o usuário deve receber.</param>
public sealed record RecipientCandidate(
    Guid UserId,
    Guid TenantId,
    RecipientPapel Papel,
    RecipientSelectionResult SelectionResult);
