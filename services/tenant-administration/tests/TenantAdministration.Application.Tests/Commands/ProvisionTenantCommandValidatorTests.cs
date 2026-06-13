using FluentAssertions;
using FluentValidation.TestHelper;
using TenantAdministration.Application.Commands;
using Xunit;

namespace TenantAdministration.Application.Tests.Commands;

public sealed class ProvisionTenantCommandValidatorTests
{
    private readonly ProvisionTenantCommandValidator _validator = new();

    [Fact(DisplayName = "Command válido: sem erros de validação")]
    public void ValidCommand_ShouldHaveNoErrors()
    {
        var cmd = new ProvisionTenantCommand(
            Slug: "meu-tenant",
            SlugConfirmation: "meu-tenant",
            DisplayName: "Meu Tenant",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            AdminEmail: "admin@meutenant.com.br");

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact(DisplayName = "Slug ausente: TA-ERR-001")]
    public void EmptySlug_ShouldHaveError_TA_ERR_001()
    {
        var cmd = new ProvisionTenantCommand("", "", "Nome", "America/Sao_Paulo", "07:00", "a@b.com");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorCode("TA-ERR-001");
    }

    [Fact(DisplayName = "SlugConfirmation diverge: TA-ERR-003")]
    public void SlugConfirmationMismatch_ShouldHaveError_TA_ERR_003()
    {
        var cmd = new ProvisionTenantCommand("meu-tenant", "outro", "Nome", "America/Sao_Paulo", "07:00", "a@b.com");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SlugConfirmation)
            .WithErrorCode("TA-ERR-003");
    }

    [Fact(DisplayName = "AdminEmail ausente: erro de validação")]
    public void EmptyAdminEmail_ShouldHaveValidationError()
    {
        var cmd = new ProvisionTenantCommand("slug", "slug", "Nome", "America/Sao_Paulo", "07:00", "");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.AdminEmail);
    }

    [Fact(DisplayName = "AdminEmail inválido: erro de validação")]
    public void InvalidAdminEmail_ShouldHaveValidationError()
    {
        var cmd = new ProvisionTenantCommand("slug", "slug", "Nome", "America/Sao_Paulo", "07:00", "nao-e-email");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.AdminEmail);
    }

    [Fact(DisplayName = "DisplayName ausente: TA-ERR-001")]
    public void EmptyDisplayName_ShouldHaveError_TA_ERR_001()
    {
        var cmd = new ProvisionTenantCommand("slug", "slug", "", "America/Sao_Paulo", "07:00", "a@b.com");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorCode("TA-ERR-001");
    }
}
