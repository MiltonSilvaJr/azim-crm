using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using DataMigration.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DataMigration.Api.Tests.Infrastructure;

/// <summary>
/// Factory de integração da API para testes de contrato.
/// Substitui infra real (EF, banco) por fakes em memória.
/// Registra stubs para todas as portas da Application que normalmente
/// são implementadas pela Infrastructure.
///
/// Rastreia: design §8, §10, §12, TASK-21..TASK-23.
/// </summary>
public sealed class MigrationApiFactory : WebApplicationFactory<DataMigration.Api.AssemblyReference>
{
    /// <summary>Tenant padrão usado nos testes.</summary>
    public static readonly Guid DefaultTenantId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>Usuário PlatOp padrão.</summary>
    public static readonly Guid DefaultPlatOpId = new("22222222-2222-2222-2222-222222222222");

    /// <summary>Usuário TenantAdmin padrão.</summary>
    public static readonly Guid DefaultAdminId = new("33333333-3333-3333-3333-333333333333");

    /// <summary>
    /// Repositório em memória compartilhado — permite consultar/injetar jobs nos testes.
    /// </summary>
    public InMemoryMigrationJobRepository JobRepository { get; } = new();

    /// <summary>
    /// Flags de feature que podem ser configuradas por teste.
    /// </summary>
    public TestFeatureFlags FeatureFlags { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // =========================================================
            // Autenticação de teste (substitui JWT)
            // =========================================================
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // =========================================================
            // Contexto de tenant fake (lê headers X-Test-*)
            // =========================================================
            services.RemoveAll<ICurrentTenantContext>();
            services.AddScoped<ICurrentTenantContext>(sp =>
            {
                var httpContext = sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
                var req = httpContext.HttpContext?.Request;

                var tenantId = DefaultTenantId;
                var userId = DefaultPlatOpId;
                var role = "PlatformOperator";

                if (req is not null)
                {
                    if (req.Headers.TryGetValue("X-Test-TenantId", out var t)
                        && Guid.TryParse(t.FirstOrDefault(), out var parsedTenant))
                    {
                        tenantId = parsedTenant;
                    }

                    if (req.Headers.TryGetValue("X-Test-UserId", out var u)
                        && Guid.TryParse(u.FirstOrDefault(), out var parsedUser))
                    {
                        userId = parsedUser;
                    }

                    if (req.Headers.TryGetValue("X-Test-Role", out var r))
                    {
                        role = r.FirstOrDefault() ?? role;
                    }
                }

                return new TestTenantContext(tenantId, userId, role);
            });

            // =========================================================
            // Repositório em memória (substitui MigrationJobRepository)
            // =========================================================
            services.RemoveAll<IMigrationJobRepository>();
            services.AddSingleton<IMigrationJobRepository>(JobRepository);

            // =========================================================
            // Feature flags de teste
            // =========================================================
            services.RemoveAll<IFeatureFlags>();
            services.AddSingleton<IFeatureFlags>(FeatureFlags);

            // =========================================================
            // IClock — stub determinístico
            // =========================================================
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new FixedClock(DateTimeOffset.UtcNow));

            // =========================================================
            // IUnitOfWork — stub no-op (sem banco em testes de API)
            // =========================================================
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(_ =>
            {
                var uow = Substitute.For<IUnitOfWork>();
                uow.IsRollbackOnly.Returns(false);
                uow.BeginAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
                uow.BeginRollbackOnlyAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
                uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
                uow.RollbackAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
                return uow;
            });

            // =========================================================
            // ISpreadsheetParser — stub que retorna estrutura válida
            // (testes de MIG-ERR-001/002 usam conteúdo real)
            // =========================================================
            services.RemoveAll<ISpreadsheetParser>();
            services.AddSingleton<ISpreadsheetParser>(new StubSpreadsheetParser());

            // =========================================================
            // Portas de import — stubs no-op (não são exercitadas nos
            // testes de contrato da API; exercitadas em Integration.Tests)
            // =========================================================
            services.RemoveAll<IAccountImportPort>();
            services.AddScoped<IAccountImportPort>(_ =>
            {
                var port = Substitute.For<IAccountImportPort>();
                port.CreateOrGetAsync(Arg.Any<AccountImportRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new AccountImportResult(Guid.NewGuid(), true));
                return port;
            });

            services.RemoveAll<IPartnerImportPort>();
            services.AddScoped<IPartnerImportPort>(_ =>
            {
                var port = Substitute.For<IPartnerImportPort>();
                port.CreateOrGetAsync(Arg.Any<PartnerImportRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new PartnerImportResult(Guid.NewGuid(), true));
                return port;
            });

            services.RemoveAll<IOpportunityImportPort>();
            services.AddScoped<IOpportunityImportPort>(_ =>
            {
                var port = Substitute.For<IOpportunityImportPort>();
                port.CreateOrUpdateAsync(Arg.Any<OpportunityImportRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new OpportunityImportResult(Guid.NewGuid(), true));
                return port;
            });

            services.RemoveAll<IActivityImportPort>();
            services.AddScoped<IActivityImportPort>(_ =>
            {
                var port = Substitute.For<IActivityImportPort>();
                port.CreateOrUpdateAsync(Arg.Any<ActivityImportRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new ActivityImportResult(Guid.NewGuid(), true));
                return port;
            });

            services.RemoveAll<IOpportunityNumberPort>();
            services.AddScoped<IOpportunityNumberPort>(_ =>
            {
                var port = Substitute.For<IOpportunityNumberPort>();
                port.AllocateNextAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                    .Returns(OpportunityNumber.FromSequence(1));
                port.GetCurrentSequenceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                    .Returns(0);
                return port;
            });

            services.RemoveAll<IOrganizationReadPort>();
            services.AddScoped<IOrganizationReadPort>(_ =>
            {
                var port = Substitute.For<IOrganizationReadPort>();
                port.GetBuIdByNameAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                    .Returns((Guid?)Guid.NewGuid());
                port.GetStageIdByNameAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                    .Returns((Guid?)Guid.NewGuid());
                port.GetUserIdByNameAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                    .Returns((Guid?)Guid.NewGuid());
                return port;
            });

            // IHttpContextAccessor
            services.AddHttpContextAccessor();
        });
    }

    /// <summary>
    /// Cria HttpClient com headers de PlatformOperator.
    /// </summary>
    public HttpClient CreatePlatOpClient() =>
        CreateClientWithRole("PlatformOperator");

    /// <summary>
    /// Cria HttpClient com headers de TenantAdmin.
    /// </summary>
    public HttpClient CreateTenantAdminClient() =>
        CreateClientWithRole("TenantAdmin");

    /// <summary>
    /// Cria HttpClient sem autenticação (headers ausentes).
    /// </summary>
    public HttpClient CreateUnauthenticatedClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateClientWithRole(string role)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-TenantId", DefaultTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-UserId", DefaultPlatOpId.ToString());
        return client;
    }
}

/// <summary>Implementação fake do contexto de tenant para testes.</summary>
public sealed class TestTenantContext : ICurrentTenantContext
{
    public TestTenantContext(Guid tenantId, Guid userId, string role)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
    }

    public Guid? TenantId { get; }
    public Guid? UserId { get; }
    public string? Role { get; }
}

/// <summary>Feature flags configuráveis por teste.</summary>
public sealed class TestFeatureFlags : IFeatureFlags
{
    private readonly Dictionary<string, bool> _flags = new(StringComparer.Ordinal)
    {
        ["migration.import_enabled"] = true,
    };

    public bool IsEnabled(string flagName) =>
        _flags.TryGetValue(flagName, out var enabled) && enabled;

    public void Set(string flagName, bool enabled) =>
        _flags[flagName] = enabled;
}

/// <summary>Clock com instante fixo para testes determinísticos.</summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
    public DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Stub do ISpreadsheetParser para testes de API.
/// Por padrão retorna estrutura com as colunas canônicas esperadas.
/// Testes de MIG-ERR-001/002 enviam bytes inválidos que causam exceção real no parser.
/// </summary>
public sealed class StubSpreadsheetParser : ISpreadsheetParser
{
    // Colunas mínimas que o UploadSpreadsheetHandler valida (design §5.3)
    private static readonly IReadOnlyList<string> ValidPipelineColumns =
    [
        "Empresa", "Contato", "Responsável", "Etapa",
        "Parceiro", "Meses", "Chave de Importação"
    ];

    private static readonly IReadOnlyList<string> ValidAcoesColumns =
    [
        "Tipo", "Descrição", "Responsável", "Chave de Importação"
    ];

    public Task<SpreadsheetStructure> ParseStructureAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        // Lê os primeiros bytes para verificar se é um ZIP válido (xlsx)
        // Se não for, lança exceção (simula comportamento do parser real)
        var header = new byte[4];
        var read = stream.Read(header, 0, 4);
        stream.Position = 0;

        if (read < 4 || header[0] != 0x50 || header[1] != 0x4B)
        {
            // Não é ZIP — não é xlsx
            throw new InvalidOperationException("MIG-ERR-001: Arquivo não é .xlsx válido.");
        }

        return Task.FromResult(new SpreadsheetStructure(
            ValidPipelineColumns,
            ValidAcoesColumns,
            DetectedRowCount: 0));
    }

    public Task<IReadOnlyList<SourceRow>> ParseRowsAsync(
        Stream stream,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SourceRow>>(Array.Empty<SourceRow>());
}

/// <summary>
/// Repositório em memória do MigrationJob para testes de API.
/// Thread-safe para uso em múltiplos testes simultâneos.
/// </summary>
public sealed class InMemoryMigrationJobRepository : IMigrationJobRepository
{
    private readonly Dictionary<Guid, MigrationJob> _store = new();
    private readonly Lock _lock = new();

    public Task<MigrationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _store.TryGetValue(id, out var job);
            return Task.FromResult(job);
        }
    }

    public Task AddAsync(MigrationJob job, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _store[job.Id] = job;
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MigrationJob job, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _store[job.Id] = job;
        }
        return Task.CompletedTask;
    }

    /// <summary>Injeta um job diretamente para setup de testes.</summary>
    public void Seed(MigrationJob job)
    {
        lock (_lock)
        {
            _store[job.Id] = job;
        }
    }

    /// <summary>Limpa o repositório entre testes.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _store.Clear();
        }
    }

    /// <summary>Obtém todos os jobs (para verificação em testes).</summary>
    public IReadOnlyList<MigrationJob> All()
    {
        lock (_lock)
        {
            return _store.Values.ToList().AsReadOnly();
        }
    }
}
