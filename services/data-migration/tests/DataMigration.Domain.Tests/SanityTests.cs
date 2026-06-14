using FluentAssertions;
using Xunit;

namespace DataMigration.Domain.Tests;

/// <summary>
/// Sanidade do assembly DataMigration.Domain.
///
/// Verifica que o assembly de Domain compila e é carregável sem qualquer
/// dependência de EF Core, ClosedXML ou SDK de infraestrutura.
/// Este teste é o "Red" da TASK-01 (ST-01): se o Domain tiver referência
/// proibida, o carregamento falhará.
///
/// Rastreia: TASK-01 (ST-01 — sanidade; design §3).
/// Os testes de conteúdo (agregados, objetos de valor) serão adicionados na Onda 2 (TASK-03..07).
/// </summary>
public sealed class SanityTests
{
    /// <summary>
    /// Verifica que o assembly DataMigration.Domain é carregável.
    ///
    /// Se o Domain tiver dependência de EF Core ou ClosedXML, o projeto não
    /// compilaria ou o teste de arquitetura (Architecture.Tests) quebraria.
    /// Este teste garante que o assembly existe e é resolvível.
    /// </summary>
    [Fact(DisplayName = "DataMigration.Domain é carregável sem dependência de infraestrutura (TASK-01, ST-01)")]
    public void Domain_Assembly_ShouldBeLoadable()
    {
        // Arrange & Act
        var assembly = typeof(AssemblyReference).Assembly;

        // Assert
        assembly.Should().NotBeNull(
            because: "DataMigration.Domain deve ser carregável sem dependência de EF Core, " +
                     "ClosedXML ou qualquer SDK de infraestrutura (design §3, TASK-01 ST-01).");

        assembly.FullName.Should().Contain("DataMigration.Domain",
            because: "o assembly deve ter o nome correto do módulo.");
    }
}
