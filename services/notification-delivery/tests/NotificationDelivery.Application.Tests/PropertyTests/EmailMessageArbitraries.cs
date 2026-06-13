using FsCheck;
using FsCheck.Fluent;
using NotificationDelivery.Contracts;

namespace NotificationDelivery.Application.Tests.PropertyTests;

/// <summary>
/// Geradores de <see cref="EmailMessage"/> e respostas de provedor para Property-Based Testing.
///
/// Usados em PBT-02/05 (TASK-18).
/// Cada gerador produz exemplos válidos conforme o design §13.1 e os invariantes dos tipos.
///
/// Cópia local para o projeto Application.Tests (evita dependência cruzada entre test projects).
/// A versão canônica está em NotificationDelivery.Infrastructure.Tests/PropertyTests/EmailMessageArbitraries.cs.
/// </summary>
public static class EmailMessageArbitraries
{
    // -------------------------------------------------------------------------
    // Gerador de endereço de e-mail RFC 5321 simplificado
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gera endereços de e-mail sintaticamente válidos no formato <c>local@domain.tld</c>.
    /// </summary>
    public static Arbitrary<string> ValidEmailAddress()
    {
        var localParts = new[] { "user", "alice", "bob", "carol", "tenant-a", "no-reply", "info", "test123" };
        var domains = new[] { "example.com", "azim.com.br", "test.io", "mail.org", "foo.net" };

        var gen = Gen.Elements(localParts)
            .SelectMany(local => Gen.Elements(domains)
                .Select(domain => $"{local}@{domain}"));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Gera textos não-vazios para Subject e HtmlBody.
    /// </summary>
    private static Gen<string> NonEmptyString(int maxLength = 100)
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789_-".ToCharArray();
        return Gen.Choose(1, maxLength)
            .SelectMany(len =>
                Gen.ArrayOf<char>(Gen.Elements<char>(chars), len)
                    .Select(arr => new string(arr)));
    }

    /// <summary>
    /// Gera um <see cref="EmailMessage"/> válido com todos os campos obrigatórios preenchidos.
    ///
    /// Parâmetro <paramref name="withIdempotencyKey"/> controla se o campo é preenchido.
    /// </summary>
    public static Arbitrary<EmailMessage> ValidEmailMessage(bool withIdempotencyKey = false)
    {
        var tenants = new[] { "tenant-a", "tenant-b", "tenant-c", "azim-default" };

        var gen =
            from email in ValidEmailAddress().Generator
            from subject in NonEmptyString(80)
            from body in NonEmptyString(200)
            from tenantId in Gen.Elements(tenants)
            from corrId in Gen.Fresh(() => Guid.NewGuid().ToString())
            from idemKey in withIdempotencyKey
                ? Gen.Fresh(() => (string?)Guid.NewGuid().ToString())
                : Gen.Constant((string?)null)
            select new EmailMessage(
                recipientEmail: email,
                subject: "Assunto " + subject,
                htmlBody: "<p>" + body + "</p>",
                tenantId: tenantId,
                correlationId: corrId,
                idempotencyKey: idemKey);

        return gen.ToArbitrary();
    }

    // -------------------------------------------------------------------------
    // Gerador de sequências de falha transiente + sucesso (PBT-05)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Representa uma sequência de resultados a retornar pelo sender fake (PBT-05).
    /// </summary>
    /// <param name="TransientCount">Número de falhas transientes antes do sucesso.</param>
    public sealed record TransientThenSuccessSequence(int TransientCount);

    /// <summary>
    /// Gera sequências com até 4 falhas transientes antes de um sucesso (PBT-05).
    /// Dentro do limite de retries configurável (design §6.4, RNF-3.2: até 5 tentativas).
    /// </summary>
    public static Arbitrary<TransientThenSuccessSequence> TransientThenSuccessSequences()
    {
        var gen = Gen.Choose(0, 4)
            .Select(n => new TransientThenSuccessSequence(n));
        return gen.ToArbitrary();
    }
}
