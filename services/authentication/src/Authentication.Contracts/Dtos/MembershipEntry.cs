using System.Text.Json.Serialization;

namespace Authentication.Contracts.Dtos;

/// <summary>
/// Entrada de membership: associação entre unidade de negócio e papel do usuário.
///
/// Usado em <see cref="MeResponse"/> para listar as unidades de negócio às quais
/// o usuário pertence e o papel correspondente.
///
/// Mapeia: design.md § 8.1, design.md § 4.3 (MembershipSet), TASK-17.
/// </summary>
public sealed class MembershipEntry
{
    /// <summary>Identificador da unidade de negócio.</summary>
    [JsonPropertyName("bu_id")]
    public Guid BuId { get; set; }

    /// <summary>Papel do usuário na unidade de negócio.</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}
