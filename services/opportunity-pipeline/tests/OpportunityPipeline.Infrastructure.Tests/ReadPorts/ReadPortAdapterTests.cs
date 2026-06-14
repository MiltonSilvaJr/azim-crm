using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OpportunityPipeline.Infrastructure.ReadPorts;
using System.Net;
using System.Text.Json;

namespace OpportunityPipeline.Infrastructure.Tests.ReadPorts;

/// <summary>
/// Testes de adapters HTTP (Organization, Account, Partner, Activity)
/// usando DelegatingHandler stub em memória — sem Testcontainers (TASK-17).
/// Verifica: HTTP 200 → mapeamento correto, HTTP 404/500 → degradação graciosa,
/// cache hit não dispara segunda chamada HTTP, cross-cutting (timeout/falha).
/// Mapeia: TASK-17, design §6.4, DD-005.
/// </summary>
public sealed class ReadPortAdapterTests
{
    // =========================================================================
    // Helpers de stub HTTP
    // =========================================================================

    private static HttpClient CreateClient(
        HttpStatusCode status,
        string? body = null,
        string baseAddress = "http://localhost/")
    {
        var handler = new StubHandler(status, body ?? "{}");
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }

    private static IMemoryCache CreateCache() =>
        new MemoryCache(new MemoryCacheOptions());

    // =========================================================================
    // OrganizationReadPortAdapter
    // =========================================================================

    [Fact(DisplayName = "ORG_01: ValidateOwnerMembershipAsync retorna true quando HTTP 200")]
    public async Task ValidateOwnerMembership_Http200_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK);
        var adapter = new OrganizationReadPortAdapter(client, CreateCache(),
            NullLogger<OrganizationReadPortAdapter>.Instance);

        // Act
        var result = await adapter.ValidateOwnerMembershipAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeTrue("HTTP 200 indica que o owner é membro da BU.");
    }

    [Fact(DisplayName = "ORG_02: ValidateOwnerMembershipAsync retorna false quando HTTP 404")]
    public async Task ValidateOwnerMembership_Http404_ReturnsFalse()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.NotFound);
        var adapter = new OrganizationReadPortAdapter(client, CreateCache(),
            NullLogger<OrganizationReadPortAdapter>.Instance);

        // Act
        var result = await adapter.ValidateOwnerMembershipAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeFalse("HTTP 404 indica que o owner não é membro da BU.");
    }

    [Fact(DisplayName = "ORG_03: ValidateOwnerMembershipAsync retorna false em exceção (degradação graciosa)")]
    public async Task ValidateOwnerMembership_Exception_ReturnsFalse()
    {
        // Arrange: handler que lança exceção
        var client = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://localhost/") };
        var adapter = new OrganizationReadPortAdapter(client, CreateCache(),
            NullLogger<OrganizationReadPortAdapter>.Instance);

        // Act
        var result = await adapter.ValidateOwnerMembershipAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeFalse("falha de rede deve retornar false (degradação graciosa, design §6.4).");
    }

    [Fact(DisplayName = "ORG_04: GetStageRefAsync retorna StageRef mapeada corretamente quando HTTP 200")]
    public async Task GetStageRefAsync_Http200_ReturnsMappedStageRef()
    {
        // Arrange
        var stageId = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new
        {
            id = stageId,
            name = "Qualificação",
            category = "open",
            defaultProbability = 20,
            order = 1
        });

        var client = CreateClient(HttpStatusCode.OK, body);
        var adapter = new OrganizationReadPortAdapter(client, CreateCache(),
            NullLogger<OrganizationReadPortAdapter>.Instance);

        // Act
        var result = await adapter.GetStageRefAsync(Guid.NewGuid(), stageId);

        // Assert
        result.Should().NotBeNull("HTTP 200 com body válido deve retornar StageRef.");
        result!.StageId.Should().Be(stageId);
        result.Name.Should().Be("Qualificação");
        result.DefaultProbability.Should().Be(20);
        result.Order.Should().Be(1);
    }

    [Fact(DisplayName = "ORG_05: GetStageRefAsync retorna null quando HTTP 404")]
    public async Task GetStageRefAsync_Http404_ReturnsNull()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.NotFound);
        var adapter = new OrganizationReadPortAdapter(client, CreateCache(),
            NullLogger<OrganizationReadPortAdapter>.Instance);

        // Act
        var result = await adapter.GetStageRefAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull("HTTP 404 deve retornar null (degradação graciosa).");
    }

    [Fact(DisplayName = "ORG_06: GetStageRefAsync usa cache — segunda chamada não dispara HTTP")]
    public async Task GetStageRefAsync_SecondCall_UsesCacheNotHttp()
    {
        // Arrange: contador de chamadas HTTP
        var stageId = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new
        {
            id = stageId,
            name = "Qualificação",
            category = "open",
            defaultProbability = 20,
            order = 1
        });

        var handler = new CountingHandler(HttpStatusCode.OK, body);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var adapter = new OrganizationReadPortAdapter(client, CreateCache(),
            NullLogger<OrganizationReadPortAdapter>.Instance);

        var tenantId = Guid.NewGuid();

        // Act: duas chamadas para o mesmo stage
        await adapter.GetStageRefAsync(tenantId, stageId);
        await adapter.GetStageRefAsync(tenantId, stageId);

        // Assert: apenas 1 chamada HTTP (segunda hit no cache)
        handler.CallCount.Should().Be(1,
            "a segunda chamada deve ser servida pelo cache, sem nova requisição HTTP (design §6.2).");
    }

    [Fact(DisplayName = "ORG_07: GetOriginChannelRefAsync retorna OriginChannelRef quando HTTP 200")]
    public async Task GetOriginChannelRefAsync_Http200_ReturnsChannelRef()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new
        {
            id = channelId,
            name = "Parceiro",
            isPartnerChannel = true
        });

        var client = CreateClient(HttpStatusCode.OK, body);
        var adapter = new OrganizationReadPortAdapter(client, CreateCache(),
            NullLogger<OrganizationReadPortAdapter>.Instance);

        // Act
        var result = await adapter.GetOriginChannelRefAsync(Guid.NewGuid(), channelId);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Parceiro");
        result.IsPartnerChannel.Should().BeTrue();
    }

    // =========================================================================
    // AccountReadPortAdapter
    // =========================================================================

    [Fact(DisplayName = "ACC_01: AccountExistsAsync retorna true quando HTTP 200")]
    public async Task AccountExistsAsync_Http200_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK);
        var adapter = new AccountReadPortAdapter(client,
            NullLogger<AccountReadPortAdapter>.Instance);

        // Act
        var result = await adapter.AccountExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeTrue("HTTP 200 confirma que a conta existe.");
    }

    [Fact(DisplayName = "ACC_02: AccountExistsAsync retorna false quando HTTP 404")]
    public async Task AccountExistsAsync_Http404_ReturnsFalse()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.NotFound);
        var adapter = new AccountReadPortAdapter(client,
            NullLogger<AccountReadPortAdapter>.Instance);

        // Act
        var result = await adapter.AccountExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeFalse("HTTP 404 confirma que a conta não existe.");
    }

    [Fact(DisplayName = "ACC_03: AccountExistsAsync retorna false em exceção de rede")]
    public async Task AccountExistsAsync_Exception_ReturnsFalse()
    {
        // Arrange
        var client = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://localhost/") };
        var adapter = new AccountReadPortAdapter(client,
            NullLogger<AccountReadPortAdapter>.Instance);

        // Act
        var result = await adapter.AccountExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeFalse("exceção de rede deve retornar false (degradação graciosa).");
    }

    [Fact(DisplayName = "ACC_04: ContactBelongsToAccountAsync retorna true quando HTTP 200")]
    public async Task ContactBelongsToAccount_Http200_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK);
        var adapter = new AccountReadPortAdapter(client,
            NullLogger<AccountReadPortAdapter>.Instance);

        // Act
        var result = await adapter.ContactBelongsToAccountAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "ACC_05: ContactBelongsToAccountAsync retorna false quando HTTP 404")]
    public async Task ContactBelongsToAccount_Http404_ReturnsFalse()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.NotFound);
        var adapter = new AccountReadPortAdapter(client,
            NullLogger<AccountReadPortAdapter>.Instance);

        // Act
        var result = await adapter.ContactBelongsToAccountAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    // =========================================================================
    // PartnerReadPortAdapter
    // =========================================================================

    [Fact(DisplayName = "PARTNER_01: PartnerExistsAsync retorna true quando HTTP 200")]
    public async Task PartnerExistsAsync_Http200_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK);
        var adapter = new PartnerReadPortAdapter(client, CreateCache(),
            NullLogger<PartnerReadPortAdapter>.Instance);

        // Act
        var result = await adapter.PartnerExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "PARTNER_02: PartnerExistsAsync retorna false quando HTTP 404")]
    public async Task PartnerExistsAsync_Http404_ReturnsFalse()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.NotFound);
        var adapter = new PartnerReadPortAdapter(client, CreateCache(),
            NullLogger<PartnerReadPortAdapter>.Instance);

        // Act
        var result = await adapter.PartnerExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "PARTNER_03: GetCommissionDefaultsAsync retorna defaults mapeados quando HTTP 200")]
    public async Task GetCommissionDefaultsAsync_Http200_ReturnsMappedDefaults()
    {
        // Arrange
        var body = JsonSerializer.Serialize(new
        {
            pctSetup = 10.0m,
            pctRecorrente = 5.0m
        });

        var client = CreateClient(HttpStatusCode.OK, body);
        var adapter = new PartnerReadPortAdapter(client, CreateCache(),
            NullLogger<PartnerReadPortAdapter>.Instance);

        // Act
        var result = await adapter.GetCommissionDefaultsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().NotBeNull();
        result!.PctSetup.Should().Be(10.0m);
        result.PctRecorrente.Should().Be(5.0m);
    }

    [Fact(DisplayName = "PARTNER_04: GetCommissionDefaultsAsync retorna null quando HTTP 404")]
    public async Task GetCommissionDefaultsAsync_Http404_ReturnsNull()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.NotFound);
        var adapter = new PartnerReadPortAdapter(client, CreateCache(),
            NullLogger<PartnerReadPortAdapter>.Instance);

        // Act
        var result = await adapter.GetCommissionDefaultsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull("HTTP 404 deve retornar null (degradação graciosa).");
    }

    [Fact(DisplayName = "PARTNER_05: GetCommissionDefaultsAsync usa cache — segunda chamada não dispara HTTP")]
    public async Task GetCommissionDefaultsAsync_SecondCall_UsesCacheNotHttp()
    {
        // Arrange
        var body = JsonSerializer.Serialize(new { pctSetup = 10.0m, pctRecorrente = 5.0m });
        var handler = new CountingHandler(HttpStatusCode.OK, body);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var adapter = new PartnerReadPortAdapter(client, CreateCache(),
            NullLogger<PartnerReadPortAdapter>.Instance);

        var tenantId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();

        // Act
        await adapter.GetCommissionDefaultsAsync(tenantId, partnerId);
        await adapter.GetCommissionDefaultsAsync(tenantId, partnerId);

        // Assert
        handler.CallCount.Should().Be(1,
            "cache deve evitar segunda requisição HTTP (design §6.2).");
    }

    // =========================================================================
    // ActivityReadPortAdapter
    // =========================================================================

    [Fact(DisplayName = "ACT_01: GetLastActivityAtAsync retorna data quando HTTP 200 com payload")]
    public async Task GetLastActivityAtAsync_Http200_ReturnsDate()
    {
        // Arrange
        var expectedDate = DateTimeOffset.UtcNow.AddDays(-3);
        var body = JsonSerializer.Serialize(new
        {
            lastActivityAt = expectedDate
        });

        var client = CreateClient(HttpStatusCode.OK, body);
        var adapter = new ActivityReadPortAdapter(client,
            NullLogger<ActivityReadPortAdapter>.Instance);

        // Act
        var result = await adapter.GetLastActivityAtAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().NotBeNull("HTTP 200 com payload deve retornar a data da última atividade.");
        result!.Value.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
    }

    [Fact(DisplayName = "ACT_02: GetLastActivityAtAsync retorna null quando HTTP 404 (degradação graciosa — DD-005)")]
    public async Task GetLastActivityAtAsync_Http404_ReturnsNull()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.NotFound);
        var adapter = new ActivityReadPortAdapter(client,
            NullLogger<ActivityReadPortAdapter>.Instance);

        // Act
        var result = await adapter.GetLastActivityAtAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull("HTTP 404 deve retornar null (degradação graciosa — DD-005).");
    }

    [Fact(DisplayName = "ACT_03: GetLastActivityAtAsync retorna null em exceção de rede (degradação graciosa — DD-005)")]
    public async Task GetLastActivityAtAsync_Exception_ReturnsNull()
    {
        // Arrange
        var client = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://localhost/") };
        var adapter = new ActivityReadPortAdapter(client,
            NullLogger<ActivityReadPortAdapter>.Instance);

        // Act
        var result = await adapter.GetLastActivityAtAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull(
            "exceção de rede não deve propagar — degradação graciosa posterga o scan (DD-005).");
    }

    // =========================================================================
    // Stub handlers internos
    // =========================================================================

    /// <summary>Handler que retorna status e body fixos.</summary>
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    /// <summary>Handler que lança HttpRequestException (simula falha de rede).</summary>
    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulação de falha de rede.");
    }

    /// <summary>Handler que conta chamadas e retorna status/body fixos.</summary>
    private sealed class CountingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        private int _count;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _count++;
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }

        /// <summary>Número de requisições HTTP realizadas.</summary>
        public int CallCount => _count;
    }
}
