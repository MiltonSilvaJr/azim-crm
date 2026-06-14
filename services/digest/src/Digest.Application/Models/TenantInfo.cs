namespace Digest.Application.Models;

/// <summary>
/// Informações do tenant necessárias para seleção de elegibilidade (design §5.2, Req 2).
/// Retornado por <see cref="Ports.IUserDirectoryPort.GetActiveTenantInfosAsync"/>.
/// </summary>
/// <param name="TenantId">Identificador do tenant.</param>
/// <param name="IanaTimezone">Fuso IANA configurado no tenant (ex.: "America/Sao_Paulo").</param>
/// <param name="DigestTime">Horário local desejado do digest (ex.: 07:00).</param>
/// <param name="Active">Indica se o tenant está ativo.</param>
public sealed record TenantInfo(
    Guid TenantId,
    string IanaTimezone,
    TimeOnly DigestTime,
    bool Active);
