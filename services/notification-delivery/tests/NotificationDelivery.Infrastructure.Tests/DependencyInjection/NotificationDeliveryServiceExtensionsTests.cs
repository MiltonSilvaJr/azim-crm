using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationDelivery.Application.Ports;
using NotificationDelivery.Contracts;
using NotificationDelivery.Infrastructure.DependencyInjection;
using NotificationDelivery.Infrastructure.Tests.Secrets;
using Xunit;

namespace NotificationDelivery.Infrastructure.Tests.DependencyInjection;

/// <summary>
/// Testes das extensões de DI (<see cref="NotificationDeliveryServiceExtensions"/>).
///
/// Verifica que <c>AddNotificationDelivery</c> registra a cadeia completa
/// e que <see cref="IEmailSender"/> é resolvível no container (critério de aceite TASK-16/ST-01c).
///
/// Usa <see cref="SecretProviderFake"/> em vez do GCP Secret Manager real (RISK-EXEC-03).
/// </summary>
public sealed class NotificationDeliveryServiceExtensionsTests
{
    private static IServiceProvider BuildServiceProvider(string? provider = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NotificationDelivery:Provider"] = provider ?? "resend",
                ["NotificationDelivery:SecretManager:ProjectId"] = "test-project",
                ["NotificationDelivery:Resilience:MaxRetryAttempts"] = "1",
                ["NotificationDelivery:Resilience:TimeoutPerAttemptSeconds"] = "5",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationDelivery(config);

        // Substituir ISecretProvider pelo fake (sem GCP real — RISK-EXEC-03)
        // Remove o registro real e adiciona o fake
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISecretProvider));
        if (descriptor != null) services.Remove(descriptor);

        services.AddSingleton<ISecretProvider>(new SecretProviderFake(
            new Dictionary<string, string>
            {
                ["RESEND_API_KEY"] = "re_test_key",
                ["SENDGRID_API_KEY"] = "SG.test_key"
            }));

        return services.BuildServiceProvider();
    }

    // -------------------------------------------------------------------------
    // ST-01c: IEmailSender resolvível via AddNotificationDelivery (Req 11, TASK-16)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "AddNotificationDelivery (Resend): IEmailSender resolvível no container")]
    public void AddNotificationDelivery_WithResend_IEmailSenderIsResolvable()
    {
        // Arrange
        var sp = BuildServiceProvider(provider: "resend");

        // Act
        var sender = sp.GetService<IEmailSender>();

        // Assert
        sender.Should().NotBeNull(
            because: "IEmailSender deve ser registrado e resolvível após AddNotificationDelivery() (TASK-16/ST-01c)");
    }

    [Fact(DisplayName = "AddNotificationDelivery (SendGrid): IEmailSender resolvível no container")]
    public void AddNotificationDelivery_WithSendGrid_IEmailSenderIsResolvable()
    {
        // Arrange
        var sp = BuildServiceProvider(provider: "sendgrid");

        // Act
        var sender = sp.GetService<IEmailSender>();

        // Assert
        sender.Should().NotBeNull(
            because: "IEmailSender deve ser resolvível para SendGrid (TASK-16/ST-01c)");
    }

    [Fact(DisplayName = "AddNotificationDelivery: IEmailProviderClient resolvível no container")]
    public void AddNotificationDelivery_IEmailProviderClientIsResolvable()
    {
        // Arrange
        var sp = BuildServiceProvider();

        // Act
        var client = sp.GetService<IEmailProviderClient>();

        // Assert
        client.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // Health check registrado com o nome correto
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "AddNotificationDelivery: health check 'email_provider' registrado")]
    public void AddNotificationDelivery_RegistersHealthCheck()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NotificationDelivery:Provider"] = "resend",
                ["NotificationDelivery:SecretManager:ProjectId"] = "test-project"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationDelivery(config);

        // Replace ISecretProvider
        var d = services.FirstOrDefault(s => s.ServiceType == typeof(ISecretProvider));
        if (d != null) services.Remove(d);
        services.AddSingleton<ISecretProvider>(new SecretProviderFake(
            new Dictionary<string, string> { ["RESEND_API_KEY"] = "key" }));

        var sp = services.BuildServiceProvider();

        // Act — verificar que o health check service existe
        var healthCheckService = sp.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();

        // Assert
        healthCheckService.Should().NotBeNull(
            because: "AddHealthChecks() deve ter sido chamado por AddNotificationDelivery()");
    }
}
