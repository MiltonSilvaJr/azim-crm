namespace TenantAdministration.Application.Dtos;

/// <summary>
/// DTO de tons derivados para retorno da camada de Application à Api.
/// Espelha <c>TenantAdministration.Domain.ValueObjects.DerivedTones</c>
/// sem criar dependência de Domain na camada de Api.
/// design.md §5.2 e §5.3 (Req 8).
/// </summary>
public sealed record DerivedTonesDto(
    string PrimaryHover,
    string PrimaryActive,
    string PrimaryMuted,
    string SecondaryHover,
    string SecondaryActive,
    string SecondaryMuted);
