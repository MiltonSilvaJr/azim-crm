using FluentAssertions;
using NSubstitute;
using TenantAdministration.Application.Commands;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using Xunit;

namespace TenantAdministration.Application.Tests.Commands;

public sealed class ProvisionTenantHandlerTests
{
    private readonly ITenantRepository _repository = Substitute.For<ITenantRepository>();
    private readonly ITenantProvisioningSaga _saga = Substitute.For<ITenantProvisioningSaga>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private ProvisionTenantHandler CreateHandler()
    {
        _clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return new(_repository, _saga, _clock);
    }

    private static ProvisionTenantCommand ValidCommand(
        string slug = "meu-tenant",
        string? slugConfirmation = null,
        string timezone = "America/Sao_Paulo") =>
        new(
            Slug: slug,
            SlugConfirmation: slugConfirmation ?? slug,
            DisplayName: "Meu Tenant",
            Timezone: timezone,
            DigestTime: "07:00",
            AdminEmail: "admin@meutenant.com.br",
            IdempotencyKey: "idem-key-123");

    // ──────────────────────────────────────────────────────────────
    // Cenários de sucesso
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Provisionamento bem-sucedido retorna tenantId e slug")]
    public async Task ValidCommand_ShouldReturnTenantIdAndSlug()
    {
        _clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        _repository.ExistsSlugAsync("meu-tenant", Arg.Any<CancellationToken>()).Returns(false);
        _saga.ExecuteAsync(
            Arg.Any<Guid>(), "meu-tenant", "Meu Tenant",
            "America/Sao_Paulo", "07:00", "admin@meutenant.com.br",
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProvisioningResult(Guid.NewGuid(), "idp-tenant-id"));

        var handler = CreateHandler();
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.Slug.Should().Be("meu-tenant");
        result.Status.Should().Be("provisioned");
        result.TenantId.Should().NotBeEmpty();
    }

    // ──────────────────────────────────────────────────────────────
    // TA-ERR-003: confirmação de slug diverge
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TA-ERR-003: confirmação de slug divergente lança DomainValidationException")]
    public async Task SlugConfirmationMismatch_ShouldThrow_TA_ERR_003()
    {
        var handler = CreateHandler();
        var cmd = ValidCommand(slug: "meu-tenant", slugConfirmation: "outro-slug");

        var act = () => handler.Handle(cmd, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-003");
    }

    // ──────────────────────────────────────────────────────────────
    // TA-ERR-005: formato de slug inválido
    // ──────────────────────────────────────────────────────────────

    [Theory(DisplayName = "TA-ERR-005: slug com formato inválido lança DomainValidationException")]
    [InlineData("--invalido")]
    [InlineData("slug com espaço")]
    [InlineData("ab")]          // muito curto
    [InlineData("")]            // vazio
    public async Task InvalidSlugFormat_ShouldThrow_TA_ERR_005(string slug)
    {
        var handler = CreateHandler();
        var cmd = ValidCommand(slug: slug, slugConfirmation: slug);

        var act = () => handler.Handle(cmd, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-005");
    }

    // ──────────────────────────────────────────────────────────────
    // TA-ERR-002: slug já existe
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TA-ERR-002: slug já existente lança DomainValidationException (409)")]
    public async Task ExistingSlug_ShouldThrow_TA_ERR_002()
    {
        _repository.ExistsSlugAsync("meu-tenant", Arg.Any<CancellationToken>()).Returns(true);

        var handler = CreateHandler();
        var act = () => handler.Handle(ValidCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-002");
    }

    // ──────────────────────────────────────────────────────────────
    // TA-ERR-006: fuso IANA inválido
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TA-ERR-006: fuso horário inválido lança DomainValidationException")]
    public async Task InvalidTimezone_ShouldThrow_TA_ERR_006()
    {
        _repository.ExistsSlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var handler = CreateHandler();
        var cmd = ValidCommand(timezone: "Invalid/Timezone");

        var act = () => handler.Handle(cmd, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-006");
    }

    // ──────────────────────────────────────────────────────────────
    // TA-ERR-009: falha no IdP propagada
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TA-ERR-009: falha na saga do IdP é propagada ao caller")]
    public async Task SagaIdpFailure_ShouldPropagate()
    {
        _repository.ExistsSlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _saga.ExecuteAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ProvisioningResult>(_ => throw new DomainValidationException("TA-ERR-009", "Falha ao criar tenant de identidade."));

        var handler = CreateHandler();
        var act = () => handler.Handle(ValidCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-009");
    }

    // ──────────────────────────────────────────────────────────────
    // Handler não importa EF Core
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Handler não referencia EF Core nem GCP SDK diretamente")]
    public void Handler_ShouldNot_ReferenceEfCoreOrGcpSdk()
    {
        var handlerType = typeof(ProvisionTenantHandler);
        var referencedAssemblies = handlerType.Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        referencedAssemblies.Should().NotContain(a => a.StartsWith("Microsoft.EntityFrameworkCore",
            StringComparison.OrdinalIgnoreCase));
        referencedAssemblies.Should().NotContain(a => a.StartsWith("Google.",
            StringComparison.OrdinalIgnoreCase));
    }
}
