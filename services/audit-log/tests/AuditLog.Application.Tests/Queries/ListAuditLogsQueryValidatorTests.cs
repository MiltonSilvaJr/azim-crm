using AuditLog.Application.Queries;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace AuditLog.Application.Tests.Queries;

/// <summary>
/// Testes unitários do <see cref="ListAuditLogsQueryValidator"/>.
/// </summary>
public sealed class ListAuditLogsQueryValidatorTests
{
    private readonly ListAuditLogsQueryValidator _sut = new();

    // -----------------------------------------------------------------------
    // PageSize máximo (AUD-ERR-001)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve aceitar pageSize = 200 (máximo)")]
    public void Should_Accept_Max_PageSize()
    {
        var query = new ListAuditLogsQuery { PageSize = 200 };
        var result = _sut.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact(DisplayName = "Deve rejeitar pageSize > 200")]
    public void Should_Reject_PageSize_Over_200()
    {
        var query = new ListAuditLogsQuery { PageSize = 201 };
        var result = _sut.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact(DisplayName = "Deve rejeitar pageSize = 0")]
    public void Should_Reject_Zero_PageSize()
    {
        var query = new ListAuditLogsQuery { PageSize = 0 };
        var result = _sut.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact(DisplayName = "Deve aceitar pageSize = 50 (default)")]
    public void Should_Accept_Default_PageSize()
    {
        var query = new ListAuditLogsQuery();
        var result = _sut.TestValidate(query);
        result.IsValid.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Intervalo de datas (AUD-ERR-006)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve rejeitar from > to")]
    public void Should_Reject_From_Greater_Than_To()
    {
        var query = new ListAuditLogsQuery
        {
            From = DateTimeOffset.UtcNow.AddDays(1),
            To = DateTimeOffset.UtcNow
        };
        var result = _sut.TestValidate(query);
        result.Errors.Should().NotBeEmpty(because: "from > to é inválido (AUD-ERR-006)");
    }

    [Fact(DisplayName = "Deve aceitar from = to")]
    public void Should_Accept_From_Equal_To()
    {
        var now = DateTimeOffset.UtcNow;
        var query = new ListAuditLogsQuery { From = now, To = now };
        var result = _sut.TestValidate(query);
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "Deve aceitar from < to")]
    public void Should_Accept_From_Less_Than_To()
    {
        var query = new ListAuditLogsQuery
        {
            From = DateTimeOffset.UtcNow.AddDays(-1),
            To = DateTimeOffset.UtcNow
        };
        var result = _sut.TestValidate(query);
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "Deve aceitar quando apenas From é fornecido")]
    public void Should_Accept_Only_From()
    {
        var query = new ListAuditLogsQuery { From = DateTimeOffset.UtcNow.AddDays(-7) };
        var result = _sut.TestValidate(query);
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Deve aceitar quando apenas To é fornecido")]
    public void Should_Accept_Only_To()
    {
        var query = new ListAuditLogsQuery { To = DateTimeOffset.UtcNow };
        var result = _sut.TestValidate(query);
        result.IsValid.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Página
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve rejeitar page = 0")]
    public void Should_Reject_Zero_Page()
    {
        var query = new ListAuditLogsQuery { Page = 0 };
        var result = _sut.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact(DisplayName = "Deve aceitar page = 1")]
    public void Should_Accept_Page_One()
    {
        var query = new ListAuditLogsQuery { Page = 1 };
        var result = _sut.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.Page);
    }
}
