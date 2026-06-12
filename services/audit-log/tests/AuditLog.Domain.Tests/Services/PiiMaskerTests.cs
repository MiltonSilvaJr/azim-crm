using AuditLog.Domain.Services;
using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AuditLog.Domain.Tests.Services;

/// <summary>
/// Testes de unidade e PBT-04 para <see cref="PiiMasker"/> e <see cref="PiiFieldPolicy"/>.
/// </summary>
public sealed class PiiMaskerTests
{
    // ------------------------------------------------------------------ Geradores FsCheck

    /// <summary>
    /// Arbitrários para geração de estados de Contact com valores PII arbitrários.
    /// </summary>
    private static class ContactArbitraries
    {
        public static Arbitrary<ContactState> ContactStateArb()
        {
            var gen =
                from name in Arb.Generate<NonEmptyString>()
                from email in Arb.Generate<NonEmptyString>()
                from phone in Arb.Generate<NonEmptyString>()
                from nonPii in Arb.Generate<NonEmptyString>()
                select new ContactState(name.Get, email.Get, phone.Get, nonPii.Get);

            return Arb.From(gen);
        }
    }

    public sealed record ContactState(string Name, string Email, string Phone, string NonPiiField);

    // ------------------------------------------------------------------ Helpers

    private static PiiMasker CreateMasker() => new(new PiiFieldPolicy());

    // ------------------------------------------------------------------ PiiMasker — mascaramento básico

    [Fact(DisplayName = "MaskedMarker é a constante '[MASKED]'")]
    public void MaskedMarker_EConstanteMasked()
    {
        PiiMasker.MaskedMarker.Should().Be("[MASKED]");
    }

    [Fact(DisplayName = "Mask Contact ForCreate: substitui name, email e phone por [MASKED]")]
    public void Mask_ContactForCreate_MascararPii()
    {
        var masker = CreateMasker();
        var delta = AuditDelta.ForCreate(new Dictionary<string, object?>
        {
            ["name"] = "Alice Ferreira",
            ["email"] = "alice@example.com",
            ["phone"] = "+5511999990000",
            ["company"] = "Azim"
        }.AsReadOnly());

        var masked = masker.Mask("Contact", delta);

        masked.After!["name"].Should().Be(PiiMasker.MaskedMarker);
        masked.After!["email"].Should().Be(PiiMasker.MaskedMarker);
        masked.After!["phone"].Should().Be(PiiMasker.MaskedMarker);
    }

    [Fact(DisplayName = "Mask Contact ForCreate: preserva campos não-PII sem alteração")]
    public void Mask_ContactForCreate_PreservaNaoPii()
    {
        var masker = CreateMasker();
        var delta = AuditDelta.ForCreate(new Dictionary<string, object?>
        {
            ["name"] = "Bob",
            ["company"] = "Azim",
            ["segment"] = "Enterprise"
        }.AsReadOnly());

        var masked = masker.Mask("Contact", delta);

        masked.After!["company"].Should().Be("Azim");
        masked.After!["segment"].Should().Be("Enterprise");
    }

    [Fact(DisplayName = "Mask Contact ForUpdate: substitui before e after dos campos PII por [MASKED]")]
    public void Mask_ContactForUpdate_MascararPiiBeforeEAfter()
    {
        var masker = CreateMasker();
        var delta = AuditDelta.ForUpdate(new Dictionary<string, AuditAttributeChange>
        {
            ["name"] = new AuditAttributeChange("Alice", "Alice Ferreira"),
            ["email"] = new AuditAttributeChange("old@x.com", "new@x.com"),
            ["stage"] = new AuditAttributeChange("Lead", "Qualified")
        }.AsReadOnly());

        var masked = masker.Mask("Contact", delta);

        masked.Changes!["name"].Before.Should().Be(PiiMasker.MaskedMarker);
        masked.Changes!["name"].After.Should().Be(PiiMasker.MaskedMarker);
        masked.Changes!["email"].Before.Should().Be(PiiMasker.MaskedMarker);
        masked.Changes!["email"].After.Should().Be(PiiMasker.MaskedMarker);
    }

    [Fact(DisplayName = "Mask Contact ForUpdate: preserva campos não-PII sem alteração")]
    public void Mask_ContactForUpdate_PreservaNaoPii()
    {
        var masker = CreateMasker();
        var delta = AuditDelta.ForUpdate(new Dictionary<string, AuditAttributeChange>
        {
            ["name"] = new AuditAttributeChange("A", "B"),
            ["stage"] = new AuditAttributeChange("Lead", "Qualified")
        }.AsReadOnly());

        var masked = masker.Mask("Contact", delta);

        masked.Changes!["stage"].Before.Should().Be("Lead");
        masked.Changes!["stage"].After.Should().Be("Qualified");
    }

    [Fact(DisplayName = "Mask Contact ForDelete: substitui campos PII em Before por [MASKED]")]
    public void Mask_ContactForDelete_MascararPiiEmBefore()
    {
        var masker = CreateMasker();
        var delta = AuditDelta.ForDelete(new Dictionary<string, object?>
        {
            ["name"] = "Carlos",
            ["email"] = "carlos@x.com",
            ["phone"] = "+55119",
            ["status"] = "active"
        }.AsReadOnly());

        var masked = masker.Mask("Contact", delta);

        masked.Before!["name"].Should().Be(PiiMasker.MaskedMarker);
        masked.Before!["email"].Should().Be(PiiMasker.MaskedMarker);
        masked.Before!["phone"].Should().Be(PiiMasker.MaskedMarker);
        masked.Before!["status"].Should().Be("active");
    }

    [Fact(DisplayName = "Mask de entidade sem política retorna delta inalterado")]
    public void Mask_EntidadeSemPolitica_RetornaDeltaImutavel()
    {
        var masker = CreateMasker();
        var delta = AuditDelta.ForCreate(new Dictionary<string, object?>
        {
            ["title"] = "Oportunidade ABC",
            ["amount_cents"] = 10000L
        }.AsReadOnly());

        // "Opportunity" não tem campos PII na política padrão
        var masked = masker.Mask("Opportunity", delta);

        masked.Should().Be(delta);
    }

    [Fact(DisplayName = "Mask preserva a chave do campo PII no delta (campo presente, valor mascarado)")]
    public void Mask_PreservaChaveDoCampoPii()
    {
        var masker = CreateMasker();
        var delta = AuditDelta.ForCreate(new Dictionary<string, object?>
        {
            ["name"] = "X",
            ["email"] = "x@x.com"
        }.AsReadOnly());

        var masked = masker.Mask("Contact", delta);

        // Chave presente com valor [MASKED] — confirma REQ-004.4
        masked.After!.Should().ContainKey("name");
        masked.After!.Should().ContainKey("email");
    }

    // ------------------------------------------------------------------ PiiFieldPolicy — extensibilidade

    [Fact(DisplayName = "PiiFieldPolicy é extensível com novos entity_types sem alterar PiiMasker")]
    public void PiiFieldPolicy_ExtensibilidadeComNovoEntityType()
    {
        // Cria política customizada adicionando "Partner" com campo "taxId"
        var customPolicy = new PiiFieldPolicy(new Dictionary<string, IReadOnlySet<string>>
        {
            ["Contact"] = new HashSet<string> { "name", "email", "phone" },
            ["Partner"] = new HashSet<string> { "taxId", "legalName" }
        });
        var masker = new PiiMasker(customPolicy);

        var delta = AuditDelta.ForCreate(new Dictionary<string, object?>
        {
            ["taxId"] = "12.345.678/0001-99",
            ["legalName"] = "Empresa Ltda",
            ["segment"] = "Finance"
        }.AsReadOnly());

        var masked = masker.Mask("Partner", delta);

        masked.After!["taxId"].Should().Be(PiiMasker.MaskedMarker);
        masked.After!["legalName"].Should().Be(PiiMasker.MaskedMarker);
        masked.After!["segment"].Should().Be("Finance");
    }

    [Fact(DisplayName = "PiiMasker não possui dependência de infraestrutura — verifica via reflexão")]
    public void PiiMasker_SemDependenciaDeInfraestrutura()
    {
        var maskerAssembly = typeof(PiiMasker).Assembly;

        var hasEfCore = maskerAssembly.GetReferencedAssemblies()
            .Any(a => a.Name?.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) == true);

        hasEfCore.Should().BeFalse(
            because: "PiiMasker é serviço de domínio puro; não pode depender de EF Core (design §4.6)");
    }

    // ------------------------------------------------------------------ PBT-04

    /// <summary>
    /// PBT-04: para qualquer entidade Contact com campos PII arbitrários,
    /// o delta resultante NÃO contém nenhum valor PII original em texto claro (REQ-004, RNF-002).
    /// Mínimo de 100 amostras geradas pelo FsCheck.
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = new[] { typeof(ContactArbitraries) })]
    public bool PBT04_Mascaramento_NaoVazaPiiEmTextoClaroNoDelta(ContactState contact)
    {
        var masker = CreateMasker();

        // Cria delta de criação com PII em texto claro
        var delta = AuditDelta.ForCreate(new Dictionary<string, object?>
        {
            ["name"] = contact.Name,
            ["email"] = contact.Email,
            ["phone"] = contact.Phone,
            ["nonPiiField"] = contact.NonPiiField
        }.AsReadOnly());

        var masked = masker.Mask("Contact", delta);

        var after = masked.After!;

        // Nenhum campo PII pode ter o valor original em texto claro
        var nameLeaked = after.TryGetValue("name", out var nameVal)
            && nameVal?.ToString() == contact.Name
            && contact.Name != PiiMasker.MaskedMarker;

        var emailLeaked = after.TryGetValue("email", out var emailVal)
            && emailVal?.ToString() == contact.Email
            && contact.Email != PiiMasker.MaskedMarker;

        var phoneLeaked = after.TryGetValue("phone", out var phoneVal)
            && phoneVal?.ToString() == contact.Phone
            && contact.Phone != PiiMasker.MaskedMarker;

        return !nameLeaked && !emailLeaked && !phoneLeaked;
    }

    [Fact(DisplayName = "Mask ForUpdate: valores PII distintos que mascaram para [MASKED] idêntico produz delta válido sem reexecutar guard")]
    public void Mask_ContactForUpdate_PiiDistintosViramMesmoMarcador_DeltaValido()
    {
        var masker = CreateMasker();

        // "João" != "Maria" — ForUpdate aceita porque o guard valida os valores originais
        // Após mascaramento: before == after == "[MASKED]", mas o guard NÃO é re-executado
        // Este é exatamente o cenário que motivava o bypass ForMaskedUpdate (design §4.3)
        var delta = AuditDelta.ForUpdate(new Dictionary<string, AuditAttributeChange>
        {
            ["name"] = new AuditAttributeChange("João", "Maria")
        }.AsReadOnly());

        var masked = masker.Mask("Contact", delta);

        masked.Kind.Should().Be(AuditDeltaKind.Update);
        masked.Changes!["name"].Before.Should().Be(PiiMasker.MaskedMarker);
        masked.Changes!["name"].After.Should().Be(PiiMasker.MaskedMarker);
    }

    /// <summary>
    /// PBT-04 (variante update): campos PII em before e after não aparecem em texto claro.
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = new[] { typeof(ContactArbitraries) })]
    public bool PBT04_Mascaramento_NaoVazaPiiEmUpdate(ContactState before, ContactState after)
    {
        // Garante que os valores são diferentes para satisfazer a invariante do ForUpdate
        if (before.Name == after.Name && before.Email == after.Email && before.Phone == after.Phone)
            return true; // amostra não-representativa; descarta (vacuously true)

        var masker = CreateMasker();
        var changes = new Dictionary<string, AuditAttributeChange>();

        if (before.Name != after.Name)
            changes["name"] = new AuditAttributeChange(before.Name, after.Name);
        if (before.Email != after.Email)
            changes["email"] = new AuditAttributeChange(before.Email, after.Email);
        if (before.Phone != after.Phone)
            changes["phone"] = new AuditAttributeChange(before.Phone, after.Phone);

        if (changes.Count == 0)
            return true; // nada mudou nesta amostra

        var delta = AuditDelta.ForUpdate(changes.AsReadOnly());
        var masked = masker.Mask("Contact", delta);

        foreach (var (field, change) in masked.Changes!)
        {
            if (field is "name" or "email" or "phone")
            {
                if (change.Before?.ToString() != PiiMasker.MaskedMarker) return false;
                if (change.After?.ToString() != PiiMasker.MaskedMarker) return false;
            }
        }

        return true;
    }
}
