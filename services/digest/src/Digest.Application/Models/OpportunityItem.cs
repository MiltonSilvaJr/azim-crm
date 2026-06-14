using Digest.Domain.ValueObjects;

namespace Digest.Application.Models;

/// <summary>
/// Item de oportunidade retornado pelas portas de leitura do digest (design §6.4, Req 4).
/// Valores monetários em centavos via <see cref="MoneyCents"/> (DD-010).
/// </summary>
/// <param name="OpportunityId">Identificador da oportunidade.</param>
/// <param name="OwnerId">Identificador do proprietário (user_id).</param>
/// <param name="Name">Nome da oportunidade (usado na composição; não logado — DD-011).</param>
/// <param name="ExpectedCloseDate">Data de fechamento previsto.</param>
/// <param name="WeightedValue">Valor ponderado pelo estágio em centavos.</param>
public sealed record OpportunityItem(
    Guid OpportunityId,
    Guid OwnerId,
    string Name,
    DateOnly? ExpectedCloseDate,
    MoneyCents WeightedValue);
