namespace Organization.Contracts.Events;

/// <summary>
/// Carga do evento de integração <c>user.activated.v1</c>.
/// Publicado quando um usuário é ativado (aceite de convite ou provisionamento inicial).
/// Sem PII: apenas identificadores e papéis por BU — sem e-mail ou <c>display_name</c> (RNF 3, DD-005).
/// Consumido pelo módulo <c>authentication</c> para carregar memberships no token.
/// </summary>
/// <param name="TenantId">Identificador do tenant dono do usuário.</param>
/// <param name="UserId">Identificador único do usuário ativado.</param>
/// <param name="Memberships">Relação de papéis por BU atribuídos ao usuário no momento da ativação.</param>
public sealed record UserActivatedV1Payload(
    Guid TenantId,
    Guid UserId,
    IReadOnlyList<MembershipEntry> Memberships);

/// <summary>
/// Entrada de membership: par <c>(BuId, Role)</c> sem dados pessoais.
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
/// <param name="Role">Papel do usuário na BU (ex.: <c>Vendedor</c>).</param>
public sealed record MembershipEntry(Guid BuId, string Role);
