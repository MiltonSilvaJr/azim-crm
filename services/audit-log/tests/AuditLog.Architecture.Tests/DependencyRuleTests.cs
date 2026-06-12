using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace AuditLog.Architecture.Tests;

/// <summary>
/// Valida as regras de dependência da Clean Architecture no módulo AuditLog.
/// Qualquer violação de referência proibida fará estes testes falhar.
/// </summary>
public sealed class DependencyRuleTests
{
    // Nomes dos assemblies de produção
    private const string DomainAssembly = "AuditLog.Domain";
    private const string ContractsAssembly = "AuditLog.Contracts";
    private const string ApplicationAssembly = "AuditLog.Application";
    private const string InfrastructureAssembly = "AuditLog.Infrastructure";
    private const string ApiAssembly = "AuditLog.Api";

    // Namespace raiz para carregar todos os assemblies de produção
    private static Types AllProductionTypes => Types.InAssemblies(
    [
        typeof(AuditLog.Domain.AssemblyMarker).Assembly,
        typeof(AuditLog.Contracts.AssemblyMarker).Assembly,
        typeof(AuditLog.Application.AssemblyMarker).Assembly,
        typeof(AuditLog.Infrastructure.AssemblyMarker).Assembly,
        typeof(AuditLog.Api.AssemblyMarker).Assembly,
    ]);

    // -----------------------------------------------------------------------
    // Regras do Domain
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
            because: "o Domain não pode ter dependência de EF Core");
    }

    // -----------------------------------------------------------------------
    // Regras do Contracts
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Contracts não deve depender de Domain")]
    public void Contracts_ShouldNot_DependOn_Domain()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ContractsAssembly)
            .ShouldNot().HaveDependencyOn(DomainAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts é uma camada sem dependências internas");
    }

    [Fact(DisplayName = "Contracts não deve depender de Application")]
    public void Contracts_ShouldNot_DependOn_Application()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ContractsAssembly)
            .ShouldNot().HaveDependencyOn(ApplicationAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts é uma camada sem dependências internas");
    }

    [Fact(DisplayName = "Contracts não deve depender de Infrastructure")]
    public void Contracts_ShouldNot_DependOn_Infrastructure()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ContractsAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts é uma camada sem dependências internas");
    }

    [Fact(DisplayName = "Contracts não deve depender de Api")]
    public void Contracts_ShouldNot_DependOn_Api()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ContractsAssembly)
            .ShouldNot().HaveDependencyOn(ApiAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts é uma camada sem dependências internas");
    }

    // -----------------------------------------------------------------------
    // Regras da Application
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Application não deve depender de Infrastructure")]
    public void Application_ShouldNot_DependOn_Infrastructure()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não pode conhecer detalhes de Infrastructure");
    }

    [Fact(DisplayName = "Application não deve depender de Api")]
    public void Application_ShouldNot_DependOn_Api()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(ApiAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não pode conhecer a camada de entrega (Api)");
    }

    [Fact(DisplayName = "Application não deve depender de Microsoft.EntityFrameworkCore")]
    public void Application_ShouldNot_DependOn_EntityFrameworkCore()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não pode ter dependência direta de EF Core");
    }

    // -----------------------------------------------------------------------
    // Regras da Infrastructure
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Infrastructure não deve depender de Api")]
    public void Infrastructure_ShouldNot_DependOn_Api()
    {
        var result = AllProductionTypes
            .That().ResideInNamespace(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn(ApiAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Infrastructure não deve conhecer a camada de entrega (Api)");
    }
}
