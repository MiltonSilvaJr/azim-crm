using System.Reflection;

namespace AccountManagement.Architecture.Tests;

/// <summary>
/// Predicados de arquitetura reutilizáveis extraídos das regras do design §3.
///
/// Cada constante rastreia exatamente uma decisão de design para facilitar
/// a localização do motivo em caso de falha de CI.
///
/// Regra de dependência (design §3, design §2 — P1):
///   Api            -> Application, Infrastructure, Contracts (wiring DI)
///   Application    -> Domain, Contracts (NUNCA Infrastructure)
///   Infrastructure -> Application, Domain, Contracts
///   Domain         -> (nenhum projeto interno)
///   Contracts      -> (nenhum projeto interno)
///
/// Namespaces proibidos em Domain (isolamento de infraestrutura — design §2):
///   EF Core, Npgsql, GCP SDKs, ASP.NET Core, MediatR.
/// </summary>
internal static class ArchitectureRules
{
    // -------------------------------------------------------------------------
    // Assemblies de produção (resolvidos via marcadores AssemblyReference)
    // -------------------------------------------------------------------------

    /// <summary>Assembly de AccountManagement.Contracts.</summary>
    internal static readonly Assembly ContractsAssembly =
        typeof(AccountManagement.Contracts.AssemblyReference).Assembly;

    /// <summary>Assembly de AccountManagement.Domain.</summary>
    internal static readonly Assembly DomainAssembly =
        typeof(AccountManagement.Domain.AssemblyReference).Assembly;

    /// <summary>Assembly de AccountManagement.Application.</summary>
    internal static readonly Assembly ApplicationAssembly =
        typeof(AccountManagement.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de AccountManagement.Infrastructure.</summary>
    internal static readonly Assembly InfrastructureAssembly =
        typeof(AccountManagement.Infrastructure.AssemblyReference).Assembly;

    /// <summary>Assembly de AccountManagement.Api.</summary>
    internal static readonly Assembly ApiAssembly =
        typeof(AccountManagement.Api.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Nomes de assembly para NotHaveDependencyOn
    // -------------------------------------------------------------------------

    internal const string ContractsAssemblyName = "AccountManagement.Contracts";
    internal const string DomainAssemblyName = "AccountManagement.Domain";
    internal const string ApplicationAssemblyName = "AccountManagement.Application";
    internal const string InfrastructureAssemblyName = "AccountManagement.Infrastructure";
    internal const string ApiAssemblyName = "AccountManagement.Api";

    // -------------------------------------------------------------------------
    // Namespaces proibidos no Domain (isolamento de infraestrutura — design §2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Namespaces de infraestrutura proibidos em AccountManagement.Domain.
    /// Domain não deve depender de EF Core, drivers de banco, mensageria,
    /// GCP SDKs ou framework web (design §3, design §2 — P1).
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
