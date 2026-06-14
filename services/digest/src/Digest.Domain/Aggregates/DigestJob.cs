using Digest.Domain.Enums;
using Digest.Domain.Events;
using Digest.Domain.ValueObjects;

namespace Digest.Domain.Aggregates;

/// <summary>
/// Aggregate root efêmero que representa a execução do digest para um (<c>tenant_id</c>, <c>digest_date</c>).
/// Opera em memória durante o processamento; coordena destinatários, composição e emissão de eventos (design §4.1).
/// </summary>
/// <remarks>
/// Invariantes:
/// <list type="bullet">
///   <item>Sempre associado a exatamente um <c>tenant_id</c> e um <c>digest_date</c> (Req 4.1).</item>
///   <item>Azimute incluído apenas quando <c>digest_date</c> é segunda-feira e papel é gestão (RN-029, Req 5.7, PBT-06).</item>
///   <item>Exatamente um <see cref="DigestEmailSent"/> por chamada a <see cref="RegisterSent"/> (RNF 10.1).</item>
/// </list>
/// Sem dependência de EF Core ou infraestrutura.
/// </remarks>
public sealed class DigestJob
{
    private readonly List<DigestEmailSent> _domainEvents = [];

    /// <summary>Identificador do tenant processado neste job.</summary>
    public Guid TenantId { get; }

    /// <summary>Data local do digest no fuso do tenant.</summary>
    public DigestDate DigestDate { get; }

    /// <summary>Eventos de domínio acumulados durante o processamento.</summary>
    public IReadOnlyList<DigestEmailSent> DomainEvents => _domainEvents.AsReadOnly();

    private DigestJob(Guid tenantId, DigestDate digestDate)
    {
        TenantId = tenantId;
        DigestDate = digestDate;
    }

    /// <summary>
    /// Cria um novo <see cref="DigestJob"/> para o tenant e data especificados.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Não pode ser vazio.</param>
    /// <param name="digestDate">Data local do tenant (não UTC).</param>
    /// <exception cref="ArgumentException">Quando <paramref name="tenantId"/> é vazio.</exception>
    public static DigestJob Create(Guid tenantId, DigestDate digestDate)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser vazio.", nameof(tenantId));
        ArgumentNullException.ThrowIfNull(digestDate);

        return new DigestJob(tenantId, digestDate);
    }

    /// <summary>
    /// Determina se o bloco de azimute deve ser incluído para o papel especificado.
    /// Regra: <c>digest_date</c> deve ser segunda-feira E papel deve ser gestão (Req 5.7, RN-029, PBT-06).
    /// </summary>
    public bool ShouldIncludeAzimute(RecipientPapel papel)
    {
        var isGestao = papel is RecipientPapel.GestorBU or RecipientPapel.TAdmin;
        return DigestDate.IsMonday() && isGestao;
    }

    /// <summary>
    /// Registra o envio bem-sucedido de um digest para um usuário.
    /// Emite exatamente um <see cref="DigestEmailSent"/> (RNF 10.1).
    /// </summary>
    /// <param name="userId">Identificador do usuário destinatário.</param>
    /// <param name="messageId">Identificador da mensagem no provedor (sem PII).</param>
    public void RegisterSent(Guid userId, string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var evt = new DigestEmailSent(
            TenantId: TenantId,
            UserId: userId,
            DigestDate: DigestDate,
            MessageId: messageId,
            OccurredAt: DateTimeOffset.UtcNow);

        _domainEvents.Add(evt);
    }

    /// <summary>
    /// Limpa os domain events acumulados (após publicação pelo Outbox).
    /// </summary>
    public void ClearEvents() => _domainEvents.Clear();
}
