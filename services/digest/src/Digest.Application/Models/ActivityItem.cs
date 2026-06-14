namespace Digest.Application.Models;

/// <summary>
/// Item de atividade retornado pelas portas de leitura do digest (design §6.4, Req 4).
/// Sem PII: não contém nome do proprietário, apenas identificadores opacos (RNF 3).
/// </summary>
/// <param name="ActivityId">Identificador da atividade.</param>
/// <param name="OwnerId">Identificador do proprietário (user_id).</param>
/// <param name="DueDate">Data de vencimento da atividade.</param>
/// <param name="Title">Título da atividade (usado na composição; não logado — DD-011).</param>
public sealed record ActivityItem(
    Guid ActivityId,
    Guid OwnerId,
    DateOnly DueDate,
    string Title);
