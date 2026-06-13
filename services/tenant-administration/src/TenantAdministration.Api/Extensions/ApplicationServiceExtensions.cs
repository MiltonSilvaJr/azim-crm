using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TenantAdministration.Application.Behaviors;
using TenantAdministration.Application.Ports;
using TenantAdministration.Api.Infrastructure;

namespace TenantAdministration.Api.Extensions;

/// <summary>
/// Extensões de registro dos serviços da Application e Api na DI.
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registra MediatR, behaviors, validadores e serviços da Api.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // MediatR com assemblies de Application
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(
                typeof(Application.AssemblyMarker).Assembly);

            // Pipeline na ordem definida em design.md §5.4
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CorrelationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        });

        // FluentValidation — registra todos os validators da Application
        services.AddValidatorsFromAssembly(
            typeof(Application.AssemblyMarker).Assembly);

        // Contextos do usuário e tenant (implementados na Api)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        return services;
    }
}
