using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários e PBTs para o objeto de valor Slug.
/// Cobre TASK-02: ST-01 Red.
/// </summary>
public sealed class SlugTests
{
    // ──────────────────────────────────────────────
    // Casos válidos
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("abc")]
    [InlineData("my-tenant")]
    [InlineData("vellus")]
    [InlineData("a-b-c")]
    [InlineData("abc-def-ghi")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmn")] // 40 chars
    public void Create_ValidSlug_ReturnsSuccess(string input)
    {
        var result = Slug.Create(input);
        result.IsSuccess.Should().BeTrue(because: $"'{input}' é um slug válido");
        result.Value.Value.Should().Be(input);
    }

    [Fact]
    public void Create_TrimsWhitespaceAndLowercases_ReturnsSuccess()
    {
        var result = Slug.Create("  MyTenant  ");
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("mytenant");
    }

    // ──────────────────────────────────────────────
    // Caracteres inválidos
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("América")]       // não-ASCII
    [InlineData("my tenant")]     // espaço interno
    [InlineData("my_tenant")]     // underscore
    [InlineData("my.tenant")]     // ponto
    [InlineData("tenant!")]       // caractere especial
    public void Create_InvalidCharacters_ReturnsFailure(string input)
    {
        var result = Slug.Create(input);
        result.IsFailure.Should().BeTrue(because: $"'{input}' contém caracteres inválidos");
        result.ErrorCode.Should().Be("TA-ERR-005");
    }

    // ──────────────────────────────────────────────
    // Bordas com hífen
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("-abc")]          // inicia com hífen
    [InlineData("abc-")]          // termina com hífen
    [InlineData("-abc-")]         // ambas as bordas
    public void Create_HyphenAtBorder_ReturnsFailure(string input)
    {
        var result = Slug.Create(input);
        result.IsFailure.Should().BeTrue(because: $"'{input}' tem hífen nas bordas");
        result.ErrorCode.Should().Be("TA-ERR-005");
    }

    // ──────────────────────────────────────────────
    // Hífens consecutivos
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("my--tenant")]
    [InlineData("a--b")]
    [InlineData("abc---def")]
    public void Create_ConsecutiveHyphens_ReturnsFailure(string input)
    {
        var result = Slug.Create(input);
        result.IsFailure.Should().BeTrue(because: $"'{input}' tem hífens consecutivos");
        result.ErrorCode.Should().Be("TA-ERR-005");
    }

    // ──────────────────────────────────────────────
    // Comprimento
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("ab")]            // 2 chars — muito curto
    [InlineData("a")]             // 1 char
    [InlineData("")]              // vazio
    public void Create_TooShort_ReturnsFailure(string input)
    {
        var result = Slug.Create(input);
        result.IsFailure.Should().BeTrue(because: $"'{input}' é muito curto");
        result.ErrorCode.Should().Be("TA-ERR-005");
    }

    [Fact]
    public void Create_TooLong_ReturnsFailure()
    {
        // 41 caracteres
        var input = new string('a', 41);
        var result = Slug.Create(input);
        result.IsFailure.Should().BeTrue(because: "slug com 41 chars excede o limite de 40");
        result.ErrorCode.Should().Be("TA-ERR-005");
    }

    [Fact]
    public void Create_ExactlyMinLength_ReturnsSuccess()
    {
        var result = Slug.Create("abc");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        var input = new string('a', 40);
        var result = Slug.Create(input);
        result.IsSuccess.Should().BeTrue();
    }

    // ──────────────────────────────────────────────
    // Imutabilidade
    // ──────────────────────────────────────────────

    [Fact]
    public void Slug_HasNoPublicSetter()
    {
        var type = typeof(Slug);
        var valueProperty = type.GetProperty(nameof(Slug.Value));
        valueProperty.Should().NotBeNull();
        valueProperty!.CanWrite.Should().BeFalse(because: "Slug deve ser imutável");
    }

    // ──────────────────────────────────────────────
    // Igualdade por valor
    // ──────────────────────────────────────────────

    [Fact]
    public void Slug_EqualityByValue()
    {
        var a = Slug.Create("my-tenant").Value;
        var b = Slug.Create("my-tenant").Value;
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Slug_DifferentValues_AreNotEqual()
    {
        var a = Slug.Create("tenant-a").Value;
        var b = Slug.Create("tenant-b").Value;
        a.Should().NotBe(b);
    }

    // ──────────────────────────────────────────────
    // PBT-01 — imutabilidade: sequências de modificação não alteram o slug
    // ──────────────────────────────────────────────

    /// <summary>
    /// PBT-01: Uma vez criado, o valor do slug nunca muda, independentemente de
    /// quantas vezes seja lido ou de quantos slugs distintos sejam criados em paralelo.
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-01: Slug imutável após criação")]
    public Property Pbt01_SlugIsImmutableAfterCreation()
    {
        // Geramos slugs válidos simples: apenas letras minúsculas, 3..40 chars
        var gen = Gen.Choose(3, 40)
            .SelectMany(len => Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h',
                'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                'u', 'v', 'w', 'x', 'y', 'z')
                .ArrayOf(len)
                .Select(chars => new string(chars)));

        return Prop.ForAll(Arb.From(gen), rawSlug =>
        {
            var r1 = Slug.Create(rawSlug);
            if (r1.IsFailure) return true;

            var slug = r1.Value;
            var initialValue = slug.Value;

            // Ler múltiplas vezes não altera o valor
            for (var i = 0; i < 10; i++)
            {
                slug.Value.Should().Be(initialValue);
            }

            // Criar outro slug com mesmo input produz o mesmo valor
            var r2 = Slug.Create(rawSlug);
            r2.Value.Value.Should().Be(initialValue);

            return true;
        });
    }

    // ──────────────────────────────────────────────
    // PBT-02 — formato: aceitos ⟺ normalizável em [a-z-]
    // ──────────────────────────────────────────────

    /// <summary>
    /// PBT-02: Um slug é aceito se e somente se, após trim e lowercase,
    /// contém apenas [a-z-], tem comprimento 3..40, não inicia/termina com hífen
    /// e não possui hífens consecutivos.
    /// </summary>
    [Property(MaxTest = 500, DisplayName = "PBT-02: Aceitos ↔ normalizável em [a-z-]")]
    public Property Pbt02_AcceptedIfAndOnlyIfNormalizable()
    {
        // Geramos strings compostas por letras, hífens e alguns caracteres inválidos
        var validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_. 0123456789";
        var gen = Gen.Choose(0, 50)
            .SelectMany(len => Gen.Elements(validChars.ToCharArray())
                .ArrayOf(len)
                .Select(chars => new string(chars)));

        return Prop.ForAll(Arb.From(gen), raw =>
        {
            var normalized = raw.Trim().ToLowerInvariant();
            var isNormallyValid = IsValidSlug(normalized);

            var result = Slug.Create(raw);

            if (isNormallyValid)
            {
                result.IsSuccess.Should().BeTrue(
                    because: $"'{raw}' normaliza para '{normalized}' que é válido");
            }
            else
            {
                result.IsFailure.Should().BeTrue(
                    because: $"'{raw}' normaliza para '{normalized}' que é inválido");
            }

            return true;
        });
    }

    /// <summary>
    /// Implementação local da regra de validação para uso no PBT-02.
    /// Deve espelhar exatamente as regras do objeto de valor.
    /// </summary>
    private static bool IsValidSlug(string normalized)
    {
        if (string.IsNullOrEmpty(normalized)) return false;
        if (normalized.Length < 3 || normalized.Length > 40) return false;
        if (!normalized.All(c => c is (>= 'a' and <= 'z') or '-')) return false;
        if (normalized.StartsWith('-') || normalized.EndsWith('-')) return false;
        if (normalized.Contains("--")) return false;
        return true;
    }
}
