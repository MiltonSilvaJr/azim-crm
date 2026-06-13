using FluentAssertions;
using NotificationDelivery.Application.Ports;
using Xunit;

namespace NotificationDelivery.Infrastructure.Tests.Secrets;

/// <summary>
/// Testes para <see cref="SecretProviderFake"/> e para os contratos de <see cref="ISecretProvider"/>.
///
/// Estratégia (RISK-EXEC-03): sem GCP Secret Manager real em CI.
/// O <see cref="SecretProviderFake"/> isola completamente a dependência de infraestrutura.
///
/// Cobre os critérios de aceite da TASK-14 com injeção de fake:
/// - Retorna credencial configurada (ST-01);
/// - Conta chamadas (base para verificar cache no provedor real);
/// - Falha de acesso → <see cref="SecretProviderException"/> (ST-01d);
/// - Credencial nunca em log (verificado na TASK-15 / PBT-03).
/// </summary>
public sealed class SecretProviderFakeTests
{
    // -------------------------------------------------------------------------
    // ST-01: SecretProviderFake retorna credencial corretamente
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "GetSecretAsync: retorna credencial para nome configurado")]
    public async Task GetSecretAsync_WithKnownSecret_ReturnsValue()
    {
        // Arrange
        var fake = new SecretProviderFake(new Dictionary<string, string>
        {
            ["RESEND_API_KEY"] = "re_test_key_12345"
        });

        // Act
        var result = await fake.GetSecretAsync("RESEND_API_KEY");

        // Assert
        result.Should().Be("re_test_key_12345");
    }

    [Fact(DisplayName = "GetSecretAsync: lança SecretProviderException para segredo não configurado")]
    public async Task GetSecretAsync_WithUnknownSecret_ThrowsSecretProviderException()
    {
        // Arrange
        var fake = new SecretProviderFake();

        // Act
        var act = async () => await fake.GetSecretAsync("UNKNOWN_KEY");

        // Assert
        await act.Should().ThrowAsync<SecretProviderException>()
            .WithMessage("*UNKNOWN_KEY*");
    }

    [Fact(DisplayName = "GetSecretAsync: lança SecretProviderException quando configurado para falhar")]
    public async Task GetSecretAsync_WithForcedFailure_ThrowsSecretProviderException()
    {
        // Arrange
        var fake = new SecretProviderFake(
            secrets: new Dictionary<string, string> { ["RESEND_API_KEY"] = "value" },
            failOn: new Dictionary<string, bool> { ["RESEND_API_KEY"] = true });

        // Act
        var act = async () => await fake.GetSecretAsync("RESEND_API_KEY");

        // Assert
        await act.Should().ThrowAsync<SecretProviderException>()
            .WithMessage("*Falha simulada*");
    }

    // -------------------------------------------------------------------------
    // ST-02: contador de chamadas (base para verificar cache)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "GetSecretAsync: contador de chamadas incrementa a cada invocação")]
    public async Task GetSecretAsync_MultipleCalls_IncrementsCallCount()
    {
        // Arrange
        var fake = new SecretProviderFake(new Dictionary<string, string>
        {
            ["RESEND_API_KEY"] = "value"
        });

        // Act — chama 3 vezes
        await fake.GetSecretAsync("RESEND_API_KEY");
        await fake.GetSecretAsync("RESEND_API_KEY");
        await fake.GetSecretAsync("RESEND_API_KEY");

        // Assert
        fake.GetCallCount("RESEND_API_KEY").Should().Be(3,
            because: "o fake não tem cache — cada chamada incrementa o contador; " +
                     "quando o SecretManagerProvider (com cache) for testado, o contador deve ser 1 " +
                     "para chamadas dentro do TTL");
    }

    [Fact(DisplayName = "GetCallCount: retorna 0 para segredo nunca chamado")]
    public void GetCallCount_WithNeverCalledSecret_ReturnsZero()
    {
        // Arrange
        var fake = new SecretProviderFake();

        // Act + Assert
        fake.GetCallCount("QUALQUER_CHAVE").Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // Invariante: SecretProviderException não expõe valor da credencial (RNF-6.3)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SecretProviderException: mensagem não contém valor da credencial")]
    public async Task GetSecretAsync_OnFailure_ExceptionMessageDoesNotContainCredentialValue()
    {
        // Arrange
        const string secretValue = "sk_live_super_secret_key_9999";
        var fake = new SecretProviderFake(
            secrets: new Dictionary<string, string> { ["RESEND_API_KEY"] = secretValue },
            failOn: new Dictionary<string, bool> { ["RESEND_API_KEY"] = true });

        // Act
        SecretProviderException? capturedException = null;
        try
        {
            await fake.GetSecretAsync("RESEND_API_KEY");
        }
        catch (SecretProviderException ex)
        {
            capturedException = ex;
        }

        // Assert
        capturedException.Should().NotBeNull();
        capturedException!.Message.Should().NotContain(secretValue,
            because: "a mensagem de exceção não deve expor o valor da credencial (RNF-6.3, Req 10.4)");
        capturedException.SecretName.Should().Be("RESEND_API_KEY");
    }

    // -------------------------------------------------------------------------
    // Contratos de ISecretProvider
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "ISecretProvider: SecretProviderFake implementa a interface corretamente")]
    public void SecretProviderFake_ImplementsISecretProvider()
    {
        // Arrange + Act
        ISecretProvider provider = new SecretProviderFake();

        // Assert
        provider.Should().BeAssignableTo<ISecretProvider>();
    }

    [Fact(DisplayName = "SecretProviderException: SecretName preservado na exceção")]
    public async Task GetSecretAsync_OnFailure_ExceptionPreservesSecretName()
    {
        // Arrange
        var fake = new SecretProviderFake();

        // Act
        SecretProviderException? ex = null;
        try { await fake.GetSecretAsync("MY_SECRET"); }
        catch (SecretProviderException e) { ex = e; }

        // Assert
        ex.Should().NotBeNull();
        ex!.SecretName.Should().Be("MY_SECRET");
    }
}
