using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Reporting.Architecture.Tests;

/// <summary>
/// Testes que validam a pureza do read side (DD-003):
/// Reporting.Domain não deve conter aggregates transacionais.
///
/// O módulo reporting é um Supporting Subdomain read side puro (SD-07).
/// Não há aggregate root próprio; o "domínio" se limita a objetos de valor de
/// leitura imutáveis e enums de escopo.
///
/// Mapeia: TASK-02, DD-003, design §3, §4.1, §4.2.
/// </summary>
public sealed class ReadSidePurityTests
{
    /// <summary>
    /// Regra (f): Domain não contém classes com sufixo "Aggregate".
    ///
    /// Mapeia: DD-003 (read side sem aggregate transacional), design §4.1.
    /// Na estrutura atual (Onda 1), o teste passa trivialmente —
    /// serve como guard permanente para futuros PRs.
    /// </summary>
    [Fact(DisplayName = "Domain não deve conter aggregates transacionais (DD-003, read side puro)")]
    public void Domain_ShouldNotContain_TransactionalAggregates()
    {
        var domainAssembly = ArchitectureRules.DomainAssembly;

        // Obtém todos os tipos do assembly Domain cujo nome termina em "Aggregate"
        var aggregateTypes = domainAssembly
            .GetTypes()
            .Where(t => t.Name.EndsWith("Aggregate", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        aggregateTypes.Should().BeEmpty(
            because: "Reporting.Domain é read side puro — sem aggregates transacionais (DD-003). " +
                     "Adicionar um aggregate aqui violaria a separação de subdomínios (SD-07). " +
                     $"Tipos em violação: [{string.Join(", ", aggregateTypes)}]");
    }

    /// <summary>
    /// Regra (g): Domain não contém classes com sufixo "AggregateRoot".
    ///
    /// Mapeia: DD-003, design §4.1.
    /// Redundância intencional: cobre convenção de nomenclatura alternativa.
    /// </summary>
    [Fact(DisplayName = "Domain não deve conter AggregateRoot (DD-003, read side puro)")]
    public void Domain_ShouldNotContain_AggregateRoot()
    {
        var domainAssembly = ArchitectureRules.DomainAssembly;

        var aggregateRootTypes = domainAssembly
            .GetTypes()
            .Where(t => t.Name.EndsWith("AggregateRoot", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        aggregateRootTypes.Should().BeEmpty(
            because: "Reporting.Domain não deve conter AggregateRoot (DD-003). " +
                     $"Tipos em violação: [{string.Join(", ", aggregateRootTypes)}]");
    }

    /// <summary>
    /// Regra (h): Domain não contém classes com sufixo "Entity" (entidades transacionais).
    ///
    /// Mapeia: DD-003, design §4.2 (sem entidades transacionais com identidade e ciclo de vida).
    /// Objetos de valor e enums são permitidos; entidades EF/transacionais não.
    /// </summary>
    [Fact(DisplayName = "Domain não deve conter entidades transacionais com sufixo Entity (DD-003)")]
    public void Domain_ShouldNotContain_TransactionalEntities()
    {
        var domainAssembly = ArchitectureRules.DomainAssembly;

        var entityTypes = domainAssembly
            .GetTypes()
            .Where(t => t.Name.EndsWith("Entity", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        entityTypes.Should().BeEmpty(
            because: "Reporting.Domain não deve conter entidades transacionais (DD-003, design §4.2). " +
                     "Apenas objetos de valor de leitura e enums são permitidos. " +
                     $"Tipos em violação: [{string.Join(", ", entityTypes)}]");
    }
}
