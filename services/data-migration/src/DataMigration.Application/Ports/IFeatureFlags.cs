namespace DataMigration.Application.Ports;

/// <summary>
/// Porta de leitura de feature flags.
///
/// Usada pelo <c>FeatureFlagBehavior</c> para verificar se
/// <c>migration.import_enabled</c> está habilitada antes de
/// executar o import (DD-009, design §5.4).
///
/// Implementação em Infrastructure.
///
/// Rastreia: design §5.4, DD-009, TASK-12.
/// </summary>
public interface IFeatureFlags
{
    /// <summary>
    /// Verifica se a flag está habilitada.
    /// </summary>
    /// <param name="flagName">Nome da flag (ex: <c>"migration.import_enabled"</c>).</param>
    /// <returns><c>true</c> quando habilitada.</returns>
    bool IsEnabled(string flagName);
}
