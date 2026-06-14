using Digest.Application.Ports;
using Digest.Domain.ValueObjects;
using MediatR;
using NodaTime;

namespace Digest.Application.Queries;

/// <summary>
/// Handler de <see cref="SelectEligibleTenantsQuery"/>.
/// Para cada tenant ativo, instancia <see cref="TenantEligibility"/> e avalia elegibilidade
/// pelo fuso IANA e horário local (Req 2, PBT-01, DD-012).
/// </summary>
public sealed class SelectEligibleTenantsQueryHandler
    : IRequestHandler<SelectEligibleTenantsQuery, IReadOnlyList<Guid>>
{
    private readonly IUserDirectoryPort _userDirectory;
    private readonly IDateTimeZoneProvider _zoneProvider;

    /// <summary>
    /// Constrói o handler com as dependências necessárias.
    /// </summary>
    /// <param name="userDirectory">Porta de leitura do diretório de tenants.</param>
    /// <param name="zoneProvider">Provedor TZDB injetável (para testabilidade — DD-012).</param>
    public SelectEligibleTenantsQueryHandler(
        IUserDirectoryPort userDirectory,
        IDateTimeZoneProvider zoneProvider)
    {
        _userDirectory = userDirectory;
        _zoneProvider = zoneProvider;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> Handle(
        SelectEligibleTenantsQuery request,
        CancellationToken cancellationToken)
    {
        var tenants = await _userDirectory.GetActiveTenantInfosAsync(cancellationToken);

        var utcInstant = Instant.FromDateTimeOffset(request.ReferenceUtc);
        var eligible = new List<Guid>();

        foreach (var tenant in tenants)
        {
            if (!tenant.Active)
                continue;

            try
            {
                var digestTime = new LocalTime(tenant.DigestTime.Hour, tenant.DigestTime.Minute);
                var eligibility = new TenantEligibility(
                    utcInstant,
                    tenant.IanaTimezone,
                    digestTime,
                    tenant.Active,
                    _zoneProvider);

                if (eligibility.IsEligible())
                    eligible.Add(tenant.TenantId);
            }
            catch (ArgumentException)
            {
                // Fuso IANA inválido — ignora o tenant (não deve bloquear os demais)
            }
        }

        return eligible.AsReadOnly();
    }
}
