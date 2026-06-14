namespace AccountManagement.Application.Ports;

/// <summary>
/// Porta de leitura para atividades do módulo activity-management.
///
/// Usada pelo <see cref="AccountManagement.Application.Accounts.Queries.GetAccount360.GetAccount360Handler"/>
/// para compor a visão 360° da conta (DD-004, Req 6).
/// A implementação concreta na Infrastructure faz chamada HTTP/gRPC interna via mTLS (design §6.4).
///
/// Mapeia: design §5 (Ports), design §6.4, Req 6, DD-004.
/// </summary>
public interface IActivityReadPort
{
    /// <summary>
    /// Retorna atividades associadas à conta, ordenadas por data decrescente.
    /// </summary>
    /// <param name="accountId">Identificador da conta.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de atividades da conta.</returns>
    Task<IReadOnlyList<ActivityReadModel>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Modelo de leitura de atividade retornado pelo port (sem PII sensível).
/// </summary>
/// <param name="ActivityId">Identificador único da atividade.</param>
/// <param name="Type">Tipo de atividade (reunião, ligação, e-mail, etc.).</param>
/// <param name="Summary">Resumo da atividade.</param>
/// <param name="OccurredAt">Data e hora da atividade (UTC).</param>
public sealed record ActivityReadModel(
    Guid ActivityId,
    string Type,
    string Summary,
    DateTimeOffset OccurredAt);
