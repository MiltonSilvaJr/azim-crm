using FsCheck;
using FsCheck.Fluent;
using NotificationDelivery.Contracts;

namespace NotificationDelivery.Infrastructure.Tests.PropertyTests;

/// <summary>
/// Geradores de <see cref="EmailMessage"/> e respostas de provedor para Property-Based Testing.
///
/// Usados em PBT-01 (TASK-17), PBT-02/05 (TASK-18), PBT-03 (TASK-19).
/// Cada gerador produz exemplos válidos conforme o design §13.1 e os invariantes dos tipos.
///
/// Geradores documentados conforme TASK-17/ST-03.
/// </summary>
public static class EmailMessageArbitraries
{
    // -------------------------------------------------------------------------
    // Gerador de endereço de e-mail RFC 5321 simplificado
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gera endereços de e-mail sintaticamente válidos no formato <c>local@domain.tld</c>.
    /// Usado em PBT-01, PBT-03. Garante que <see cref="EmailMessage"/> pode ser construído
    /// sem falha de validação.
    /// </summary>
    public static Arbitrary<string> ValidEmailAddress()
    {
        // Locais e domínios restritos a chars ASCII seguros para garantir passar no regex do EmailMessage
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
    /// Gerador para PBT-01 (reversibilidade de adapter) e PBT-04 (totalidade da ACL).
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
    // Gerador de respostas de provedor (conjunto discreto — design §13.1, PBT-04)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Caso de resposta de provedor para testar totalidade do mapeamento ACL (PBT-04).
    /// </summary>
    public sealed record ProviderResponseCase(
        System.Net.HttpStatusCode StatusCode,
        string? MessageId,
        bool IsBounce,
        bool IsSuppressed,
        string Description);

    /// <summary>
    /// Gera casos de resposta do provedor cobrindo todos os estados do design §13.1.
    ///
    /// Conjunto fixo de 11 casos discretos conforme design §13.1 e §12 (PBT-04).
    /// </summary>
    public static Arbitrary<ProviderResponseCase> ProviderResponseCases()
    {
        var cases = new[]
        {
            new ProviderResponseCase(System.Net.HttpStatusCode.OK, "msg-pbt-001", false, false, "200 sucesso"),
            new ProviderResponseCase(System.Net.HttpStatusCode.Accepted, "msg-pbt-202", false, false, "202 sucesso"),
            new ProviderResponseCase(System.Net.HttpStatusCode.BadRequest, null, false, false, "400 payload invalido"),
            new ProviderResponseCase(System.Net.HttpStatusCode.Unauthorized, null, false, false, "401 credencial"),
            new ProviderResponseCase(System.Net.HttpStatusCode.UnprocessableEntity, null, false, false, "422 payload invalido"),
            new ProviderResponseCase((System.Net.HttpStatusCode)429, null, false, false, "429 rate limit"),
            new ProviderResponseCase(System.Net.HttpStatusCode.InternalServerError, null, false, false, "500 server error"),
            new ProviderResponseCase(System.Net.HttpStatusCode.ServiceUnavailable, null, false, false, "503 indisponivel"),
            new ProviderResponseCase(System.Net.HttpStatusCode.OK, null, true, false, "hard_bounce"),
            new ProviderResponseCase(System.Net.HttpStatusCode.OK, null, false, true, "suppressed"),
            new ProviderResponseCase(System.Net.HttpStatusCode.NoContent, null, false, false, "204 inesperado"),
        };

        return Gen.Elements(cases).ToArbitrary();
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
        // maxFailures=4 ≤ MaxRetryAttempts=5 (garante que o sucesso alcança o sender)
        var gen = Gen.Choose(0, 4)
            .Select(n => new TransientThenSuccessSequence(n));
        return gen.ToArbitrary();
    }
}
