using AccountManagement.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AccountManagement.Application.DependencyInjection;

/// <summary>
/// Extensões de DI para registrar todos os serviços da camada Application.
///
/// Registra MediatR com behaviors na ordem correta (design §5.4),
/// FluentValidation e contextos de request scoped.
///
/// Chamado pelo <c>Program.cs</c> da Api.
///
/// Mapeia: design §5.4, TASK-13.
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registra Application layer no container de DI.
    /// </summary>
    /// <param name="services">Container de serviços.</param>
    /// <returns>O container para encadeamento.</returns>
    public static IServiceCollection AddAccountManagementApplication(
        this IServiceCollection services)
    {
        // MediatR com behaviors internos (registrados aqui pois os tipos são internal)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly);

            // Pipeline MediatR — ordem de execução (design §5.4):
            // 1. CorrelationLoggingBehavior
            cfg.AddBehavior(typeof(IPipelineBehavior<,>),
                typeof(CorrelationLoggingBehavior<,>));
            // 2. TenantScopeBehavior
            cfg.AddBehavior(typeof(IPipelineBehavior<,>),
                typeof(TenantScopeBehavior<,>));
            // 3. BuScopeBehavior (após TenantScope — ADR-0009)
            cfg.AddBehavior(typeof(IPipelineBehavior<,>),
                typeof(BuScopeBehavior<,>));
            // 4. ValidationBehavior
            cfg.AddBehavior(typeof(IPipelineBehavior<,>),
                typeof(ValidationBehavior<,>));
            // 5. PiiAccessBehavior
            cfg.AddBehavior(typeof(IPipelineBehavior<,>),
                typeof(PiiAccessBehavior<,>));
            // 6. TransactionBehavior
            cfg.AddBehavior(typeof(IPipelineBehavior<,>),
                typeof(TransactionBehavior<,>));
        });

        // FluentValidation — todos os validators do assembly Application
        services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);

        // Contextos scoped (populados pelo middleware e behaviors)
        services.AddScoped<TenantContext>();
        services.AddScoped<UserContext>();
        services.AddScoped<CorrelationContext>();
        services.AddScoped<BuScopeContext>();

        return services;
    }
}
