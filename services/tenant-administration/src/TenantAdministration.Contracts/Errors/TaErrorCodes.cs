namespace TenantAdministration.Contracts.Errors;

/// <summary>
/// Catálogo canônico de códigos de erro do módulo tenant-administration.
/// Todos os endpoints referenciam exclusivamente estes códigos (design.md §12).
/// </summary>
public static class TaErrorCodes
{
    /// <summary>TA-ERR-001: Campos obrigatórios ausentes no provisionamento.</summary>
    public const string RequiredFieldsMissing = "TA-ERR-001";

    /// <summary>TA-ERR-002: Slug já está em uso.</summary>
    public const string SlugAlreadyInUse = "TA-ERR-002";

    /// <summary>TA-ERR-003: Confirmação de slug não corresponde.</summary>
    public const string SlugConfirmationMismatch = "TA-ERR-003";

    /// <summary>TA-ERR-004: Cor inválida; use formato #RRGGBB.</summary>
    public const string ColorInvalidFormat = "TA-ERR-004";

    /// <summary>TA-ERR-005: Slug em formato inválido; use apenas minúsculas e hífens.</summary>
    public const string SlugInvalidFormat = "TA-ERR-005";

    /// <summary>TA-ERR-006: Fuso horário IANA inválido.</summary>
    public const string TimezoneInvalid = "TA-ERR-006";

    /// <summary>TA-ERR-007: Transição de estado inválida.</summary>
    public const string InvalidStateTransition = "TA-ERR-007";

    /// <summary>TA-ERR-008: Tenant não encontrado.</summary>
    public const string TenantNotFound = "TA-ERR-008";

    /// <summary>TA-ERR-009: Falha ao criar tenant de identidade.</summary>
    public const string IdentityProvisioningFailed = "TA-ERR-009";

    /// <summary>TA-ERR-010: Provisionamento revertido por falha de persistência.</summary>
    public const string ProvisioningReverted = "TA-ERR-010";

    /// <summary>TA-ERR-011: Slug não pode ser alterado.</summary>
    public const string SlugImmutable = "TA-ERR-011";

    /// <summary>TA-ERR-012: Arquivo excede 1 MB.</summary>
    public const string AssetTooLarge = "TA-ERR-012";

    /// <summary>TA-ERR-013: Formato de arquivo não suportado.</summary>
    public const string AssetFormatUnsupported = "TA-ERR-013";

    /// <summary>TA-ERR-014: Contraste insuficiente para WCAG AA.</summary>
    public const string WcagContrastInsufficient = "TA-ERR-014";
}
