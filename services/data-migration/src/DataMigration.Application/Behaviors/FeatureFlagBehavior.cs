using DataMigration.Application.Commands.Import;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using MediatR;

namespace DataMigration.Application.Behaviors;

/// <summary>
/// Pipeline behavior que bloqueia o <see cref="ExecuteImportCommand"/>
/// quando a feature flag <c>migration.import_enabled</c> está desabilitada.
///
/// Reduz superfície de ataque após a Fase 1 (DD-009, VAL-TRD-11).
///
/// Rastreia: design §5.4, DD-009, TASK-12.
/// </summary>
public sealed class FeatureFlagBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const string ImportEnabledFlag = "migration.import_enabled";

    private readonly IFeatureFlags _featureFlags;

    /// <summary>Cria o behavior com a porta de feature flags injetada.</summary>
    public FeatureFlagBehavior(IFeatureFlags featureFlags)
    {
        _featureFlags = featureFlags;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Aplica apenas para ExecuteImportCommand (DD-009)
        if (request is ExecuteImportCommand && !_featureFlags.IsEnabled(ImportEnabledFlag))
        {
            throw new MigrationDomainException(
                "MIG-ERR-009",
                $"Importação desabilitada nesta fase. " +
                $"Flag '{ImportEnabledFlag}' = false (DD-009). " +
                $"Solicite habilitação ao operador de plataforma.");
        }

        return await next();
    }
}
