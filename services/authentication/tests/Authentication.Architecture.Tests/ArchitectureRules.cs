using System.Reflection;

namespace Authentication.Architecture.Tests;

/// <summary>
/// Predicados de arquitetura reutilizáveis extraídos das regras DD-001.
///
/// Cada constante rastreia exatamente um requisito ou decisão de design para
/// facilitar localização do motivo em caso de falha.
///
/// Regra de dependência (DD-001):
///   Api            -> Application, Contracts, Infrastructure (wiring DI)
///   Application    -> Domain, Contracts
///   Infrastructure -> Application, Domain, Contracts
///   Domain         -> Contracts (apenas)
///   Contracts      -> (nenhum projeto interno)
///
/// Confinamento ACL (DD-001, Req 6.2, 6.3, RISK-AUTH-03):
///   Firebase Admin SDK SOMENTE em Infrastructure.
///   Símbolo identity_uid SOMENTE em Infrastructure.
/// </summary>
internal static class ArchitectureRules
{
    // -------------------------------------------------------------------------
    // Assemblies de produção (resolvidos via marcadores AssemblyReference)
    // -------------------------------------------------------------------------

    /// <summary>Assembly de Authentication.Contracts.</summary>
    internal static readonly Assembly ContractsAssembly =
        typeof(Authentication.Contracts.AssemblyReference).Assembly;

    /// <summary>Assembly de Authentication.Domain.</summary>
    internal static readonly Assembly DomainAssembly =
        typeof(Authentication.Domain.AssemblyReference).Assembly;

    /// <summary>Assembly de Authentication.Application.</summary>
    internal static readonly Assembly ApplicationAssembly =
        typeof(Authentication.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de Authentication.Infrastructure.</summary>
    internal static readonly Assembly InfrastructureAssembly =
        typeof(Authentication.Infrastructure.AssemblyReference).Assembly;

    /// <summary>Assembly de Authentication.Api.</summary>
    internal static readonly Assembly ApiAssembly =
        typeof(Authentication.Api.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Nomes de assembly para NotHaveDependencyOn
    // -------------------------------------------------------------------------

    internal const string ContractsAssemblyName = "Authentication.Contracts";
    internal const string DomainAssemblyName = "Authentication.Domain";
    internal const string ApplicationAssemblyName = "Authentication.Application";
    internal const string InfrastructureAssemblyName = "Authentication.Infrastructure";
    internal const string ApiAssemblyName = "Authentication.Api";

    // -------------------------------------------------------------------------
    // Namespaces proibidos fora de Infrastructure (DD-001, Req 6.2, RISK-AUTH-03)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Prefixos de namespace do Firebase Admin SDK para .NET.
    /// Proibidos em Domain, Application, Contracts e Api.
    ///
    /// Mapeia: DD-001 (confinamento ACL), Req 6.2, 6.3, RISK-AUTH-03.
    /// Se o SDK for atualizado com novo prefixo, adicionar aqui.
    /// </summary>
    internal static readonly string[] ForbiddenFirebaseNamespaces =
    [
        "FirebaseAdmin",
        "Google.Apis.Auth",
        "Google.Cloud.Firestore"
    ];

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    internal static string FormatFailingTypes(NetArchTest.Rules.TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? []);
}
