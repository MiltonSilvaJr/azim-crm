namespace Organization.Contracts.Events;

/// <summary>
/// Carga do evento de integração <c>bu.created.v1</c>.
/// Publicado quando uma Business Unit é criada com sucesso.
/// Sem PII: apenas identificadores e nome da BU (RNF 3, DD-005).
/// </summary>
/// <param name="TenantId">Identificador do tenant dono da BU.</param>
/// <param name="BuId">Identificador único da Business Unit criada.</param>
/// <param name="Name">Nome da Business Unit (não contém dados pessoais).</param>
public sealed record BuCreatedV1Payload(
    Guid TenantId,
    Guid BuId,
    string Name);
