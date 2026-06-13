using FluentAssertions;
using FluentValidation.TestHelper;
using NSubstitute;
using TenantAdministration.Application.Commands;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Application.Tests.Commands;

public sealed class UpdateDigestConfigHandlerTests
{
    private readonly ITenantRepository _repository = Substitute.For<ITenantRepository>();
    private readonly IEventOutbox _outbox = Substitute.For<IEventOutbox>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private Tenant CreateTenant()
    {
        var slug = Slug.Create("meu-tenant").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        return Tenant.Provision(slug, "Meu Tenant", tz, DigestTime.Default, "admin@a.com", Now);
    }

    private UpdateDigestConfigHandler CreateHandler() =>
        new(_repository, _outbox, _clock);

    [Fact(DisplayName = "Fuso IANA válido: atualiza e enfileira DigestConfigChanged")]
    public async Task ValidTimezone_ShouldUpdateAndEnqueueEvent()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateTenant();
        tenant.ClearDomainEvents();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var cmd = new UpdateDigestConfigCommand(tenant.Id, "America/New_York", "08:00");
        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        result.Timezone.Should().Be("America/New_York");
        result.DigestTime.Should().Be("08:00");
        await _repository.Received(1).UpdateAsync(tenant, Arg.Any<CancellationToken>());
        await _outbox.Received(1).AppendRangeAsync(Arg.Any<IEnumerable<TenantAdministration.Domain.Events.IDomainEvent>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "TA-ERR-006: fuso IANA inválido → DomainValidationException")]
    public async Task InvalidTimezone_ShouldThrow_TA_ERR_006()
    {
        var handler = CreateHandler();
        var cmd = new UpdateDigestConfigCommand(Guid.NewGuid(), "Invalid/Zone", "07:00");

        var act = () => handler.Handle(cmd, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-006");
    }

    [Fact(DisplayName = "TA-ERR-011: slug no corpo é rejeitado pelo validator")]
    public void SlugInBody_ShouldFail_TA_ERR_011()
    {
        var validator = new UpdateDigestConfigCommandValidator();
        var cmd = new UpdateDigestConfigCommand(Guid.NewGuid(), "America/Sao_Paulo", "07:00", Slug: "meu-tenant");

        var result = validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorCode("TA-ERR-011");
    }
}
