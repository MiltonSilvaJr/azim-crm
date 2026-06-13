using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TenantAdministration.Application.Behaviors;
using TenantAdministration.Application.Ports;
using Xunit;

namespace TenantAdministration.Application.Tests.Behaviors;

public sealed class CorrelationBehaviorTests
{
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    [Fact(DisplayName = "Com tenant_id no contexto: behavior propaga e chama next")]
    public async Task WithTenantContext_ShouldPropagateAndCallNext()
    {
        _tenantContext.CorrelationId.Returns("corr-123");
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        _tenantContext.Slug.Returns("meu-tenant");

        var behavior = new CorrelationBehavior<SampleRequest, string>(
            _tenantContext,
            NullLogger<CorrelationBehavior<SampleRequest, string>>.Instance);

        var result = await behavior.Handle(
            new SampleRequest(),
            ct => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact(DisplayName = "Sem correlation_id no contexto: behavior gera um e chama next")]
    public async Task WithoutCorrelationId_ShouldGenerateAndCallNext()
    {
        _tenantContext.CorrelationId.Returns((string?)null);
        _tenantContext.TenantId.Returns((Guid?)null);
        _tenantContext.Slug.Returns((string?)null);

        var behavior = new CorrelationBehavior<SampleRequest, string>(
            _tenantContext,
            NullLogger<CorrelationBehavior<SampleRequest, string>>.Instance);

        var result = await behavior.Handle(
            new SampleRequest(),
            ct => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
    }

    private sealed record SampleRequest : IRequest<string>;
}
