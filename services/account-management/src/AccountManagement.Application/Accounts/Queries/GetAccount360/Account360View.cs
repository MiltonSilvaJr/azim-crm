using AccountManagement.Domain.Accounts;

namespace AccountManagement.Application.Accounts.Queries.GetAccount360;

/// <summary>
/// Visão composta 360° de uma conta.
///
/// Agrega dados do agregado <see cref="Account"/> com oportunidades e atividades
/// de módulos externos via portas de leitura (DD-004, Req 6).
///
/// Seções de dados externos (<see cref="Opportunities"/>, <see cref="Activities"/>)
/// podem estar indisponíveis quando a porta downstream falhar — veja
/// <see cref="SectionAvailability"/> para sinalização de degradação parcial (design §15).
///
/// Mapeia: design §5.2, §8, Req 6, PBT-05, DD-004.
/// </summary>
public sealed record Account360View
{
    /// <summary>Conta central da visão 360°.</summary>
    public Account Account { get; init; } = null!;

    /// <summary>Contatos ativos da conta (PII acessível após PiiAccessBehavior).</summary>
    public IReadOnlyList<Contact> Contacts { get; init; } = [];

    /// <summary>
    /// Oportunidades filtradas pelo escopo de BUs do usuário (PBT-05).
    /// <c>null</c> quando a porta <see cref="IOpportunityReadPort"/> estiver indisponível.
    /// </summary>
    public IReadOnlyList<OpportunityReadModel>? Opportunities { get; init; }

    /// <summary>
    /// Atividades da conta.
    /// <c>null</c> quando a porta <see cref="IActivityReadPort"/> estiver indisponível.
    /// </summary>
    public IReadOnlyList<ActivityReadModel>? Activities { get; init; }

    /// <summary>
    /// Disponibilidade de cada seção externa. Permite que a API sinalize
    /// degradação parcial sem retornar erro HTTP (design §15, DD-004).
    /// </summary>
    public SectionAvailability Availability { get; init; } = SectionAvailability.AllAvailable;
}

/// <summary>
/// Indica a disponibilidade das seções externas da visão 360°.
/// </summary>
/// <param name="OpportunitiesAvailable">
/// <c>true</c> quando a porta de oportunidades respondeu com sucesso.
/// </param>
/// <param name="ActivitiesAvailable">
/// <c>true</c> quando a porta de atividades respondeu com sucesso.
/// </param>
public sealed record SectionAvailability(
    bool OpportunitiesAvailable,
    bool ActivitiesAvailable)
{
    /// <summary>Instância de disponibilidade total (ambas as seções disponíveis).</summary>
    public static readonly SectionAvailability AllAvailable =
        new(OpportunitiesAvailable: true, ActivitiesAvailable: true);

    /// <summary>Indica se alguma seção está degradada.</summary>
    public bool IsPartiallyDegraded => !OpportunitiesAvailable || !ActivitiesAvailable;
}
