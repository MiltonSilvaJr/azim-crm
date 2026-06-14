using System.Reflection;

namespace OpportunityPipeline.Architecture.Tests;

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

    /// <summary>Assembly de OpportunityPipeline.Contracts.</summary>
    internal static readonly Assembly ContractsAssembly =
        typeof(OpportunityPipeline.Contracts.AssemblyReference).Assembly;

    /// <summary>Assembly de OpportunityPipeline.Domain.</summary>
    internal static readonly Assembly DomainAssembly =
        typeof(OpportunityPipeline.Domain.AssemblyReference).Assembly;

    /// <summary>Assembly de OpportunityPipeline.Application.</summary>
    internal static readonly Assembly ApplicationAssembly =
        typeof(OpportunityPipeline.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de OpportunityPipeline.Infrastructure.</summary>
    internal static readonly Assembly InfrastructureAssembly =
        typeof(OpportunityPipeline.Infrastructure.AssemblyReference).Assembly;

    /// <summary>Assembly de OpportunityPipeline.Api.</summary>
    internal static readonly Assembly ApiAssembly =
        typeof(OpportunityPipeline.Api.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Nomes de assembly para NotHaveDependencyOn
    // -------------------------------------------------------------------------

    internal const string ContractsAssemblyName = "OpportunityPipeline.Contracts";
    internal const string DomainAssemblyName = "OpportunityPipeline.Domain";
    internal const string ApplicationAssemblyName = "OpportunityPipeline.Application";
    internal const string InfrastructureAssemblyName = "OpportunityPipeline.Infrastructure";
    internal const string ApiAssemblyName = "OpportunityPipeline.Api";

    // -------------------------------------------------------------------------
    // Namespaces proibidos no Domain (isolamento de infraestrutura — design §2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Namespaces de infraestrutura proibidos em OpportunityPipeline.Domain.
    /// Domain não deve depender de EF Core, drivers de banco, mensageria,
    /// Redis, GCP SDKs ou framework web (design §3, design §2, RNF 11).
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
