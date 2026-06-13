using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationDelivery.Contracts;
using NotificationDelivery.Infrastructure.HealthChecks;
using Xunit;

namespace NotificationDelivery.Infrastructure.Tests.HealthChecks;

/// <summary>
/// Testes do <see cref="EmailProviderHealthCheck"/> (Req 11, design §11.5).
///
/// Usa fakes de <see cref="IEmailSender"/> para simular provedor disponível/indisponível
/// sem rede real nem credencial (RISK-EXEC-03).
///
/// Cobre os critérios de aceite da TASK-16:
/// - Provedor disponível → Healthy (ST-01a);
/// - Provedor 5xx → Unhealthy com descrição sem PII (ST-01b);
/// - Exceção não propagada (Req 3.5, Req 11.3);
/// - Descrição sem PII e sem credencial (Req 11.3).
/// </summary>
public sealed class EmailProviderHealthCheckTests
{
    private static HealthCheckContext BuildContext() => new()
    {
        Registration = new HealthCheckRegistration("email_provider", _ => null!, null, null)
    };

    // -------------------------------------------------------------------------
    // ST-01a: provedor disponível → Healthy
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "CheckHealthAsync: sender saudável → Healthy")]
    public async Task CheckHealthAsync_WithHealthySender_ReturnsHealthy()
    {
        // Arrange
        var healthySender = new FakeEmailSender(HealthCheckResult.Healthy("OK"));
        var healthCheck = new EmailProviderHealthCheck(
            healthySender,
            NullLogger<EmailProviderHealthCheck>.Instance);

        // Act
        var result = await healthCheck.CheckHealthAsync(BuildContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    // -------------------------------------------------------------------------
    // ST-01b: provedor 5xx → Unhealthy com descrição sem PII
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "CheckHealthAsync: sender inativo → Unhealthy sem PII na descrição")]
    public async Task CheckHealthAsync_WithUnhealthySender_ReturnsUnhealthy()
    {
        // Arrange
        var unhealthySender = new FakeEmailSender(
            HealthCheckResult.Unhealthy("Provedor indisponível."));

        var healthCheck = new EmailProviderHealthCheck(
            unhealthySender,
            NullLogger<EmailProviderHealthCheck>.Instance);

        // Act
        var result = await healthCheck.CheckHealthAsync(BuildContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);

        // Verificar ausência de PII e credencial na descrição (Req 11.3)
        result.Description.Should().NotContain("@",
            because: "a descrição do health check não deve conter e-mail (Req 11.3)");
        result.Description.Should().NotContain("api_key",
            because: "a descrição não deve conter credencial (Req 11.3)");
    }

    // -------------------------------------------------------------------------
    // Exceção não propagada → Unhealthy (Req 11.3, Req 3.5)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "CheckHealthAsync: exceção no sender → Unhealthy sem exceção propagada")]
    public async Task CheckHealthAsync_WithThrowingSender_ReturnsUnhealthyWithoutThrowing()
    {
        // Arrange
        var throwingSender = new ThrowingEmailSender(new HttpRequestException("Timeout"));
        var healthCheck = new EmailProviderHealthCheck(
            throwingSender,
            NullLogger<EmailProviderHealthCheck>.Instance);

        // Act — não deve lançar (Req 3.5)
        var act = async () => await healthCheck.CheckHealthAsync(BuildContext());
        await act.Should().NotThrowAsync(
            because: "EmailProviderHealthCheck não deve propagar exceções (Req 11.3)");

        var result = await healthCheck.CheckHealthAsync(BuildContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().BeNull(
            because: "a exceção interna não deve ser exposta na resposta (Req 11.3)");
    }

    [Fact(DisplayName = "CheckHealthAsync: descrição de Unhealthy não contém PII nem credencial")]
    public async Task CheckHealthAsync_OnException_DescriptionHasNoPii()
    {
        // Arrange
        var throwingSender = new ThrowingEmailSender(new Exception("internal error with credential sk_live_xxx"));
        var healthCheck = new EmailProviderHealthCheck(
            throwingSender,
            NullLogger<EmailProviderHealthCheck>.Instance);

        // Act
        var result = await healthCheck.CheckHealthAsync(BuildContext());

        // Assert — a descrição não deve vazar credencial nem PII
        result.Description.Should().NotContain("sk_live_xxx",
            because: "descrição não deve conter credencial da exceção interna (Req 11.3)");
        result.Description.Should().NotContain("@",
            because: "descrição não deve conter e-mail (Req 11.3)");
    }

    // -------------------------------------------------------------------------
    // Fakes internos
    // -------------------------------------------------------------------------

    private sealed class FakeEmailSender : IEmailSender
    {
        private readonly HealthCheckResult _healthResult;

        public FakeEmailSender(HealthCheckResult healthResult) =>
            _healthResult = healthResult;

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken ct = default) =>
            throw new NotSupportedException("Fake — apenas health check.");

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken ct = default) =>
            Task.FromResult(_healthResult);
    }

    private sealed class ThrowingEmailSender : IEmailSender
    {
        private readonly Exception _exception;

        public ThrowingEmailSender(Exception exception) => _exception = exception;

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken ct = default) =>
            throw _exception;
    }
}
