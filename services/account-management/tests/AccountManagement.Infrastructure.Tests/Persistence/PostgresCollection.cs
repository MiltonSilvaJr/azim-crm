namespace AccountManagement.Infrastructure.Tests.Persistence;

/// <summary>
/// Definição de collection xUnit para compartilhar o <see cref="PostgresFixture"/>
/// entre todos os testes de Infrastructure que usam PostgreSQL real.
///
/// O container Testcontainers é iniciado uma única vez por collection (performance).
/// Mapeia: TASK-08 (ST-01), TASK-09 (ST-01), TASK-10 (ST-01).
/// </summary>
[CollectionDefinition("PostgresFixture")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    // Esta classe não contém código — apenas a definição da collection.
}
