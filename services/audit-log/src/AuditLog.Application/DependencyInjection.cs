using AuditLog.Application.Behaviors;
using AuditLog.Application.Commands;
using AuditLog.Application.Queries;
using AuditLog.Application.Writers;
using AuditLog.Contracts;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

// Validadores são registrados manualmente para não exigir FluentValidation.DependencyInjectionExtensions

namespace AuditLog.Application;

/// <summary>
/// Extensões de injeção de dependência para o projeto <c>AuditLog.Application</c>.
/// Registra MediatR com a pipeline de behaviors na ordem correta (design §5.4):
/// <c>ValidationBehavior</c> → <c>TenantContextBehavior</c> → <c>AuthorizationBehavior</c> → <c>LoggingBehavior</c> → Handler.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adiciona os serviços da camada Application ao container de DI.
    /// </summary>
    /// <param name="services">Container de serviços.</param>
    /// <returns>O mesmo <see cref="IServiceCollection"/> para encadeamento.</returns>
    public static IServiceCollection AddAuditLogApplication(this IServiceCollection services)
    {
        // MediatR com pipeline de behaviors (ordem importa — mais externo primeiro)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(AssemblyMarker).Assembly);

            // 1ª: Validação sintática (FluentValidation)
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));

            // 2ª: Garante presença de tenant_id no contexto
            cfg.AddOpenBehavior(typeof(TenantContextBehavior<,>));

            // 3ª: Autorização RBAC (apenas para IAuditQuery)
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));

            // 4ª: Logging estruturado sem PII
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        // Validadores FluentValidation (registrados explicitamente, sem dependência de DI extensions)
        services.AddScoped<IValidator<RecordAuditEntryCommand>, RecordAuditEntryCommandValidator>();
        services.AddScoped<IValidator<ListAuditLogsQuery>, ListAuditLogsQueryValidator>();
        services.AddScoped<IValidator<GetEntityAuditHistoryQuery>, GetEntityAuditHistoryQueryValidator>();

        // Porta pública de auditoria (IAuditWriter → AuditServiceWriter)
        services.AddScoped<IAuditWriter, AuditServiceWriter>();

        return services;
    }
}
