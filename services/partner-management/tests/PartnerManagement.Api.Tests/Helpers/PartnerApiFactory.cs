using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using PartnerManagement.Application.Ports;
using MediatR;

namespace PartnerManagement.Api.Tests.Helpers;

/// <summary>
/// Factory WebApplicationFactory para testes de integração da API partner-management.
/// Substitui MediatR real por mock controlado nos testes, permitindo RBAC e cenários de erro
/// isolados sem banco de dados ou infrastructure real.
/// Mapeia: TASK-23, TASK-24, TASK-25, design §13 (Api.Tests).
/// </summary>
public sealed class PartnerApiFactory : WebApplicationFactory<global::Program>
{
    // Mocks expostos para configuração nos testes
    public IMediator Mediator { get; } = Substitute.For<IMediator>();
    public ITenantContext TenantContext { get; } = Substitute.For<ITenantContext>();
    public IPermissionContext PermissionContext { get; } = Substitute.For<IPermissionContext>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remover registros de MediatR e substituir por mock
            // Isso garante que os behaviors NÃO sejam executados nos testes —
            // testamos o controller diretamente via mediator mock.
            services.RemoveAll<IMediator>();
            services.RemoveAll<ISender>();
            services.RemoveAll<IPublisher>();
            services.AddSingleton(Mediator);
            services.AddSingleton<ISender>(Mediator);
            services.AddSingleton<IPublisher>(Mediator);

            // Substituir autenticação por handler de teste
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Substituir contextos de tenant e permissão por mocks
            services.RemoveAll<ITenantContext>();
            services.RemoveAll<IPermissionContext>();
            services.AddScoped<ITenantContext>(_ => TenantContext);
            services.AddScoped<IPermissionContext>(_ => PermissionContext);

            // Tenant padrão para testes
            TenantContext.CurrentTenantId.Returns(TestData.DefaultTenantId);
            TenantContext.IsResolved.Returns(true);

            // PermissionContext padrão — pode ser sobrescrito por teste
            PermissionContext.HasPermission(Arg.Any<string>()).Returns(true);
            PermissionContext.Permissions.Returns([
                "partners:read",
                "partners:write",
                "partners:manage",
                "partners:commissions:read"
            ]);
        });

        builder.UseEnvironment("Testing");
    }
}

/// <summary>
/// Dados de teste compartilhados.
/// </summary>
public static class TestData
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultPartnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid DefaultActorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // Claims por papel (formato: "claim_type=value;claim_type=value")
    public const string ViewerClaims =
        "permissions=partners:read;tenant_id=11111111-1111-1111-1111-111111111111;sub=33333333-3333-3333-3333-333333333333";
    public const string WriterClaims =
        "permissions=partners:read;permissions=partners:write;tenant_id=11111111-1111-1111-1111-111111111111;sub=33333333-3333-3333-3333-333333333333";
    public const string AdminClaims =
        "permissions=partners:read;permissions=partners:write;permissions=partners:manage;permissions=partners:commissions:read;tenant_id=11111111-1111-1111-1111-111111111111;sub=33333333-3333-3333-3333-333333333333";
    public const string CommissionClaims =
        "permissions=partners:read;permissions=partners:commissions:read;tenant_id=11111111-1111-1111-1111-111111111111;sub=33333333-3333-3333-3333-333333333333";
}
