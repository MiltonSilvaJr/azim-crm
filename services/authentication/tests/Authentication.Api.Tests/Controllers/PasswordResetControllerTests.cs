using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Authentication.Api.Tests.Helpers;
using Authentication.Application.Ports.Results;
using Authentication.Contracts.Dtos;
using Authentication.Domain.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using Xunit;

namespace Authentication.Api.Tests.Controllers;

/// <summary>
/// Testes de integração para <c>PasswordResetController</c>.
///
/// Verifica:
///   - POST /v1/auth/password-reset com e-mail existente → 202 "accepted".
///   - POST /v1/auth/password-reset com e-mail inexistente → 202 "accepted" (anti-enum).
///   - PBT-03: corpo e código são indistinguíveis para qualquer e-mail (Req 8.3, Req 10.2).
///   - Resposta nunca expõe identity_uid, firebase, exception (DD-001, Req 10.4).
///
/// Mapeia: TASK-20, design.md § 8.5, Req 8, Req 8.3, Req 10.2, PBT-03, RISK-AUTH-05.
/// </summary>
public sealed class PasswordResetControllerTests : IClassFixture<AuthenticationApiFactory>
{
    private readonly AuthenticationApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public PasswordResetControllerTests(AuthenticationApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // =========================================================================
    // POST /v1/auth/password-reset — resposta uniforme (anti-enumeração)
    // =========================================================================

    [Fact(DisplayName = "POST /password-reset com e-mail existente → 202 accepted (Req 8, PBT-03)")]
    public async Task RequestReset_WithExistingEmail_Returns202Accepted()
    {
        // Configura usuário com método password (elegível para reset)
        _factory.UserDirectory
            .FindUserByEmailAsync(
                Arg.Any<string>(),
                AuthenticationApiFactory.DefaultTenantId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserDirectoryResult?>(new UserDirectoryResult
            {
                UserId = AuthenticationApiFactory.DefaultUserId,
                Email = "user@acme.com",
                IsActive = true,
                Roles = ["viewer"],
                Memberships = MembershipSet.Empty,
                SignInProvider = "password"
            }));

        // IdP gera link de reset
        _factory.IdentityProvider
            .GeneratePasswordResetLinkAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ResetLinkResult
            {
                ResetUrl = "https://example.com/reset?token=abc",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            }));

        // EmailSender não falha
        _factory.EmailSender
            .SendPasswordResetEmailAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/password-reset");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        // Endpoint público — sem Authorization
        request.Content = JsonContent.Create(new PasswordResetRequest { Email = "user@acme.com" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            because: "password-reset sempre retorna 202 (anti-enumeração, PBT-03, Req 8.3)");

        var body = await response.Content.ReadAsStringAsync();
        var resetResp = JsonSerializer.Deserialize<PasswordResetResponse>(body, JsonOptions);
        resetResp!.Status.Should().Be("accepted");
    }

    [Fact(DisplayName = "POST /password-reset com e-mail inexistente → 202 accepted (anti-enum, PBT-03)")]
    public async Task RequestReset_WithNonExistentEmail_Returns202Accepted()
    {
        // UserDirectory retorna null — e-mail não existe
        _factory.UserDirectory
            .FindUserByEmailAsync(
                Arg.Any<string>(),
                AuthenticationApiFactory.DefaultTenantId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserDirectoryResult?>(null));

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/password-reset");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Content = JsonContent.Create(new PasswordResetRequest { Email = "naoexiste@acme.com" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            because: "e-mail inexistente deve retornar 202 idêntico (anti-enumeração, PBT-03)");

        var body = await response.Content.ReadAsStringAsync();
        var resetResp = JsonSerializer.Deserialize<PasswordResetResponse>(body, JsonOptions);
        resetResp!.Status.Should().Be("accepted",
            because: "status 'accepted' é sempre retornado (anti-enumeração, PBT-03)");
    }

    [Fact(DisplayName = "POST /password-reset com usuário Google → 202 accepted (anti-enum, Req 8.4)")]
    public async Task RequestReset_WithGoogleUser_Returns202Accepted()
    {
        // Usuário com método Google — não recebe link de senha (Req 8.4)
        _factory.UserDirectory
            .FindUserByEmailAsync(
                Arg.Any<string>(),
                AuthenticationApiFactory.DefaultTenantId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserDirectoryResult?>(new UserDirectoryResult
            {
                UserId = AuthenticationApiFactory.DefaultUserId,
                Email = "google@acme.com",
                IsActive = true,
                Roles = ["viewer"],
                Memberships = MembershipSet.Empty,
                SignInProvider = "google.com"
            }));

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/password-reset");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Content = JsonContent.Create(new PasswordResetRequest { Email = "google@acme.com" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            because: "usuário Google retorna 202 idêntico sem revelar que não usa senha (Req 8.4, PBT-03)");
    }

    [Fact(DisplayName = "POST /password-reset não expõe identity_uid, firebase, exception (DD-001, Req 10.4)")]
    public async Task RequestReset_Response_DoesNotExposeInternalDetails()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/password-reset");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Content = JsonContent.Create(new PasswordResetRequest { Email = "any@acme.com" });

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContainEquivalentOf("identity_uid",
            because: "resposta nunca deve expor identity_uid (DD-001, Req 10.4)");
        body.Should().NotContainEquivalentOf("firebase",
            because: "resposta nunca deve expor detalhe do IdP (Req 10.4)");
        body.Should().NotContainEquivalentOf("exception",
            because: "resposta nunca deve expor stack trace (Req 10.4)");
        body.Should().NotContainEquivalentOf("email",
            because: "resposta 202 não deve ecoar o e-mail recebido (anti-enumeração, PBT-03)");
    }

    // =========================================================================
    // PBT-03 — Anti-enumeração: corpo e código HTTP sempre idênticos
    // =========================================================================

    /// <summary>
    /// Invólucro para e-mail gerado pelo FsCheck (pode ou não existir no tenant).
    /// </summary>
    public sealed record ArbitraryEmail(string Value);

    /// <summary>Gerador de e-mails arbitrários (existentes e inexistentes).</summary>
    public static class Pbt03Generators
    {
        public static Arbitrary<ArbitraryEmail> ArbitraryEmailArb() =>
            Gen.Elements(
                    "existing@acme.com",
                    "naoexiste@acme.com",
                    "google@acme.com",
                    "inactive@acme.com",
                    "random-" + Guid.NewGuid().ToString("N")[..8] + "@acme.com")
                .Select(e => new ArbitraryEmail(e))
                .ToArbitrary();
    }

    /// <summary>
    /// PBT-03: para qualquer e-mail (existente, inexistente, Google, inativo),
    /// a resposta de /password-reset é sempre 202 com status "accepted".
    ///
    /// Corpo e código HTTP são indistinguíveis — anti-enumeração de contas (Req 8.3, Req 10.2).
    ///
    /// Mapeia: PBT-03, Req 8.3, Req 10.2, design.md § 8.5, RISK-AUTH-05.
    /// </summary>
    [Property(
        MaxTest = 30,
        Arbitrary = [typeof(Pbt03Generators)],
        DisplayName = "PBT-03: qualquer e-mail → sempre 202 accepted (anti-enumeração, Req 8.3)")]
    public bool AntiEnumeration_AnyEmail_AlwaysReturns202Accepted(ArbitraryEmail emailWrapper)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/password-reset");
        request.Headers.Add("X-Tenant-Slug", AuthenticationApiFactory.DefaultTenantSlug);
        request.Content = JsonContent.Create(new PasswordResetRequest { Email = emailWrapper.Value });

        var response = _client.SendAsync(request).GetAwaiter().GetResult();
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var resetResp = JsonSerializer.Deserialize<PasswordResetResponse>(body, JsonOptions);

        // Código sempre 202
        if (response.StatusCode != HttpStatusCode.Accepted) return false;

        // Corpo sempre { status: "accepted" }
        if (resetResp?.Status != "accepted") return false;

        // Nunca expõe identity_uid ou firebase
        if (body.Contains("identity_uid", StringComparison.OrdinalIgnoreCase)) return false;
        if (body.Contains("firebase", StringComparison.OrdinalIgnoreCase)) return false;
        if (body.Contains("exception", StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }
}
