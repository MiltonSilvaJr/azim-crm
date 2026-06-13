namespace TenantAdministration.Domain.ValueObjects;

/// <summary>
/// Conjunto determinístico de variações de tom derivadas das cores primária e secundária.
/// Calculado por <see cref="TenantAdministration.Domain.Policies.ToneDerivationService"/>.
/// Não faz parte do estado persistido — é projeção calculada sob demanda.
/// </summary>
public sealed record DerivedTones(
    string PrimaryHover,
    string PrimaryActive,
    string PrimaryMuted,
    string SecondaryHover,
    string SecondaryActive,
    string SecondaryMuted
);
