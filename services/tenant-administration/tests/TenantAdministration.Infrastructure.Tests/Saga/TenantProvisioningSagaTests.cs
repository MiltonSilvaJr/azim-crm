using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TenantAdministration.Infrastructure.Identity;
using TenantAdministration.Infrastructure.Observability;
using TenantAdministration.Infrastructure.Outbox;
using TenantAdministration.Infrastructure.Persistence;
using TenantAdministration.Infrastructure.Persistence.Repositories;
using TenantAdministration.Infrastructure.Saga;
using TenantAdministration.Infrastructure.Tests.Fixtures;
using Xunit;

namespace TenantAdministration.Infrastructure.Tests.Saga;

/// <summary>
/// Testes de integração da <see cref="TenantProvisioningSaga"/> com PostgreSQL real (TASK-13).
/// Verifica atomicidade, compensação e idempotência. Cobre PBT-07.
/// </summary>
[Collection("Postgres")]
public sealed class TenantProvisioningSagaTests
{
    private readonly PostgresContainerFixture _fixture;

    public TenantProvisioningSagaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Gera um sufixo de slug com exatamente <paramref name="length"/> letras [a-f] de múltiplos GUIDs.
    /// Garante que o slug resultante seja sempre válido (regex exige início/fim com [a-z]).
    /// </summary>
    private static string SafeSlugSuffix(int length)
    {
        var letters = new System.Text.StringBuilder();
        while (letters.Length < length)
            letters.Append(new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).ToArray()));
        return letters.ToString()[..length] + "x";
    }

    private static TenantAdministrationMetrics CreateFakeMetrics() =>
        new(new TenantAdministration.Infrastructure.Tests.Fixtures.FakeMeterFactory());

    private TenantProvisioningSaga CreateSaga(
        TenantAdministrationDbContext db,
        FakeIdentityTenantProvisioner idp,
        FakeTenantContext tenantCtx)
    {
        var clock = new Infrastructure.Clock.SystemClock();
        var outbox = new OutboxRepository(db, tenantCtx);
        return new TenantProvisioningSaga(db, idp, outbox, tenantCtx, clock,
            NullLogger<TenantProvisioningSaga>.Instance,
            CreateFakeMetrics());
    }

    [Fact(DisplayName = "Saga cria tenant no IdP e persiste no banco — estado consistente")]
    public async Task ExecuteAsync_SuccessPath_CreatesTenantInBothSystems()
    {
        // Arrange
        var idp = new FakeIdentityTenantProvisioner();
        var tenantCtx = new FakeTenantContext();
        using var db = _fixture.CreateDbContext();
        var saga = CreateSaga(db, idp, tenantCtx);

        // Act
        var result = await saga.ExecuteAsync(
            Guid.NewGuid(),
            "saga-success-test",
            "Saga Success",
            "America/Sao_Paulo",
            "07:00",
            "admin@saga.com",
            Guid.NewGuid().ToString());

        // Assert
        result.Should().NotBeNull();
        result.TenantId.Should().NotBeEmpty();
        result.IdentityTenantId.Should().StartWith("fake-idp-");

        idp.Created.Should().Contain("saga-success-test");

        using var verifyDb = _fixture.CreateDbContext();
        var tenant = await verifyDb.Tenants.FindAsync(result.TenantId);
        tenant.Should().NotBeNull();
        tenant!.IdentityTenantId.Should().Be(result.IdentityTenantId);
    }

    [Fact(DisplayName = "Saga falha no IdP — nenhum tenant criado no banco (ambos-ou-nenhum)")]
    public async Task ExecuteAsync_IdpFails_NoTenantInDatabase()
    {
        // Arrange
        var idp = new FakeIdentityTenantProvisioner { ShouldFailOnCreate = true };
        var tenantCtx = new FakeTenantContext();
        using var db = _fixture.CreateDbContext();
        var saga = CreateSaga(db, idp, tenantCtx);

        // Slug válido: sem dígitos — prefixo puro com letras/hífens
        var slug = "saga-idp-fail-" + SafeSlugSuffix(3);

        // Act
        var act = async () => await saga.ExecuteAsync(
            Guid.NewGuid(), slug, "IdP Fail Test", "America/Sao_Paulo", "07:00",
            "admin@fail.com", Guid.NewGuid().ToString());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TA-ERR-009*");

        // Verifica que nenhum tenant foi criado no banco
        // Capturar Slug VO antes do LINQ para evitar avaliação de expressão pelo EF Core.
        var slugVo = TenantAdministration.Domain.ValueObjects.Slug.Create(slug).Value;
        using var verifyDb = _fixture.CreateDbContext();
        var count = await verifyDb.Tenants.CountAsync(t => t.Slug == slugVo);
        count.Should().Be(0);
    }

    [Fact(DisplayName = "Saga idempotente — segunda chamada com mesma chave retorna resultado anterior")]
    public async Task ExecuteAsync_SameIdempotencyKey_ReturnsExistingResult()
    {
        // Arrange
        var idp = new FakeIdentityTenantProvisioner();
        var tenantCtx = new FakeTenantContext();
        var idempotencyKey = Guid.NewGuid().ToString();
        // Slug válido: sem dígitos — prefixo puro com letras/hífens
        var slug = "idempotent-saga-" + SafeSlugSuffix(3);

        using var db1 = _fixture.CreateDbContext();
        var saga1 = CreateSaga(db1, idp, tenantCtx);
        var first = await saga1.ExecuteAsync(
            Guid.NewGuid(), slug, "Idempotent Test", "America/Sao_Paulo", "07:00",
            "admin@idem.com", idempotencyKey);

        // Resetar o fake para garantir que não cria novamente
        idp.Reset();

        // Act — segunda chamada com mesma chave
        using var db2 = _fixture.CreateDbContext();
        var saga2 = CreateSaga(db2, idp, tenantCtx);
        var second = await saga2.ExecuteAsync(
            Guid.NewGuid(), slug, "Idempotent Test", "America/Sao_Paulo", "07:00",
            "admin@idem.com", idempotencyKey);

        // Assert — IdP não chamado novamente (idempotência)
        idp.Created.Should().BeEmpty("a segunda chamada com mesma chave não deve chamar o IdP novamente");
        second.TenantId.Should().Be(first.TenantId);
        second.IdentityTenantId.Should().Be(first.IdentityTenantId);
    }
}
