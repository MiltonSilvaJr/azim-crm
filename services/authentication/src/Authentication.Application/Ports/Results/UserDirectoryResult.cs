using Authentication.Domain.ValueObjects;

namespace Authentication.Application.Ports.Results;

/// <summary>
/// Resultado da resolução de usuário pelo <see cref="IUserDirectory"/>.
///
/// Nunca contém <c>identity_uid</c>; expõe apenas dados internos do módulo
/// <c>organization</c> (DD-001).
///
/// Mapeia: design.md § 6.1, DD-001, Req 5.
/// </summary>
public sealed record UserDirectoryResult
{
    /// <summary>Identificador interno do usuário no tenant (UUID do módulo organization).</summary>
    public required Guid UserId { get; init; }

    /// <summary>E-mail do usuário conforme registrado no organization.</summary>
    public required string Email { get; init; }

    /// <summary>Papéis globais do usuário no tenant.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>Conjunto de memberships (unidade de negócio → papel).</summary>
    public required MembershipSet Memberships { get; init; }

    /// <summary>Indica se o usuário está ativo no tenant.</summary>
    public required bool IsActive { get; init; }
}
