using System.Reflection;

namespace Organization.Architecture.Tests;

/// <summary>
/// Predicados de arquitetura reutilizáveis extraídos das regras do design §3.
///
/// Cada constante rastreia exatamente uma decisão de design para facilitar
/// a localização do motivo em caso de falha de CI.
///
/// Regra de dependência (design §3, design §2):
///   Api            -> Application, Infrastructure, Contracts (wiring DI)
///   Application    -> Domain, Contracts (NUNCA Infrastructure)
///   Infrastructure -> Application, Domain, Contracts
///   Domain         -> (nenhum projeto interno)
///   Contracts      -> (nenhum projeto interno)
///
/// Namespaces proibidos em Domain (isolamento de infraestrutura):
///   EF Core, Npgsql, Redis, GCP SDKs, ASP.NET Core, MediatR.
/// </summary>
internal static class ArchitectureRules
{
    // -------------------------------------------------------------------------
    // Assemblies de produção (resolvidos via marcadores AssemblyReference)
    // -------------------------------------------------------------------------

    /// <summary>Assembly de Organization.Contracts.</summary>
    internal static readonly Assembly ContractsAssembly =
        typeof(Organization.Contracts.AssemblyReference).Assembly;

    /// <summary>Assembly de Organization.Domain.</summary>
    internal static readonly Assembly DomainAssembly =
        typeof(Organization.Domain.AssemblyReference).Assembly;

    /// <summary>Assembly de Organization.Application.</summary>
    internal static readonly Assembly ApplicationAssembly =
        typeof(Organization.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de Organization.Infrastructure.</summary>
    internal static readonly Assembly InfrastructureAssembly =
        typeof(Organization.Infrastructure.AssemblyReference).Assembly;

    /// <summary>Assembly de Organization.Api.</summary>
    internal static readonly Assembly ApiAssembly =
        typeof(Organization.Api.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Nomes de assembly para NotHaveDependencyOn
    // -------------------------------------------------------------------------

    internal const string ContractsAssemblyName = "Organization.Contracts";
    internal const string DomainAssemblyName = "Organization.Domain";
    internal const string ApplicationAssemblyName = "Organization.Application";
    internal const string InfrastructureAssemblyName = "Organization.Infrastructure";
    internal const string ApiAssemblyName = "Organization.Api";

    // -------------------------------------------------------------------------
    // Namespaces proibidos no Domain (isolamento de infraestrutura — design §2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Namespaces de infraestrutura proibidos em Organization.Domain.
    /// Domain não deve depender de EF Core, drivers de banco, mensageria,
    /// Redis, GCP SDKs ou framework web (design §3, design §2).
    /// </summary>
    internal static readonly string[] ForbiddenInfraNamespacesInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "StackExchange.Redis",
        "Google.Cloud",
        "Google.Apis",
        "FirebaseAdmin",
        "Microsoft.AspNetCore",
        "MediatR"
    ];

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    internal static string FormatFailingTypes(NetArchTest.Rules.TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? []);
}
