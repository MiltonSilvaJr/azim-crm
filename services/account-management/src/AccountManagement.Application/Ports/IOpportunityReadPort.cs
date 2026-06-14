namespace AccountManagement.Application.Ports;

/// <summary>
/// Porta de leitura para oportunidades do módulo opportunity-pipeline.
///
/// Usada pelo <see cref="AccountManagement.Application.Accounts.Queries.GetAccount360.GetAccount360Handler"/>
/// para compor a visão 360° da conta (DD-004, Req 6).
/// A implementação concreta na Infrastructure faz chamada HTTP/gRPC interna via mTLS (design §6.4).
///
/// O escopo de BUs do usuário é passado explicitamente para garantir que apenas oportunidades
/// autorizadas sejam retornadas (Req 6.2/6.3, PBT-05).
///
/// Mapeia: design §5 (Ports), design §6.4, Req 6, PBT-05, DD-004.
/// </summary>
public interface IOpportunityReadPort
{
    /// <summary>
    /// Retorna oportunidades associadas à conta, filtradas pelo escopo de BUs do usuário.
    /// </summary>
    /// <param name="accountId">Identificador da conta.</param>
    /// <param name="authorizedBuIds">
    /// Conjunto de BUs às quais o usuário tem acesso.
    /// Apenas oportunidades dentro desse escopo são retornadas (PBT-05).
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de oportunidades dentro do escopo de BUs autorizadas.</returns>
    Task<IReadOnlyList<OpportunityReadModel>> GetByAccountAsync(
        Guid accountId,
        IReadOnlySet<Guid> authorizedBuIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Modelo de leitura de oportunidade retornado pelo port (sem PII).
/// </summary>
/// <param name="OpportunityId">Identificador único da oportunidade.</param>
/// <param name="BuId">BU proprietária da oportunidade.</param>
/// <param name="Title">Título da oportunidade.</param>
/// <param name="Stage">Estágio atual.</param>
/// <param name="Value">Valor estimado em centavos (sem PII).</param>
public sealed record OpportunityReadModel(
    Guid OpportunityId,
    Guid BuId,
    string Title,
    string Stage,
    long Value);
