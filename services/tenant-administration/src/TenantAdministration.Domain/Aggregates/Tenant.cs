using TenantAdministration.Domain.Entities;
using TenantAdministration.Domain.Events;
using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Domain.Aggregates;

/// <summary>
/// Raiz do agregado de administração de tenant.
/// Fronteira transacional que abrange <see cref="TenantBranding"/> (1:1).
/// Invariantes: slug imutável e único; transições de estado válidas;
/// branding white-label estrito; cores válidas; fuso IANA válido.
/// </summary>
public sealed class Tenant
{
    private readonly List<IDomainEvent> _domainEvents = [];

    // ──────────────────────────────────────────────
    // Identidade
    // ──────────────────────────────────────────────

    /// <summary>Identificador único do tenant (UUID).</summary>
    public Guid Id { get; private set; }

    /// <summary>Slug imutável do tenant. Sem setter público.</summary>
    public Slug Slug { get; private set; }

    // ──────────────────────────────────────────────
    // Atributos
    // ──────────────────────────────────────────────

    /// <summary>Nome de exibição do tenant.</summary>
    public string DisplayName { get; private set; }

    /// <summary>Fuso horário IANA do tenant.</summary>
    public TimezoneIana Timezone { get; private set; }

    /// <summary>Horário do digest no fuso do tenant.</summary>
    public DigestTime DigestTime { get; private set; }

    /// <summary>Identificador do tenant no Identity Platform (GCP).</summary>
    public string? IdentityTenantId { get; private set; }

    // ──────────────────────────────────────────────
    // Estado
    // ──────────────────────────────────────────────

    /// <summary>Estado atual da state machine do tenant.</summary>
    public TenantStatus Status { get; private set; }

    /// <summary>Projeção de <see cref="Status"/> conforme DD-002.</summary>
    public bool Active => Status == TenantStatus.Provisioned;

    // ──────────────────────────────────────────────
    // Branding (entidade interna 1:1)
    // ──────────────────────────────────────────────

    /// <summary>Branding do tenant. Nulo até a primeira atualização.</summary>
    public TenantBranding? Branding { get; private set; }

    // ──────────────────────────────────────────────
    // Auditoria
    // ──────────────────────────────────────────────

    /// <summary>Data de provisionamento.</summary>
    public DateTimeOffset ProvisionedAt { get; private set; }

    /// <summary>Data de criação do registro.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Data da última atualização.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    // ──────────────────────────────────────────────
    // Construtor privado (somente Provision cria)
    // ──────────────────────────────────────────────

    // Construtor privado para uso exclusivo da factory Provision.
    // Os campos não-anuláveis são garantidamente inicializados antes de qualquer
    // instância ser exposta ao caller.
#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private Tenant() { }
#pragma warning restore CS8618

    // ──────────────────────────────────────────────
    // Comportamentos (factory + métodos de domínio)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Provisiona um novo tenant. Único caminho de criação do agregado.
    /// Enfileira <see cref="TenantProvisioned"/>.
    /// </summary>
    /// <param name="slug">Slug único e imutável.</param>
    /// <param name="displayName">Nome de exibição.</param>
    /// <param name="timezone">Fuso horário IANA.</param>
    /// <param name="digestTime">Horário do digest.</param>
    /// <param name="adminEmail">E-mail do administrador inicial (semente do IdP).</param>
    /// <param name="now">Instante atual (injetado para testabilidade).</param>
    public static Tenant Provision(
        Slug slug,
        string displayName,
        TimezoneIana timezone,
        DigestTime digestTime,
        string adminEmail,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminEmail);

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            DisplayName = displayName,
            Timezone = timezone,
            DigestTime = digestTime,
            Status = TenantStatus.Provisioned,
            ProvisionedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        tenant._domainEvents.Add(new TenantProvisioned(
            tenant.Id,
            slug.Value,
            displayName,
            timezone.Value,
            digestTime.Value,
            now));

        return tenant;
    }

    /// <summary>
    /// Suspende o tenant. Exige <see cref="TenantStatus.Provisioned"/>.
    /// Lança <see cref="InvalidOperationException"/> com TA-ERR-007 se já suspenso.
    /// Enfileira <see cref="TenantSuspended"/>.
    /// </summary>
    public void Suspend(DateTimeOffset now)
    {
        if (Status != TenantStatus.Provisioned)
            throw new InvalidOperationException(
                $"TA-ERR-007: Transição de estado inválida. Tenant está em '{Status}', não pode ser suspenso.");

        Status = TenantStatus.Suspended;
        UpdatedAt = now;

        _domainEvents.Add(new TenantSuspended(Id, Slug.Value, now));
    }

    /// <summary>
    /// Reativa o tenant. Exige <see cref="TenantStatus.Suspended"/>.
    /// Lança <see cref="InvalidOperationException"/> com TA-ERR-007 se já ativo.
    /// Enfileira <see cref="TenantReactivated"/>.
    /// </summary>
    public void Reactivate(DateTimeOffset now)
    {
        if (Status != TenantStatus.Suspended)
            throw new InvalidOperationException(
                $"TA-ERR-007: Transição de estado inválida. Tenant está em '{Status}', não pode ser reativado.");

        Status = TenantStatus.Provisioned;
        UpdatedAt = now;

        _domainEvents.Add(new TenantReactivated(Id, Slug.Value, now));
    }

    /// <summary>
    /// Atualiza o branding do tenant.
    /// Enfileira <see cref="BrandingChanged"/>.
    /// </summary>
    /// <param name="theme">Tema de branding white-label estrito.</param>
    /// <param name="wcagContrastOk">Resultado da validação WCAG (calculado pelo handler).</param>
    /// <param name="contrastRatio">Razão de contraste calculada.</param>
    /// <param name="now">Instante atual.</param>
    public void UpdateBranding(BrandingTheme theme, bool wcagContrastOk, decimal? contrastRatio, DateTimeOffset now)
    {
        if (Branding is null)
            Branding = new TenantBranding(theme, wcagContrastOk, contrastRatio, now);
        else
            Branding.Update(theme, wcagContrastOk, contrastRatio, now);

        UpdatedAt = now;

        _domainEvents.Add(new BrandingChanged(Id, Slug.Value, wcagContrastOk, now));
    }

    /// <summary>
    /// Atualiza a configuração de fuso e horário do digest.
    /// Enfileira <see cref="DigestConfigChanged"/>.
    /// </summary>
    public void UpdateDigestConfig(TimezoneIana timezone, DigestTime digestTime, DateTimeOffset now)
    {
        Timezone = timezone;
        DigestTime = digestTime;
        UpdatedAt = now;

        _domainEvents.Add(new DigestConfigChanged(Id, timezone.Value, digestTime.Value, now));
    }

    /// <summary>
    /// Define o ID do tenant no Identity Platform após o provisionamento externo.
    /// Visível à Infrastructure para completar o resultado da saga.
    /// </summary>
    public void SetIdentityTenantId(string identityTenantId)
    {
        IdentityTenantId = identityTenantId;
    }

    // ──────────────────────────────────────────────
    // Domain Events
    // ──────────────────────────────────────────────

    /// <summary>Eventos de domínio enfileirados. Somente leitura.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Limpa a fila de eventos de domínio após consumo pelo handler.
    /// Não expõe a lista mutável.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
