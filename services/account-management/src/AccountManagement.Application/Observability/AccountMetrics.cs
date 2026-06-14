namespace AccountManagement.Application.Observability;

/// <summary>
/// Contadores de métricas do módulo account-management.
///
/// Mantém os contadores em memória (thread-safe via Interlocked).
/// A camada Infrastructure expõe esses contadores via Prometheus
/// (<c>prometheus-net</c>) nos endpoints <c>/metrics</c> (TASK-16, design §11).
///
/// Métricas obrigatórias (RNF 9.2, design §11):
/// - <c>accounts_created_total</c>
/// - <c>contacts_created_total</c>
/// - <c>dedupe_blocked_total</c>
/// - <c>domain_events_published_total</c>
///
/// Mapeia: TASK-16 ST-01/ST-03, design §11, RNF 9.2.
/// </summary>
public sealed class AccountMetrics
{
    private long _accountsCreatedTotal;
    private long _contactsCreatedTotal;
    private long _dedupeBlockedTotal;
    private long _domainEventsPublishedTotal;

    /// <summary>Incrementa <c>accounts_created_total</c> em 1.</summary>
    public void IncrementAccountsCreated()
        => Interlocked.Increment(ref _accountsCreatedTotal);

    /// <summary>Incrementa <c>contacts_created_total</c> em 1.</summary>
    public void IncrementContactsCreated()
        => Interlocked.Increment(ref _contactsCreatedTotal);

    /// <summary>
    /// Incrementa <c>dedupe_blocked_total</c> em 1.
    /// Chamado quando <see cref="SearchSimilarAccountsQuery"/> retorna ao menos um candidato.
    /// </summary>
    public void IncrementDedupeBlocked()
        => Interlocked.Increment(ref _dedupeBlockedTotal);

    /// <summary>
    /// Incrementa <c>domain_events_published_total</c> em 1.
    /// </summary>
    /// <param name="eventType">Tipo do evento publicado (ex.: "account.created.v1").</param>
    public void IncrementDomainEventsPublished(string eventType)
        => Interlocked.Increment(ref _domainEventsPublishedTotal);

    // =========================================================================
    // Leitura de contadores (usados em testes e pela exportação Prometheus)
    // =========================================================================

    /// <summary>Lê o total atual de <c>accounts_created_total</c>.</summary>
    public long GetAccountsCreatedTotal()
        => Interlocked.Read(ref _accountsCreatedTotal);

    /// <summary>Lê o total atual de <c>contacts_created_total</c>.</summary>
    public long GetContactsCreatedTotal()
        => Interlocked.Read(ref _contactsCreatedTotal);

    /// <summary>Lê o total atual de <c>dedupe_blocked_total</c>.</summary>
    public long GetDedupeBlockedTotal()
        => Interlocked.Read(ref _dedupeBlockedTotal);

    /// <summary>Lê o total atual de <c>domain_events_published_total</c>.</summary>
    public long GetDomainEventsPublishedTotal()
        => Interlocked.Read(ref _domainEventsPublishedTotal);
}
