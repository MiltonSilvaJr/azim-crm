using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace PartnerManagement.Architecture.Tests;

/// <summary>
/// Testes de arquitetura que garantem as regras de dependência entre camadas
/// conforme design §3 do módulo partner-management.
///
/// Executa em 100% dos PRs e falha o CI em caso de violação (TASK-02, ST-01).
///
/// Regra de dependência (design §3):
///   Api            -> Application, Infrastructure, Contracts (permitido)
///   Application    -> Domain, Contracts (NUNCA Infrastructure)
///   Infrastructure -> Application, Domain, Contracts (permitido)
///   Domain         -> (nenhum projeto interno)
///   Contracts      -> (nenhum projeto interno)
///
/// Direções proibidas validadas aqui:
///   Domain         -x-> Application
///   Domain         -x-> Infrastructure
///   Domain         -x-> Api
///   Application    -x-> Infrastructure
///   Application    -x-> Api
///   Contracts      -x-> Domain
///   Contracts      -x-> Application
///   Contracts      -x-> Infrastructure
///   Contracts      -x-> Api
///
/// Módulo: BC-03 Partner Management (Supporting Subdomain).
/// </summary>
public sealed class DependencyRuleTests
{
    // =========================================================================
    // Grupo A — Domain não referencia nenhum projeto interno (design §3)
    // =========================================================================

    /// <summary>
    /// Regra A-1: tipos em Domain não referenciam Application.
    ///
    /// Domain é a camada mais interna e não pode conhecer casos de uso.
    /// Mapeia: design §3, design §2 (princípio P1 — Clean Architecture).
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Application (design §3)")]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(ArchitectureRules.DomainAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.ApplicationAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Application (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra A-2: tipos em Domain não referenciam Infrastructure.
    ///
    /// Domain não deve conhecer EF Core, Npgsql, GCP SDKs ou qualquer adapter.
    /// Mapeia: design §3, design §2 (P1 — Domain independente de infraestrutura).
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Infrastructure (design §3)")]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ArchitectureRules.DomainAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.InfrastructureAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Infrastructure (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra A-3: tipos em Domain não referenciam Api.
    ///
    /// Dependência de Domain em Api criaria ciclo e acoplamento indevido ao web framework.
    /// Mapeia: design §3.
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Api (design §3)")]
    public void Domain_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(ArchitectureRules.DomainAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.ApiAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Api (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    // =========================================================================
    // Grupo B — Application não referencia Infrastructure nem Api (design §3)
    // =========================================================================

    /// <summary>
    /// Regra B-1: tipos em Application não referenciam Infrastructure.
    ///
    /// Application define ports (interfaces); Infrastructure as implementa.
    /// Violação indicaria dependência invertida — adapter vazando para a porta.
    /// Mapeia: design §3, design §2 (P1).
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
                     "Application define portas; Infrastructure as implementa. " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra B-2: tipos em Application não referenciam Api.
    ///
    /// Application não pode conhecer controllers ou o pipeline HTTP.
    /// Mapeia: design §3.
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
    // Grupo C — Contracts não referencia nenhum projeto interno (design §3)
    // =========================================================================

    /// <summary>
    /// Regra C-1: tipos em Contracts não referenciam Domain.
    ///
    /// Contracts é a camada pública — sem dependências internas.
    /// Mapeia: design §3.
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
    /// Regra C-2: tipos em Contracts não referenciam Application.
    ///
    /// Contracts é consumido por todos os módulos; não pode criar ciclo.
    /// Mapeia: design §3.
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
    /// Regra C-3: tipos em Contracts não referenciam Infrastructure.
    ///
    /// Nenhum adapter de banco, GCP ou mensageria pode aparecer em Contracts.
    /// Mapeia: design §3.
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

    /// <summary>
    /// Regra C-4: tipos em Contracts não referenciam Api.
    ///
    /// Contratos públicos não podem depender do pipeline web.
    /// Mapeia: design §3.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve depender de Api (design §3)")]
    public void Contracts_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(ArchitectureRules.ContractsAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.ApiAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar Api (design §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    // =========================================================================
    // Grupo D — Domain não usa namespaces de infraestrutura (design §2, §3)
    // =========================================================================

    /// <summary>
    /// Regra D: Domain não referencia namespaces de EF Core, Npgsql, GCP SDKs,
    /// ASP.NET Core ou MediatR.
    ///
    /// Garante que o domínio permanece puro e testável sem dependência de framework.
    /// Mapeia: design §2 (P1), design §3.
    /// </summary>
    [Fact(DisplayName = "Domain não deve conter namespaces de infraestrutura (design §2, §3)")]
    public void Domain_ShouldNotContain_InfrastructureNamespaces()
    {
        var result = Types.InAssembly(ArchitectureRules.DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(ArchitectureRules.ForbiddenInfraNamespacesInDomain)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar namespaces de infraestrutura " +
                     $"({string.Join(", ", ArchitectureRules.ForbiddenInfraNamespacesInDomain)}) " +
                     "— domínio deve ser puro e testável sem framework (design §2, §3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }
}
