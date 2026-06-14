using Xunit;

namespace DataMigration.Infrastructure.Tests.Persistence;

/// <summary>
/// Define a coleção xUnit que compartilha o contêiner PostgreSQL entre
/// todos os testes de integração de persistência.
///
/// Rastreia: TASK-15 (Testcontainers, RLS).
/// </summary>
[CollectionDefinition("PostgresContainer")]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresContainerFixture>;
