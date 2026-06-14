using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Reporting.Architecture.Tests;

/// <summary>
/// Testes de arquitetura que garantem as regras de dependência do design §3 e a
/// ausência de aggregates transacionais em Domain (DD-003, read side puro).
///
/// Executam em 100% dos PRs e falham o CI em caso de violação.
///
/// Regra de dependência (design §3):
///   Api            -> Application, Infrastructure, Contracts
///   Application    -> Domain, Contracts  (NUNCA Infrastructure)
///   Infrastructure -> Application, Domain, Contracts  (NUNCA Api)
///   Domain         -> ∅  (NUNCA Application, Infrastructure, Contracts)
///   Contracts      -> ∅  (NUNCA nenhum projeto interno)
///
/// Mapeia: TASK-02, design §3, DD-003, ADR-0001.
/// </summary>
public sealed class DependencyRuleTests
{
    // =========================================================================
    // Camada Domain — não referencia nenhum projeto interno (design §3, DD-003)
    // =========================================================================

    /// <summary>
    /// Regra (a-1): tipos em Domain não referenciam Application.
    ///
    /// Mapeia: design §3 (Domain → ∅), DD-003.
    /// Domain é a camada mais interna do read side; sem dependências internas.
    /// Garante futuramente ao adicionar objetos de valor (Onda 2).
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Application (design §3, DD-003)")]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(ArchitectureRules.DomainAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.ApplicationAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Application (design §3, DD-003). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (a-2): tipos em Domain não referenciam Infrastructure.
    ///
    /// Mapeia: design §3 (Domain → ∅), DD-003.
    /// Nenhum adapter de banco, GCS ou RLS pode vazar para o domínio de leitura.
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Infrastructure (design §3, DD-003)")]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ArchitectureRules.DomainAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.InfrastructureAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Infrastructure (design §3, DD-003). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (a-3): tipos em Domain não referenciam Contracts.
    ///
    /// Mapeia: design §3 (Domain → ∅).
    /// Reporting.Domain é a camada mais interna; sem dependências internas — inclusive Contracts.
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Contracts (design §3)")]
    public void Domain_ShouldNotDependOn_Contracts()
    {
        var result = Types.InAssembly(ArchitectureRules.DomainAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.ContractsAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Contracts (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    // =========================================================================
    // Camada Contracts — sem referências internas (design §3)
    // =========================================================================

    /// <summary>
    /// Regra (b-1): tipos em Contracts não referenciam Domain.
    ///
    /// Mapeia: design §3 (Contracts → ∅).
    /// Contracts é consumido por todos; não pode criar ciclo com Domain.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve depender de Domain (design §3)")]
    public void Contracts_ShouldNotDependOn_Domain()
    {
        var result = Types.InAssembly(ArchitectureRules.ContractsAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.DomainAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar Domain (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (b-2): tipos em Contracts não referenciam Application.
    ///
    /// Mapeia: design §3 (Contracts → ∅).
    /// Contratos públicos não podem depender de lógica de aplicação.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve depender de Application (design §3)")]
    public void Contracts_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(ArchitectureRules.ContractsAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.ApplicationAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar Application (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (b-3): tipos em Contracts não referenciam Infrastructure.
    ///
    /// Mapeia: design §3 (Contracts → ∅).
    /// Nenhum SDK de banco, GCS ou adapter pode aparecer em Contracts.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve depender de Infrastructure (design §3)")]
    public void Contracts_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ArchitectureRules.ContractsAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.InfrastructureAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar Infrastructure (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    // =========================================================================
    // Camada Application — não referencia Infrastructure (design §3)
    // =========================================================================

    /// <summary>
    /// Regra (c): tipos em Application não referenciam Infrastructure.
    ///
    /// Mapeia: design §3 (Application → Domain, Contracts — NUNCA Infrastructure).
    /// Application define portas (interfaces); Infrastructure as implementa.
    /// Violação indicaria dependência invertida — adapter vazando para a porta.
    /// </summary>
    [Fact(DisplayName = "Application não deve depender de Infrastructure (design §3)")]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ArchitectureRules.ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.InfrastructureAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não deve referenciar Infrastructure (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (c-2): tipos em Application não referenciam Api.
    ///
    /// Mapeia: design §3. Dependência invertida de Application para Api é proibida.
    /// </summary>
    [Fact(DisplayName = "Application não deve depender de Api (design §3)")]
    public void Application_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(ArchitectureRules.ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.ApiAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não deve referenciar Api (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    // =========================================================================
    // Camada Infrastructure — não referencia Api (design §3)
    // =========================================================================

    /// <summary>
    /// Regra (d): tipos em Infrastructure não referenciam Api.
    ///
    /// Mapeia: design §3 (Infrastructure → Application, Domain, Contracts — NUNCA Api).
    /// Infrastructure é adaptador; não deve conhecer o layer de apresentação HTTP.
    /// </summary>
    [Fact(DisplayName = "Infrastructure não deve depender de Api (design §3)")]
    public void Infrastructure_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(ArchitectureRules.InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.ApiAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Infrastructure não deve referenciar Api (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    // =========================================================================
    // Camada Api — não acessa Domain diretamente (design §3)
    // =========================================================================

    /// <summary>
    /// Regra (e): tipos em Api não referenciam Domain diretamente.
    ///
    /// Mapeia: design §3 (Api → Application, Infrastructure, Contracts — NUNCA Domain diretamente).
    /// Api acessa domínio exclusivamente via Application (mediator/handlers).
    /// Garante que nenhum objeto de valor de leitura vaze para o layer HTTP sem passar pela Application.
    /// </summary>
    [Fact(DisplayName = "Api não deve depender de Domain diretamente (design §3)")]
    public void Api_ShouldNotDependOn_Domain()
    {
        var result = Types.InAssembly(ArchitectureRules.ApiAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.DomainAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Api não deve referenciar Domain diretamente (design §3). " +
                     "Acesso ao domínio deve ocorrer via Application. " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }
}
