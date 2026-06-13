using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NotificationDelivery.Application.Decorators;
using NotificationDelivery.Application.Ports;
using NotificationDelivery.Application.Rendering;
using NotificationDelivery.Application.Resilience;
using NotificationDelivery.Contracts;
using NotificationDelivery.Infrastructure.HealthChecks;
using NotificationDelivery.Infrastructure.Mapping;
using NotificationDelivery.Infrastructure.Secrets;
using NotificationDelivery.Infrastructure.Senders.Resend;
using NotificationDelivery.Infrastructure.Senders.SendGrid;
using NotificationDelivery.Infrastructure.Telemetry;

namespace NotificationDelivery.Infrastructure.DependencyInjection;

/// <summary>
/// Extensões de DI que registram toda a cadeia do módulo notification-delivery.
///
/// Cadeia registrada (design §5.3):
/// <code>IEmailSender (ResilientEmailSender) → BrandingEmailDecorator → ProviderEmailSender (Resend/SendGrid)</code>
///
/// Dependências de suporte: <see cref="ProviderResponseMapper"/>, <see cref="ISecretProvider"/>,
/// <see cref="EmailHasher"/> (stateless/static), <see cref="EmailProviderHealthCheck"/>.
///
/// Uso:
/// <code>
/// builder.Services.AddNotificationDelivery(builder.Configuration);
/// </code>
/// </summary>
public static class NotificationDeliveryServiceExtensions
{
    /// <summary>
    /// Registra toda a cadeia do módulo notification-delivery no container de DI.
    ///
    /// <para>O provedor ativo é selecionado via <c>NotificationDelivery:Provider</c>
    /// em <paramref name="configuration"/> (valores: "resend" | "sendgrid"; padrão: "resend").</para>
    ///
    /// <para>Também registra o <see cref="EmailProviderHealthCheck"/> como health check
    /// com o nome <c>"email_provider"</c>.</para>
    /// </summary>
    /// <param name="services">Coleção de serviços do DI.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>A mesma <paramref name="services"/> para encadeamento.</returns>
    public static IServiceCollection AddNotificationDelivery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // -------------------------------------------------------------------------
        // 1. Opções de configuração tipadas
        // -------------------------------------------------------------------------
        services.Configure<ResilientEmailSenderOptions>(
            configuration.GetSection(ResilientEmailSenderOptions.SectionName));

        services.Configure<SecretManagerOptions>(
            configuration.GetSection(SecretManagerOptions.SectionName));

        // -------------------------------------------------------------------------
        // 2. ACL e mapeamento
        // -------------------------------------------------------------------------
        services.TryAddSingleton<ProviderResponseMapper>();

        // -------------------------------------------------------------------------
        // 3. Secret Manager (GCP — DD-007)
        // -------------------------------------------------------------------------
        services.AddMemoryCache();

        // Registra ISecretProvider com o SecretManagerProvider concreto
        // Em testes, substituir por SecretProviderFake via .Replace() ou Override.
        services.TryAddSingleton<ISecretProvider, SecretManagerProvider>();

        // -------------------------------------------------------------------------
        // 4. Senders (provedor selecionado por configuração — DD-001)
        // -------------------------------------------------------------------------
        var providerName = configuration["NotificationDelivery:Provider"]
            ?? ResendEmailSender.ProviderName;

        if (providerName.Equals(SendGridEmailSender.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            // SendGrid como provider ativo
            services.AddHttpClient<SendGridEmailSender>(client =>
            {
                client.BaseAddress = new Uri("https://api.sendgrid.com");
            });
            services.TryAddSingleton<IEmailProviderClient, SendGridEmailSender>();
        }
        else
        {
            // Resend como provider ativo (padrão — DD-001, ADR-0005)
            services.AddHttpClient<ResendEmailSender>(client =>
            {
                client.BaseAddress = new Uri("https://api.resend.com");
            });
            services.TryAddSingleton<IEmailProviderClient, ResendEmailSender>();
        }

        // -------------------------------------------------------------------------
        // 5. Cadeia de decorators Application (design §5.3)
        // -------------------------------------------------------------------------
        services.TryAddSingleton<EmailTemplateRenderer>();

        // Sender concreto: delega ao IEmailProviderClient (Resend ou SendGrid)
        services.TryAddSingleton<ProviderEmailSenderAdapter>();

        // BrandingEmailDecorator envolve o ProviderEmailSenderAdapter
        services.TryAddSingleton<BrandingEmailDecorator>(sp =>
        {
            var inner = sp.GetRequiredService<ProviderEmailSenderAdapter>();
            var renderer = sp.GetRequiredService<EmailTemplateRenderer>();
            var logger = sp.GetRequiredService<ILogger<BrandingEmailDecorator>>();
            return new BrandingEmailDecorator(inner, renderer, logger);
        });

        // ResilientEmailSender envolve o BrandingEmailDecorator — registrado como IEmailSender
        services.TryAddSingleton<IEmailSender>(sp =>
        {
            var inner = sp.GetRequiredService<BrandingEmailDecorator>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResilientEmailSenderOptions>>();
            var logger = sp.GetRequiredService<ILogger<ResilientEmailSender>>();
            return new ResilientEmailSender(inner, options, logger);
        });

        // -------------------------------------------------------------------------
        // 6. Health check (Req 11, design §11.5)
        // -------------------------------------------------------------------------
        services.AddHealthChecks()
            .AddCheck<EmailProviderHealthCheck>(
                name: "email_provider",
                tags: ["ready", "notification-delivery"]);

        return services;
    }
}

/// <summary>
/// Adapter interno que envolve <see cref="IEmailProviderClient"/> como <see cref="IEmailSender"/>
/// sem lógica de resiliência (a resiliência é adicionada pelo <see cref="ResilientEmailSender"/>).
///
/// Mantido em Infrastructure porque referencia <see cref="IEmailProviderClient"/> (porta de saída
/// definida em Application mas implementada em Infrastructure).
///
/// Mapeamento: <see cref="ProviderResponse"/> → <see cref="SendResult"/> via <see cref="ProviderResponseMapper"/>.
/// </summary>
internal sealed class ProviderEmailSenderAdapter : IEmailSender
{
    private readonly IEmailProviderClient _providerClient;
    private readonly ProviderResponseMapper _mapper;
    private readonly Microsoft.Extensions.Logging.ILogger<ProviderEmailSenderAdapter> _logger;

    public ProviderEmailSenderAdapter(
        IEmailProviderClient providerClient,
        ProviderResponseMapper mapper,
        Microsoft.Extensions.Logging.ILogger<ProviderEmailSenderAdapter> logger)
    {
        _providerClient = providerClient;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _providerClient
                .SendAsync(message, cancellationToken)
                .ConfigureAwait(false);

            return _mapper.Map(response, message.CorrelationId, attemptCount: 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Exceção inesperada no ProviderEmailSenderAdapter. " +
                "Tipo={ExceptionType}, CorrelationId={CorrelationId}. (NOTIF-ERR-090)",
                ex.GetType().Name,
                message.CorrelationId);

            return _mapper.MapException(ex, message.CorrelationId, attemptCount: 1);
        }
    }

    public Task<HealthCheckResult> CheckAvailabilityAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy("Provedor de e-mail disponível."));
}
