using FluentAssertions;
using Organization.Infrastructure.Adapters;
using Organization.Infrastructure.Adapters.Identity;
using Xunit;

namespace Organization.Infrastructure.Tests.Adapters;

/// <summary>
/// Testes unitários dos adapters externos: FakeIdentityProvisioner, OpportunityCounterStub,
/// ActivityCounterStub. Sem chamadas a serviços reais (TASK-19).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExternalAdaptersTests
{
    // ── FakeIdentityProvisioner ───────────────────────────────────────────

    [Fact(DisplayName = "ST-08: FakeIdentityProvisioner retorna UID determinístico por e-mail")]
    public async Task FakeProvisioner_RetornaUidDeterministico()
    {
        // Arrange
        var provisioner = new FakeIdentityProvisioner();

        // Act
        var uid1 = await provisioner.ProvisionAsync("user@example.com", "User Name");
        var uid2 = await provisioner.ProvisionAsync("user@example.com", "User Name");

        // Assert
        uid1.Should().NotBeNullOrEmpty();
        uid2.Should().Be(uid1, "mesmo e-mail deve retornar o mesmo UID (idempotência)");
    }

    [Fact(DisplayName = "ST-08: FakeIdentityProvisioner gera UIDs distintos para e-mails distintos")]
    public async Task FakeProvisioner_UidsDiferentesParaEmailsDiferentes()
    {
        // Arrange
        var provisioner = new FakeIdentityProvisioner();

        // Act
        var uid1 = await provisioner.ProvisionAsync("alice@example.com", "Alice");
        var uid2 = await provisioner.ProvisionAsync("bob@example.com", "Bob");

        // Assert
        uid1.Should().NotBe(uid2, "e-mails distintos devem gerar UIDs distintos");
    }

    [Fact(DisplayName = "ST-08: FakeIdentityProvisioner é case-insensitive para e-mail")]
    public async Task FakeProvisioner_CaseInsensitivePorEmail()
    {
        // Arrange
        var provisioner = new FakeIdentityProvisioner();

        // Act
        var uid1 = await provisioner.ProvisionAsync("User@Example.COM", "User");
        var uid2 = await provisioner.ProvisionAsync("user@example.com", "User");

        // Assert
        uid1.Should().Be(uid2, "e-mail deve ser tratado case-insensitive");
    }

    [Fact(DisplayName = "ST-08: FakeIdentityProvisioner não loga e-mail (UID não contém PII)")]
    public async Task FakeProvisioner_UidNaoContemEmail()
    {
        // Arrange
        var provisioner = new FakeIdentityProvisioner();
        var email = "sensitive@user.com";

        // Act
        var uid = await provisioner.ProvisionAsync(email, "Sensitive User");

        // Assert
        uid.Should().NotContain("@", "UID não deve conter o e-mail (PII)");
        uid.Should().NotContain("sensitive", "UID não deve conter partes do e-mail");
    }

    // ── OpportunityCounterStub ────────────────────────────────────────────

    [Fact(DisplayName = "ST-08: OpportunityCounterStub retorna 0 para qualquer BU")]
    public async Task OpportunityStub_RetornaZero()
    {
        // Arrange
        var stub = new OpportunityCounterStub();

        // Act
        var count = await stub.CountActiveAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        count.Should().Be(0, "MVP stub sempre retorna 0 oportunidades ativas");
    }

    // ── ActivityCounterStub ───────────────────────────────────────────────

    [Fact(DisplayName = "ST-08: ActivityCounterStub retorna 0 para qualquer usuário")]
    public async Task ActivityStub_RetornaZero()
    {
        // Arrange
        var stub = new ActivityCounterStub();

        // Act
        var count = await stub.CountFutureAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        // Assert
        count.Should().Be(0, "MVP stub sempre retorna 0 atividades futuras");
    }
}
