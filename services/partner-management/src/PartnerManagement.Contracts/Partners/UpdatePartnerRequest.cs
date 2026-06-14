using System.ComponentModel.DataAnnotations;

namespace PartnerManagement.Contracts.Partners;

/// <summary>
/// DTO de request para atualizar um parceiro existente (PATCH).
/// Todos os campos são opcionais; apenas os fornecidos (não-nulos) são aplicados.
/// Mapeia: design §8, Req 2, TASK-22.
/// </summary>
public sealed class UpdatePartnerRequest
{
    /// <summary>Novo nome do parceiro (opcional).</summary>
    [MaxLength(200)]
    public string? Name { get; init; }

    /// <summary>Novo papel tipado canônico (opcional).</summary>
    [MaxLength(100)]
    public string? Role { get; init; }

    /// <summary>Novo percentual de setup em [0,00; 100,00] (opcional).</summary>
    [Range(typeof(decimal), "0", "100", ErrorMessage = "PM-ERR-003")]
    public decimal? PctSetup { get; init; }

    /// <summary>Novo percentual de recorrência em [0,00; 100,00] (opcional).</summary>
    [Range(typeof(decimal), "0", "100", ErrorMessage = "PM-ERR-003")]
    public decimal? PctRecorrente { get; init; }

    /// <summary>Novo e-mail de contato (opcional; <c>null</c> remove o e-mail).</summary>
    [EmailAddress(ErrorMessage = "PM-ERR-004")]
    [MaxLength(320)]
    public string? ContactEmail { get; init; }

    /// <summary>Novo telefone de contato (opcional; <c>null</c> remove o telefone).</summary>
    [MaxLength(20)]
    public string? ContactPhone { get; init; }

    /// <summary>Novas observações (opcional).</summary>
    [MaxLength(2000)]
    public string? Notes { get; init; }
}
