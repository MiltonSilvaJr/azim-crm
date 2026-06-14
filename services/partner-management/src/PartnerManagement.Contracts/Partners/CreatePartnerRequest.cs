using System.ComponentModel.DataAnnotations;

namespace PartnerManagement.Contracts.Partners;

/// <summary>
/// DTO de request para criar um novo parceiro.
/// Mapeia: design §8, Req 1, Req 5, Req 6, Req 7, TASK-22.
/// </summary>
public sealed class CreatePartnerRequest
{
    /// <summary>Nome do parceiro (obrigatório).</summary>
    [Required(ErrorMessage = "PM-ERR-001")]
    [MaxLength(200)]
    public string Name { get; init; } = null!;

    /// <summary>Papel tipado canônico do parceiro (obrigatório).</summary>
    [Required(ErrorMessage = "PM-ERR-002")]
    [MaxLength(100)]
    public string Role { get; init; } = null!;

    /// <summary>Percentual padrão de setup em [0,00; 100,00] com 2 casas decimais.</summary>
    [Range(typeof(decimal), "0", "100", ErrorMessage = "PM-ERR-003")]
    public decimal PctSetup { get; init; } = 0m;

    /// <summary>Percentual padrão de recorrência em [0,00; 100,00] com 2 casas decimais.</summary>
    [Range(typeof(decimal), "0", "100", ErrorMessage = "PM-ERR-003")]
    public decimal PctRecorrente { get; init; } = 0m;

    /// <summary>E-mail de contato do parceiro (opcional). Validação de formato no domínio.</summary>
    [EmailAddress(ErrorMessage = "PM-ERR-004")]
    [MaxLength(320)]
    public string? ContactEmail { get; init; }

    /// <summary>Telefone de contato do parceiro (opcional).</summary>
    [MaxLength(20)]
    public string? ContactPhone { get; init; }

    /// <summary>Observações livres sobre o parceiro (opcional).</summary>
    [MaxLength(2000)]
    public string? Notes { get; init; }

    /// <summary>
    /// Confirma criação mesmo que nome duplicado tenha sido alertado (MSG-021, Req 1.7).
    /// Quando <c>true</c>, a criação prossegue mesmo com alerta de duplicidade.
    /// </summary>
    public bool ConfirmCreateDespiteDuplicate { get; init; } = false;
}
