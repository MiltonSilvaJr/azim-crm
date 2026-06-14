using Digest.Application.Models;
using Digest.Domain.ValueObjects;
using MediatR;

namespace Digest.Application.Queries;

/// <summary>
/// Query que seleciona os destinatários elegíveis do digest para um tenant e data específicos.
/// Aplica <see cref="Domain.Policies.RecipientSelectionPolicy"/> a cada usuário ativo (design §5.2, Req 3).
/// </summary>
/// <param name="TenantId">Identificador do tenant.</param>
/// <param name="DigestDate">Data local do digest no fuso do tenant.</param>
public sealed record SelectRecipientsQuery(Guid TenantId, DigestDate DigestDate)
    : IRequest<IReadOnlyList<RecipientCandidate>>;
