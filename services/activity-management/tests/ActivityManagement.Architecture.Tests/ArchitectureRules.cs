using System.Reflection;

namespace ActivityManagement.Architecture.Tests;

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

    /// <summary>Assembly de ActivityManagement.Contracts.</summary>
    internal static readonly Assembly ContractsAssembly =
        typeof(ActivityManagement.Contracts.AssemblyReference).Assembly;

    /// <summary>Assembly de ActivityManagement.Domain.</summary>
    internal static readonly Assembly DomainAssembly =
        typeof(ActivityManagement.Domain.AssemblyReference).Assembly;

    /// <summary>Assembly de ActivityManagement.Application.</summary>
    internal static readonly Assembly ApplicationAssembly =
        typeof(ActivityManagement.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de ActivityManagement.Infrastructure.</summary>
    internal static readonly Assembly InfrastructureAssembly =
        typeof(ActivityManagement.Infrastructure.AssemblyReference).Assembly;

    /// <summary>Assembly de ActivityManagement.Api.</summary>
    internal static readonly Assembly ApiAssembly =
        typeof(ActivityManagement.Api.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Nomes de assembly para NotHaveDependencyOn
    // -------------------------------------------------------------------------

    internal const string ContractsAssemblyName = "ActivityManagement.Contracts";
    internal const string DomainAssemblyName = "ActivityManagement.Domain";
    internal const string ApplicationAssemblyName = "ActivityManagement.Application";
    internal const string InfrastructureAssemblyName = "ActivityManagement.Infrastructure";
    internal const string ApiAssemblyName = "ActivityManagement.Api";

    // -------------------------------------------------------------------------
    // Namespaces proibidos no Domain (isolamento de infraestrutura — design §2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Namespaces de infraestrutura proibidos em ActivityManagement.Domain.
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
