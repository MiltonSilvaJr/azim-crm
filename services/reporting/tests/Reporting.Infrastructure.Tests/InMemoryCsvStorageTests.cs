using FluentAssertions;
using Reporting.Infrastructure.Storage;
using Xunit;

namespace Reporting.Infrastructure.Tests;

/// <summary>
/// Testes unitários para <see cref="InMemoryCsvStorage"/>.
///
/// Verifica:
/// <list type="bullet">
///   <item><description>Upload é idempotente (mesmo objectName sobrescreve sem duplicata, DD-004).</description></item>
///   <item><description>Signed URL gerada com TTL configurável.</description></item>
///   <item><description>ExpiresAt dentro do TTL esperado.</description></item>
/// </list>
///
/// Mapeia: TASK-19, design §6.5, DD-004, Req 5.
/// </summary>
public sealed class InMemoryCsvStorageTests
{
    [Fact]
    public async Task Upload_PrimeiraVez_RetornaResultadoComSignedUrl()
    {
        var storage = new InMemoryCsvStorage(TimeSpan.FromMinutes(15));
        var bytes = "content"u8.ToArray();

        var result = await storage.UploadAsync("reports/tenant1/funnel/hash1/scope1.csv", bytes);

        result.SignedUrl.Should().NotBeNullOrWhiteSpace("deve retornar URL assinada");
        result.ObjectName.Should().Be("reports/tenant1/funnel/hash1/scope1.csv");
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow, "URL deve ter expiração futura");
    }

    [Fact]
    public async Task Upload_MesmoObjectName_Sobrescreve_Idempotente_DD004()
    {
        var storage = new InMemoryCsvStorage(TimeSpan.FromMinutes(15));
        var objectName = "reports/tenant/type/period/scope.csv";
        var firstBytes = "primeira versão"u8.ToArray();
        var secondBytes = "segunda versão"u8.ToArray();

        await storage.UploadAsync(objectName, firstBytes);
        await storage.UploadAsync(objectName, secondBytes);

        // Apenas um objeto deve existir (idempotente — sobrescreve)
        storage.Count.Should().Be(1, "re-upload com mesmo nome deve sobrescrever, não criar novo objeto (DD-004)");
        storage.GetStored(objectName).Should().Equal(secondBytes,
            "conteúdo deve ser da segunda versão após sobrescrita");
    }

    [Fact]
    public async Task Upload_SignedUrl_ExpiraDentroDoTtlConfigurado()
    {
        var ttl = TimeSpan.FromMinutes(30);
        var storage = new InMemoryCsvStorage(ttl);
        var before = DateTimeOffset.UtcNow;

        var result = await storage.UploadAsync("obj.csv", "data"u8.ToArray());

        var after = DateTimeOffset.UtcNow;
        result.ExpiresAt.Should().BeOnOrAfter(before.Add(ttl - TimeSpan.FromSeconds(1)));
        result.ExpiresAt.Should().BeOnOrBefore(after.Add(ttl + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Upload_ObjectosDistintos_ArmazenaSeparadamente()
    {
        var storage = new InMemoryCsvStorage();
        var obj1 = "reports/t/funnel/p1/s1.csv";
        var obj2 = "reports/t/forecast/p1/s1.csv";

        await storage.UploadAsync(obj1, "funnel"u8.ToArray());
        await storage.UploadAsync(obj2, "forecast"u8.ToArray());

        storage.Count.Should().Be(2, "objetos com nomes distintos devem ser armazenados separadamente");
        storage.Contains(obj1).Should().BeTrue();
        storage.Contains(obj2).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Upload_ObjectNameVazioOuWhitespace_LancaArgumentException(string objectName)
    {
        var storage = new InMemoryCsvStorage();

        await FluentActions.Awaiting(
            () => storage.UploadAsync(objectName, "bytes"u8.ToArray()))
            .Should().ThrowAsync<ArgumentException>(
                "objectName vazio deve ser rejeitado imediatamente");
    }
}
