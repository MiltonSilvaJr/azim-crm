using System.Reflection;
using Authentication.Application.Ports;
using FluentAssertions;

namespace Authentication.Application.Tests.Ports;

/// <summary>
/// Testes de contrato das portas de saída da camada Application.
///
/// Verificam que nenhuma porta declara tipo do Firebase SDK ou símbolo identity_uid
/// (DD-001, Req 6.2, 6.3, TASK-05 ST-01).
///
/// Mapeia: TASK-05, design.md § 6.4, DD-001.
/// </summary>
public sealed class ApplicationPortsContractTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(IIdentityProvider).Assembly;

    [Fact(DisplayName = "Nenhuma porta de Application referencia tipo do Firebase Admin SDK")]
    public void Ports_ShouldNotReferenceFirebaseAdminSdk()
    {
        // Arrange
        var portTypes = ApplicationAssembly
            .GetExportedTypes()
            .Where(t => t.IsInterface && t.Namespace != null &&
                        t.Namespace.StartsWith("Authentication.Application.Ports", StringComparison.Ordinal))
            .ToList();

        portTypes.Should().NotBeEmpty("deve haver pelo menos uma porta de saída definida");

        // Act & Assert
        foreach (var portType in portTypes)
        {
            // Verificar métodos e propriedades
            var allTypes = GetAllReferencedTypes(portType);
            allTypes.Should().NotContain(
                t => t.Namespace != null && t.Namespace.Contains("Firebase", StringComparison.OrdinalIgnoreCase),
                $"a porta {portType.Name} não deve referenciar tipos do Firebase SDK (DD-001)");
        }
    }

    [Fact(DisplayName = "Nenhuma porta de Application declara parâmetro ou retorno com nome identity_uid")]
    public void Ports_ShouldNotExposeIdentityUidSymbol()
    {
        // Arrange
        var portTypes = ApplicationAssembly
            .GetExportedTypes()
            .Where(t => t.IsInterface && t.Namespace != null &&
                        t.Namespace.StartsWith("Authentication.Application.Ports", StringComparison.Ordinal))
            .ToList();

        // Act & Assert — verifica exatamente o símbolo "identity_uid" (com underscore)
        // O prefixo "identityUidOpaque" é aceitável pois é explicitamente opaco.
        // O que se proíbe é o símbolo literal identity_uid que identifica o campo externo.
        foreach (var portType in portTypes)
        {
            var methods = portType.GetMethods();
            foreach (var method in methods)
            {
                // Verificar parâmetros
                foreach (var param in method.GetParameters())
                {
                    param.Name.Should().NotBe("identity_uid",
                        $"porta {portType.Name}.{method.Name}: parâmetro com nome exato 'identity_uid' proibido (DD-001)");
                    param.Name.Should().NotBe("identityUid",
                        $"porta {portType.Name}.{method.Name}: parâmetro com nome exato 'identityUid' proibido (DD-001)");
                }
            }
        }
    }

    [Fact(DisplayName = "IIdentityProvider deve declarar os 5 métodos definidos em design.md § 6.4")]
    public void IIdentityProvider_ShouldDeclareAllFiveMethods()
    {
        // Arrange
        var type = typeof(IIdentityProvider);

        // Act
        var methodNames = type.GetMethods().Select(m => m.Name).ToList();

        // Assert
        methodNames.Should().Contain(nameof(IIdentityProvider.VerifyTokenAsync),
            "IIdentityProvider deve assinar VerifyTokenAsync (design.md § 6.4)");
        methodNames.Should().Contain(nameof(IIdentityProvider.RevokeRefreshTokensAsync),
            "IIdentityProvider deve assinar RevokeRefreshTokensAsync (design.md § 6.4)");
        methodNames.Should().Contain(nameof(IIdentityProvider.GenerateInviteActivationAsync),
            "IIdentityProvider deve assinar GenerateInviteActivationAsync (design.md § 6.4)");
        methodNames.Should().Contain(nameof(IIdentityProvider.GeneratePasswordResetLinkAsync),
            "IIdentityProvider deve assinar GeneratePasswordResetLinkAsync (design.md § 6.4)");
        methodNames.Should().Contain(nameof(IIdentityProvider.HealthCheckAsync),
            "IIdentityProvider deve assinar HealthCheckAsync (design.md § 6.4)");
    }

    [Fact(DisplayName = "IUserDirectory deve declarar os métodos de resolução de usuário")]
    public void IUserDirectory_ShouldDeclareResolutionMethods()
    {
        var type = typeof(IUserDirectory);
        var methodNames = type.GetMethods().Select(m => m.Name).ToList();

        methodNames.Should().Contain(nameof(IUserDirectory.FindUserAsync),
            "IUserDirectory deve assinar FindUserAsync");
        methodNames.Should().Contain(nameof(IUserDirectory.IsEmailActiveAsync),
            "IUserDirectory deve assinar IsEmailActiveAsync");
    }

    [Fact(DisplayName = "ITenantDirectory deve declarar o método de resolução de slug")]
    public void ITenantDirectory_ShouldDeclareSlugResolution()
    {
        var type = typeof(ITenantDirectory);
        var methodNames = type.GetMethods().Select(m => m.Name).ToList();

        methodNames.Should().Contain(nameof(ITenantDirectory.ResolveSlugAsync),
            "ITenantDirectory deve assinar ResolveSlugAsync");
    }

    [Fact(DisplayName = "As sete portas de saída devem estar definidas em Authentication.Application")]
    public void AllSevenPorts_ShouldBeDefinedInApplicationAssembly()
    {
        // Assert
        typeof(IIdentityProvider).Assembly.FullName.Should()
            .Contain("Authentication.Application", "IIdentityProvider deve residir em Application");
        typeof(IUserDirectory).Assembly.FullName.Should()
            .Contain("Authentication.Application", "IUserDirectory deve residir em Application");
        typeof(ITenantDirectory).Assembly.FullName.Should()
            .Contain("Authentication.Application", "ITenantDirectory deve residir em Application");
        typeof(IEmailSender).Assembly.FullName.Should()
            .Contain("Authentication.Application", "IEmailSender deve residir em Application");
        typeof(IRateLimiter).Assembly.FullName.Should()
            .Contain("Authentication.Application", "IRateLimiter deve residir em Application");
        typeof(ITokenVerifier).Assembly.FullName.Should()
            .Contain("Authentication.Application", "ITokenVerifier deve residir em Application");
        typeof(ISecretProvider).Assembly.FullName.Should()
            .Contain("Authentication.Application", "ISecretProvider deve residir em Application");
    }

    // Coleta todos os tipos referenciados por uma interface (parâmetros e retornos)
    private static IEnumerable<Type> GetAllReferencedTypes(Type interfaceType)
    {
        var types = new List<Type>();
        foreach (var method in interfaceType.GetMethods())
        {
            types.Add(method.ReturnType);
            types.AddRange(method.GetParameters().Select(p => p.ParameterType));
        }
        return types.SelectMany(ExpandGenericArguments).Distinct();
    }

    private static IEnumerable<Type> ExpandGenericArguments(Type type)
    {
        yield return type;
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
                foreach (var expanded in ExpandGenericArguments(arg))
                    yield return expanded;
        }
    }
}
