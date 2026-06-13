using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace TenantAdministration.Architecture.Tests;

/// <summary>
/// Valida as regras de dependência da Clean Architecture no módulo TenantAdministration.
/// Qualquer violação de referência proibida fará estes testes falhar.
///
/// Grafo permitido (design.md §3):
///   Api  → Application, Infrastructure, Contracts
///   Infrastructure → Application, Domain, Contracts
///   Application → Domain, Contracts
///   Domain → ∅
///   Contracts → ∅
/// </summary>
public sealed class DependencyRuleTests
{
    // Nomes dos assemblies de produção
    private const string DomainAssembly = "TenantAdministration.Domain";
    private const string ContractsAssembly = "TenantAdministration.Contracts";
    private const string ApplicationAssembly = "TenantAdministration.Application";
    private const string InfrastructureAssembly = "TenantAdministration.Infrastructure";
    private const string ApiAssembly = "TenantAdministration.Api";

    /// <summary>
    /// Carrega todos os assemblies de produção via AssemblyMarker de cada projeto.
    /// </summary>
    private static Types AllProductionTypes => Types.InAssemblies(
    [
        typeof(TenantAdministration.Domain.AssemblyMarker).Assembly,
        typeof(TenantAdministration.Contracts.AssemblyMarker).Assembly,
        typeof(TenantAdministration.Application.AssemblyMarker).Assembly,
        typeof(TenantAdministration.Infrastructure.AssemblyMarker).Assembly,
        typeof(TenantAdministration.Api.AssemblyMarker).Assembly,
    ]);

    // -----------------------------------------------------------------------
    // Regras do Domain — sem dependências externas (Domain → ∅)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Domain não deve depender de Infrastructure")]
    public void Domain_ShouldNot_DependOn_Infrastructure()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(DomainAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "o Domain não pode conhecer a Infrastructure (regra Clean Architecture)");
    }

    [Fact(DisplayName = "Domain não deve depender de Application")]
    public void Domain_ShouldNot_DependOn_Application()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(DomainAssembly)
            .ShouldNot().HaveDependencyOn(ApplicationAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "o Domain não pode conhecer a Application (regra Clean Architecture)");
    }

    [Fact(DisplayName = "Domain não deve depender de Api")]
    public void Domain_ShouldNot_DependOn_Api()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(DomainAssembly)
            .ShouldNot().HaveDependencyOn(ApiAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "o Domain não pode conhecer a Api");
    }

    [Fact(DisplayName = "Domain não deve depender de Microsoft.EntityFrameworkCore")]
    public void Domain_ShouldNot_DependOn_EntityFrameworkCore()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(DomainAssembly)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "o Domain não pode ter dependência de EF Core (design.md §2, §3)");
    }

    [Fact(DisplayName = "Domain não deve depender de GCP SDK")]
    public void Domain_ShouldNot_DependOn_GcpSdk()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(DomainAssembly)
            .ShouldNot().HaveDependencyOn("Google")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "o Domain não pode conhecer GCP SDK (design.md §3)");
    }

    [Fact(DisplayName = "Domain não deve depender de framework web (Microsoft.AspNetCore)")]
    public void Domain_ShouldNot_DependOn_AspNetCore()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(DomainAssembly)
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "o Domain não pode conhecer frameworks web (design.md §3)");
    }

    // -----------------------------------------------------------------------
    // Regras do Contracts — sem dependências internas (Contracts → ∅)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Contracts não deve depender de Domain")]
    public void Contracts_ShouldNot_DependOn_Domain()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ContractsAssembly)
            .ShouldNot().HaveDependencyOn(DomainAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts é uma camada sem dependências internas (design.md §3)");
    }

    [Fact(DisplayName = "Contracts não deve depender de Application")]
    public void Contracts_ShouldNot_DependOn_Application()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ContractsAssembly)
            .ShouldNot().HaveDependencyOn(ApplicationAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts é uma camada sem dependências internas (design.md §3)");
    }

    [Fact(DisplayName = "Contracts não deve depender de Infrastructure")]
    public void Contracts_ShouldNot_DependOn_Infrastructure()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ContractsAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts é uma camada sem dependências internas (design.md §3)");
    }

    [Fact(DisplayName = "Contracts não deve depender de Api")]
    public void Contracts_ShouldNot_DependOn_Api()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ContractsAssembly)
            .ShouldNot().HaveDependencyOn(ApiAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts é uma camada sem dependências internas (design.md §3)");
    }

    // -----------------------------------------------------------------------
    // Regras da Application — Application → Domain, Contracts (não Infrastructure, não Api)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Application não deve depender de Infrastructure")]
    public void Application_ShouldNot_DependOn_Infrastructure()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não pode conhecer detalhes de Infrastructure (design.md §3)");
    }

    [Fact(DisplayName = "Application não deve depender de Api")]
    public void Application_ShouldNot_DependOn_Api()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(ApiAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não pode conhecer a camada de entrega (Api) (design.md §3)");
    }

    [Fact(DisplayName = "Application não deve depender de Microsoft.EntityFrameworkCore")]
    public void Application_ShouldNot_DependOn_EntityFrameworkCore()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não pode ter dependência direta de EF Core (design.md §3)");
    }

    // -----------------------------------------------------------------------
    // Regras da Infrastructure — Infrastructure não acessa Api
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Infrastructure não deve depender de Api")]
    public void Infrastructure_ShouldNot_DependOn_Api()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn(ApiAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Infrastructure não deve conhecer a camada de entrega (Api) (design.md §3)");
    }

    // -----------------------------------------------------------------------
    // Regra crítica: Api não acessa Domain diretamente — apenas via Application
    // (design.md §2, §3: "Api → Application"; Domain é acessado só por Application/Infrastructure)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Api não deve depender de Domain diretamente (apenas via Application)")]
    public void Api_ShouldNot_DependOn_Domain_Directly()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ApiAssembly)
            .ShouldNot().HaveDependencyOn(DomainAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "a Api não deve acessar o Domain diretamente — apenas via Application (design.md §2, §3)");
    }
}
