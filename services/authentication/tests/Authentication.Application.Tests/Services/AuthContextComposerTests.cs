using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Authentication.Application.Services;
using Authentication.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;

namespace Authentication.Application.Tests.Services;

/// <summary>
/// Testes unitários para <see cref="AuthContextComposer"/>.
///
/// Cobre: cache hit/miss, usuário inativo → 403, identity_uid não vaza para AuthContext.
/// Mapeia: TASK-06, design.md § 5.3, Req 5, Req 5.4.
/// </summary>
public sealed class AuthContextComposerTests : IDisposable
{
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IMemoryCache _cache;
    private readonly AuthContextComposer _sut;

    private static readonly MembershipSet EmptyMemberships =
        MembershipSet.Create([], DateTimeOffset.UtcNow);

    public AuthContextComposerTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new AuthContextComposer(_userDirectory, _cache);
    }

    public void Dispose() => _cache.Dispose();

    [Fact(DisplayName = "ProviderUserRef válido com usuário ativo produz AuthContext correto")]
    public async Task ActiveUser_ProducesAuthContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string providerRef = "provider-ref-1";
        const string email = "user@tenant.com";

        _userDirectory
            .FindUserAsync(providerRef, tenantId, Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryResult
            {
                UserId = userId,
                Email = email,
                Roles = ["admin"],
                Memberships = EmptyMemberships,
                IsActive = true,
            });

        var tokenResult = new VerifyTokenResult
        {
            ProviderUserRef = providerRef,
            FirebaseTenant = "ft-1",
            Email = email,
            SignInProvider = "password",
        };

        // Act
        var ctx = await _sut.ComposeAsync(tokenResult, tenantId);

        // Assert
        ctx.UserId.Should().Be(userId);
        ctx.TenantId.Should().Be(tenantId);
        ctx.Email.Should().Be(email);
        ctx.Roles.Should().Contain("admin");
    }

    [Fact(DisplayName = "AuthContext não contém ProviderUserRef nem identity_uid")]
    public async Task AuthContext_DoesNotContain_ProviderUserRef()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string providerRef = "secret-uid-abc";

        _userDirectory
            .FindUserAsync(providerRef, tenantId, Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryResult
            {
                UserId = userId,
                Email = "u@t.com",
                Roles = [],
                Memberships = EmptyMemberships,
                IsActive = true,
            });

        var tokenResult = new VerifyTokenResult
        {
            ProviderUserRef = providerRef,
            FirebaseTenant = "ft-1",
            Email = "u@t.com",
            SignInProvider = "password",
        };

        // Act
        var ctx = await _sut.ComposeAsync(tokenResult, tenantId);

        // Assert — verificar via reflexão que AuthContext não expõe o ProviderUserRef
        var props = typeof(AuthContext).GetProperties();
        foreach (var prop in props)
        {
            if (prop.PropertyType == typeof(string))
            {
                var value = (string?)prop.GetValue(ctx);
                value.Should().NotBe(providerRef,
                    $"AuthContext.{prop.Name} não deve conter o ProviderUserRef (DD-001)");
            }
        }
    }

    [Fact(DisplayName = "Usuário sem user_id ativo lança IdentityProviderException AUTH-ERR-005")]
    public async Task InactiveUser_ThrowsIdentityProviderException_WithCode005()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        const string providerRef = "ref-inactive";

        _userDirectory
            .FindUserAsync(providerRef, tenantId, Arg.Any<CancellationToken>())
            .Returns((UserDirectoryResult?)null);

        var tokenResult = new VerifyTokenResult
        {
            ProviderUserRef = providerRef,
            FirebaseTenant = "ft-1",
            Email = "u@t.com",
            SignInProvider = "password",
        };

        // Act
        var act = () => _sut.ComposeAsync(tokenResult, tenantId);

        // Assert
        await act.Should().ThrowAsync<IdentityProviderException>()
            .Where(e => e.ErrorCode == "AUTH-ERR-005");
    }

    [Fact(DisplayName = "Segunda chamada com mesmo providerRef e tenant usa cache — IUserDirectory não é chamado novamente")]
    public async Task SecondCall_UsesCacheHit_UserDirectoryNotCalledAgain()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string providerRef = "ref-cached";

        _userDirectory
            .FindUserAsync(providerRef, tenantId, Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryResult
            {
                UserId = userId,
                Email = "u@t.com",
                Roles = [],
                Memberships = EmptyMemberships,
                IsActive = true,
            });

        var tokenResult = new VerifyTokenResult
        {
            ProviderUserRef = providerRef,
            FirebaseTenant = "ft-1",
            Email = "u@t.com",
            SignInProvider = "password",
        };

        // Act — duas chamadas consecutivas
        await _sut.ComposeAsync(tokenResult, tenantId);
        await _sut.ComposeAsync(tokenResult, tenantId);

        // Assert — IUserDirectory chamado apenas uma vez (cache hit na segunda)
        await _userDirectory
            .Received(1)
            .FindUserAsync(providerRef, tenantId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cache isola tenants: providerRef do tenant A não serve tenant B")]
    public async Task CacheKey_IsolatesTenants()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userIdA = Guid.NewGuid();
        var userIdB = Guid.NewGuid();
        const string providerRef = "ref-cross";

        _userDirectory
            .FindUserAsync(providerRef, tenantA, Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryResult
            {
                UserId = userIdA,
                Email = "a@ta.com",
                Roles = [],
                Memberships = EmptyMemberships,
                IsActive = true,
            });

        _userDirectory
            .FindUserAsync(providerRef, tenantB, Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryResult
            {
                UserId = userIdB,
                Email = "a@tb.com",
                Roles = [],
                Memberships = EmptyMemberships,
                IsActive = true,
            });

        var tokenResult = new VerifyTokenResult
        {
            ProviderUserRef = providerRef,
            FirebaseTenant = "ft-shared",
            Email = "a@t.com",
            SignInProvider = "password",
        };

        // Act
        var ctxA = await _sut.ComposeAsync(tokenResult, tenantA);
        var ctxB = await _sut.ComposeAsync(tokenResult, tenantB);

        // Assert — contextos isolados por tenant
        ctxA.UserId.Should().Be(userIdA);
        ctxB.UserId.Should().Be(userIdB);
        ctxA.TenantId.Should().Be(tenantA);
        ctxB.TenantId.Should().Be(tenantB);
    }
}
