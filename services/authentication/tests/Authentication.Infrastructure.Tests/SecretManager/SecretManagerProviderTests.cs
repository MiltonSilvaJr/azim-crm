using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Infrastructure.SecretManager;
using FluentAssertions;
using Xunit;

namespace Authentication.Infrastructure.Tests.SecretManager;

/// <summary>
/// Testes de contrato e comportamento de <see cref="SecretManagerProvider"/>.
///
/// Mapeia: RNF 7, design.md § 6.6, § 6.8, DD-005, TASK-13.
/// </summary>
public sealed class SecretManagerProviderTests
{
    // =========================================================================
    // Contrato — SecretManagerProvider implementa ISecretProvider
    // =========================================================================

    /// <summary>
    /// Verifica que <see cref="SecretManagerProvider"/> implementa <see cref="ISecretProvider"/>.
    ///
    /// Mapeia: TASK-13 ST-01 (teste de contrato).
    /// </summary>
    [Fact(DisplayName = "SecretManagerProvider deve implementar ISecretProvider (TASK-13, RNF 7)")]
    public void SecretManagerProvider_ShouldImplement_ISecretProvider()
    {
        typeof(SecretManagerProvider)
            .Should()
            .Implement<ISecretProvider>(
                because: "SecretManagerProvider é o adapter concreto da porta ISecretProvider (RNF 7, DD-005)");
    }

    // =========================================================================
    // Falha controlada — bootstrap falha de forma controlada sem expor detalhes
    // =========================================================================

    /// <summary>
    /// Quando o cliente subjacente lança exceção (acesso negado / serviço indisponível),
    /// o provider deve lançar <see cref="SecretProviderException"/> com mensagem genérica
    /// — sem expor detalhes internos ao chamador.
    ///
    /// Mapeia: TASK-13 ST-01 (bootstrap falha de forma controlada), RNF 7, DD-005.
    /// </summary>
    [Fact(DisplayName = "GetSecretAsync deve lançar SecretProviderException quando o Secret Manager falha (TASK-13, RNF 7)")]
    public async Task GetSecretAsync_WhenSecretManagerFails_ShouldThrow_SecretProviderException()
    {
        // Arrange
        // Usa o provider com um cliente stub que sempre falha.
        var provider = SecretManagerProvider.CreateWithFaultyClient(
            projectId: "test-project",
            faultMessage: "Permission denied: caller does not have permission");

        // Act
        Func<Task> act = async () => await provider.GetSecretAsync("firebase-service-account-key", CancellationToken.None);

        // Assert
        var exception = await act.Should()
            .ThrowAsync<SecretProviderException>(
                because: "qualquer falha do Secret Manager deve ser mapeada para SecretProviderException " +
                         "sem expor detalhes internos ao chamador (RNF 7, DD-005)");

        exception.Which.Message
            .Should()
            .NotContain("Permission denied",
                because: "mensagem interna do Secret Manager não deve vazar ao chamador (DD-005, RNF 7)");

        exception.Which.Message
            .Should()
            .NotContain("firebase-service-account-key",
                because: "nome do segredo não deve ser exposto em mensagem de erro ao chamador");
    }

    /// <summary>
    /// O nome do segredo nunca deve aparecer em logs ou mensagens expostas.
    /// Verificação de que o <see cref="SecretProviderException"/> nunca carrega
    /// o nome do segredo na propriedade Message.
    ///
    /// Mapeia: DD-005, RNF 7.1, TASK-13.
    /// </summary>
    [Fact(DisplayName = "SecretProviderException nunca deve expor o nome do segredo na mensagem (DD-005, RNF 7)")]
    public async Task GetSecretAsync_OnFailure_ExceptionMessage_ShouldNotContain_SecretName()
    {
        // Arrange
        const string secretName = "my-super-secret-api-key";
        var provider = SecretManagerProvider.CreateWithFaultyClient(
            projectId: "test-project",
            faultMessage: "Not found");

        // Act
        Func<Task> act = async () => await provider.GetSecretAsync(secretName, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<SecretProviderException>();

        exception.Which.Message
            .Should()
            .NotContain(secretName,
                because: "o nome do segredo nunca deve ser exposto fora do adapter (DD-005)");
    }

    // =========================================================================
    // Construção — provider sem cliente injetado exige project ID não-vazio
    // =========================================================================

    /// <summary>
    /// O provider não deve aceitar project ID nulo ou vazio no construtor.
    /// Isso garante falha rápida em misconfiguration ao invés de erro tardio.
    ///
    /// Mapeia: TASK-13 ST-01 (bootstrap falha de forma controlada), DD-005.
    /// </summary>
    [Theory(DisplayName = "SecretManagerProvider deve rejeitar projectId nulo ou vazio (TASK-13)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidProjectId_ShouldThrow_ArgumentException(string? projectId)
    {
        // Act
        var act = () => new SecretManagerProvider(projectId!);

        // Assert
        act.Should()
            .Throw<ArgumentException>(
                because: "project ID nulo ou vazio deve falhar no construtor para detectar misconfiguration precocemente");
    }
}
