using Xunit;

namespace Reporting.Infrastructure.Tests;

/// <summary>
/// Definição da coleção xUnit que compartilha o <see cref="PostgresContainerFixture"/>
/// entre todos os testes de integração do módulo Infrastructure.
///
/// Garante que o container PostgreSQL é iniciado uma única vez por execução de testes,
/// reduzindo o overhead de Docker.
///
/// Mapeia: TASK-18, design §13.3.
/// </summary>
[CollectionDefinition("PostgresContainer")]
public sealed class PostgresContainerCollection : ICollectionFixture<PostgresContainerFixture>
{
    // Classe marcadora — sem implementação necessária.
    // A fixture é injetada por construtor em todas as classes com [Collection("PostgresContainer")].
}
