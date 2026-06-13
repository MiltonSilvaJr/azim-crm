using Authentication.Application.Ports;
using Authentication.Application.Ports.Results;
using Authentication.Application.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;

namespace Authentication.Application.Tests.PropertyTests;

/// <summary>
/// PBT-03 — Resposta indistinguível (anti-enumeração, unitário).
///
/// Propriedade: o tipo de resposta (sucesso/falha) do <c>PasswordResetService</c>
/// é idêntico para e-mail existente e inexistente.
/// Ambos devem retornar sem exceção (accepted) — nunca revelar existência de conta.
///
/// FsCheck gera ≥ 100 pares (e-mail existente, e-mail inexistente).
/// Mapeia: PBT-03, TASK-09, design.md § 5.1, Req 8.3, Req 10.2.
/// </summary>
public sealed class Pbt03Tests
{
    /// <summary>Par de e-mails: um ativo e um inexistente.</summary>
    public sealed record EmailPair(string ActiveEmail, string MissingEmail, Guid TenantId);

    /// <summary>Provedor de geradores para PBT-03.</summary>
    public static class Generators
    {
        /// <summary>Gera pares de e-mails distintos (ativo e inexistente) com tenant aleatório.</summary>
        public static Arbitrary<EmailPair> EmailPairArbitrary()
        {
            var gen = ArbMap.Default
                .GeneratorFor<int>()
                .Select(Math.Abs)
                .Where(n => n > 0)
                .SelectMany(i =>
                    ArbMap.Default
                        .GeneratorFor<int>()
                        .Select(Math.Abs)
                        .Where(j => j > 0)
                        .Select(j => new EmailPair(
                            $"active-{i:D6}@tenant.com",
                            $"missing-{j:D6}@tenant.com",
                            Guid.NewGuid())));

            return gen.ToArbitrary();
        }
    }

    /// <summary>
    /// PBT-03: tipo de resposta do PasswordResetService é idêntico para
    /// e-mail existente (ativo, método password) e e-mail inexistente.
    /// Ambos devem retornar sem exceção (202 accepted).
    ///
    /// Mapeia: PBT-03, Req 8.3, Req 10.2.
    /// </summary>
    [Property(
        MaxTest = 100,
        Arbitrary = [typeof(Generators)],
        DisplayName = "PBT-03: resposta idêntica para e-mail existente e inexistente")]
    public bool ResponseType_IdenticalForExistingAndMissingEmail(EmailPair emailPair)
    {
        // Arrange
        var identityProvider = Substitute.For<IIdentityProvider>();
        var userDirectory = Substitute.For<IUserDirectory>();
        var emailSender = Substitute.For<IEmailSender>();
        var auditEmitter = Substitute.For<IAuditEventEmitter>();
        var options = Options.Create(new PasswordResetOptions { ConstantDelayMs = 0 });

        var sut = new PasswordResetService(
            identityProvider, userDirectory, emailSender, auditEmitter, options);

        // Configura: e-mail ativo → retorna usuário com método password
        userDirectory
            .FindUserByEmailAsync(emailPair.ActiveEmail, emailPair.TenantId, Arg.Any<CancellationToken>())
            .Returns(new UserDirectoryResult
            {
                UserId = Guid.NewGuid(),
                Email = emailPair.ActiveEmail,
                Roles = [],
                Memberships = MembershipSet.Create([], DateTimeOffset.UtcNow),
                IsActive = true,
                SignInProvider = "password",
            });

        identityProvider
            .GeneratePasswordResetLinkAsync(
                emailPair.ActiveEmail, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ResetLinkResult
            {
                ResetUrl = "https://idp/reset?token=gen",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });

        // Configura: e-mail ausente → retorna null
        userDirectory
            .FindUserByEmailAsync(emailPair.MissingEmail, emailPair.TenantId, Arg.Any<CancellationToken>())
            .Returns((UserDirectoryResult?)null);

        // Act: requisição com e-mail ativo
        var activeSucceeded = false;
        try
        {
            sut.RequestAsync(new RequestPasswordResetCommand(
                emailPair.ActiveEmail, "ft-tenant", emailPair.TenantId))
                .GetAwaiter().GetResult();
            activeSucceeded = true;
        }
        catch { activeSucceeded = false; }

        // Act: requisição com e-mail inexistente
        var missingSucceeded = false;
        try
        {
            sut.RequestAsync(new RequestPasswordResetCommand(
                emailPair.MissingEmail, "ft-tenant", emailPair.TenantId))
                .GetAwaiter().GetResult();
            missingSucceeded = true;
        }
        catch { missingSucceeded = false; }

        // Assert: tipo de resposta idêntico (ambos sucesso ou ambos falha)
        // PBT-03: ambos devem ser aceitos (true == true)
        return activeSucceeded == missingSucceeded && activeSucceeded;
    }
}
