namespace GoalForecast.Infrastructure.Tests.Fixtures;

/// <summary>
/// Coleção xUnit que compartilha o container PostgreSQL entre todas as classes
/// de teste de infraestrutura. Evita múltiplos containers simultâneos.
/// Mapeia: TASK-15..20.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Database";
}
