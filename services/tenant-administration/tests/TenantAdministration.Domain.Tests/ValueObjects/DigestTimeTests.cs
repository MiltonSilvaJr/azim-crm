using FluentAssertions;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor DigestTime.
/// Cobre TASK-02.
/// </summary>
public sealed class DigestTimeTests
{
    [Theory]
    [InlineData("07:00")]
    [InlineData("00:00")]
    [InlineData("23:59")]
    [InlineData("12:30")]
    [InlineData("08:00")]
    public void Create_ValidHhMmFormat_ReturnsSuccess(string time)
    {
        var result = DigestTime.Create(time);
        result.IsSuccess.Should().BeTrue(because: $"'{time}' é um horário HH:mm válido");
        result.Value.Value.Should().Be(time);
    }

    [Theory]
    [InlineData("7:00")]        // sem zero leading
    [InlineData("24:00")]       // hora inválida
    [InlineData("23:60")]       // minuto inválido
    [InlineData("07:0")]        // minuto sem zero leading
    [InlineData("07-00")]       // separador errado
    [InlineData("07:00:00")]    // com segundos
    [InlineData("")]            // vazio
    [InlineData("  ")]          // espaços
    [InlineData("abc")]         // não numérico
    public void Create_InvalidFormat_ReturnsFailure(string time)
    {
        var result = DigestTime.Create(time);
        result.IsFailure.Should().BeTrue(because: $"'{time}' não é um formato HH:mm válido");
    }

    [Fact]
    public void Create_NullInput_ReturnsFailure()
    {
        var result = DigestTime.Create(null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Default_Is0700()
    {
        DigestTime.Default.Value.Should().Be("07:00");
    }

    [Fact]
    public void DigestTime_EqualityByValue()
    {
        var a = DigestTime.Create("07:00").Value;
        var b = DigestTime.Create("07:00").Value;
        a.Should().Be(b);
    }

    [Fact]
    public void DigestTime_HasNoPublicSetter()
    {
        var valueProperty = typeof(DigestTime).GetProperty(nameof(DigestTime.Value));
        valueProperty.Should().NotBeNull();
        valueProperty!.CanWrite.Should().BeFalse();
    }
}
