using FluentValidation;
using MediatR;
using Organization.Application.Behaviors;

namespace Organization.Api.DependencyInjection;

/// <summary>
/// Extensões de registro de serviços de Application na composição raiz (Program.cs).
/// Centraliza MediatR, FluentValidation e pipeline behaviors.
/// </summary>
public static class ApplicationExtensions
{
    /// <summary>
    /// Registra MediatR, behaviors e validadores da camada Application.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <returns>A mesma coleção para encadeamento.</returns>
    public static IServiceCollection AddOrganizationApplication(
        this IServiceCollection services)
    {
        // MediatR — descobre handlers em Organization.Application
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(
                typeof(Organization.Application.AssemblyReference).Assembly);
        });

        // FluentValidation — descobre validators em Organization.Application
        services.AddValidatorsFromAssembly(
            typeof(Organization.Application.AssemblyReference).Assembly,
            includeInternalTypes: true);

        // Pipeline behaviors — ordem: Logging → TenantContext → RBAC → Validation → Idempotency → Transaction → Handler
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TenantContextBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RbacAuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
