using Digest.Domain.Enums;

namespace Digest.Application.Models;

/// <summary>
/// Informações de usuário necessárias para seleção de destinatários (design §6.4, Req 3).
/// Retornado por <see cref="Ports.IUserDirectoryPort.GetActiveUsersAsync"/>.
/// </summary>
/// <param name="UserId">Identificador do usuário.</param>
/// <param name="TenantId">Identificador do tenant ao qual o usuário pertence.</param>
/// <param name="Papel">Papel do usuário no tenant.</param>
/// <param name="Active">Indica se o usuário está ativo no tenant.</param>
public sealed record UserInfo(
    Guid UserId,
    Guid TenantId,
    RecipientPapel Papel,
    bool Active);
