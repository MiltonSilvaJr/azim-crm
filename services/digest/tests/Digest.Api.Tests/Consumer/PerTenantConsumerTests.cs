using System.Net;
using System.Text;
using System.Text.Json;
using Digest.Application.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Digest.Api.Tests.Consumer;

/// <summary>
/// Testes de contrato do <c>PerTenantConsumer</c> (TASK-22).
/// Cobre: payload Pub/Sub válido → RunDigestForTenantCommand despachado;
///        payload inválido → 400 (sem retry);
///        falha no handler → 200 (sem propagar para Pub/Sub — RNF 5.3);
///        consumer de webhook de entrega → UpdateDeliveryStatusCommand despachado.
/// Usa <see cref="DigestApiFactory"/> com <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// </summary>
public sealed class PerTenantConsumerTests : IClassFixture<DigestApiFactory>
{
    private readonly DigestApiFactory _factory;

    public PerTenantConsumerTests(DigestApiFactory factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------
    // Payload Pub/Sub válido → comando despachado
    // ---------------------------------------------------------------

    [Fact]
    public async Task Consumer_WithValidPubSubMessage_DispatchesRunDigestForTenantCommand()
    {
        // Arrange — captura chamadas ao mediator para verificar o comando despachado
        var tenantId = Guid.NewGuid();
        var referenceUtc = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid();

        Guid? capturedTenantId = null;
        IMediator? capturedMediator = null;

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Intercepta o mediator para capturar o comando
                var mediator = Substitute.For<IMediator>();
                mediator.Send(
                    Arg.Do<RunDigestForTenantCommand>(cmd => capturedTenantId = cmd.TenantId),
                    Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(new RunDigestForTenantResult(0, 0, 0, 0)));
                // Configurar SelectEligibleTenantsQuery também (necessário para o factory base)
                mediator.Send(
                    Arg.Any<Digest.Application.Queries.SelectEligibleTenantsQuery>(),
                    Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>()));

                capturedMediator = mediator;
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));
                if (descriptor != null) services.Remove(descriptor);
                services.AddSingleton(mediator);
            });
        }).CreateClient();

        var fanoutPayload = new
        {
            tenant_id = tenantId,
            reference_utc = referenceUtc,
            correlation_id = correlationId
        };
        var encodedData = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fanoutPayload, SnakeCaseOptions)));

        var envelope = new
        {
            message = new
            {
                data = encodedData,
                message_id = "test-msg-id-1",
                publish_time = DateTimeOffset.UtcNow.ToString("O"),
                attributes = (object?)null
            },
            subscription = "projects/azim-proj/subscriptions/azim-digest-fanout"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/consume")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(envelope, SnakeCaseOptions),
                Encoding.UTF8,
                "application/json")
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert — consumer deve retornar 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verifica que o comando foi despachado com o tenant_id correto
        Assert.Equal(tenantId, capturedTenantId);
    }

    // ---------------------------------------------------------------
    // Payload inválido → 400 (Pub/Sub não retenta mensagem com 400)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Consumer_WithInvalidPayload_Returns400()
    {
        // Arrange — mensagem com data inválida (não é Base64 válido)
        var envelope = new
        {
            message = new
            {
                data = "%%invalid-base64%%",
                message_id = "test-msg-invalid",
                publish_time = DateTimeOffset.UtcNow.ToString("O"),
                attributes = (object?)null
            },
            subscription = "projects/azim-proj/subscriptions/azim-digest-fanout"
        };

        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/consume")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(envelope, SnakeCaseOptions),
                Encoding.UTF8,
                "application/json")
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert — payload inválido retorna 400 (Pub/Sub não retenta)
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Consumer_WithMissingTenantId_Returns400()
    {
        // Arrange — mensagem com tenant_id ausente
        var payloadSemTenantId = new
        {
            reference_utc = DateTimeOffset.UtcNow
        };
        var encodedData = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payloadSemTenantId, SnakeCaseOptions)));

        var envelope = new
        {
            message = new
            {
                data = encodedData,
                message_id = "test-msg-no-tenant",
                publish_time = DateTimeOffset.UtcNow.ToString("O"),
                attributes = (object?)null
            },
            subscription = "projects/azim-proj/subscriptions/azim-digest-fanout"
        };

        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/consume")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(envelope, SnakeCaseOptions),
                Encoding.UTF8,
                "application/json")
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert — sem tenant_id retorna 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------
    // Falha no handler → 200 (não propaga para Pub/Sub — RNF 5.3)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Consumer_WhenHandlerThrows_Returns200ToPreventInfiniteRetry()
    {
        // Arrange — handler lança exceção (simula falha temporária do tenant)
        var tenantId = Guid.NewGuid();

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mediator = Substitute.For<IMediator>();
                // SelectEligibleTenantsQuery para o factory
                mediator.Send(
                    Arg.Any<Digest.Application.Queries.SelectEligibleTenantsQuery>(),
                    Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>()));
                // RunDigestForTenantCommand lança exceção
                mediator.Send(
                    Arg.Any<RunDigestForTenantCommand>(),
                    Arg.Any<CancellationToken>())
                    .ThrowsAsync(new InvalidOperationException("Falha simulada do tenant"));

                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));
                if (descriptor != null) services.Remove(descriptor);
                services.AddSingleton(mediator);
            });
        }).CreateClient();

        var fanoutPayload = new
        {
            tenant_id = tenantId,
            reference_utc = DateTimeOffset.UtcNow,
            correlation_id = Guid.NewGuid()
        };
        var encodedData = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fanoutPayload, SnakeCaseOptions)));

        var envelope = new
        {
            message = new
            {
                data = encodedData,
                message_id = "test-msg-failure",
                publish_time = DateTimeOffset.UtcNow.ToString("O"),
                attributes = (object?)null
            },
            subscription = "projects/azim-proj/subscriptions/azim-digest-fanout"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/consume")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(envelope, SnakeCaseOptions),
                Encoding.UTF8,
                "application/json")
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert — falha no handler não propaga para Pub/Sub (retorna 200 para evitar retry infinito)
        // A retry policy da subscription gerencia retentativas com DLQ (design §6.3, RNF 5.3)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------------------------------------------------------------
    // Webhook de status de entrega → UpdateDeliveryStatusCommand
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeliveryStatusWebhook_WithValidDeliveredStatus_Returns200()
    {
        // Arrange — evento de entrega do provedor
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mediator = Substitute.For<IMediator>();
                mediator.Send(
                    Arg.Any<Digest.Application.Queries.SelectEligibleTenantsQuery>(),
                    Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>()));
                mediator.Send(
                    Arg.Any<UpdateDeliveryStatusCommand>(),
                    Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(new UpdateDeliveryStatusResult(Updated: true, Skipped: false)));

                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));
                if (descriptor != null) services.Remove(descriptor);
                services.AddSingleton(mediator);
            });
        }).CreateClient();

        var webhookPayload = new
        {
            message_id = "provider-msg-id-123",
            status = "delivered",
            tenant_id = (string?)null
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/delivery-status")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(webhookPayload, SnakeCaseOptions),
                Encoding.UTF8,
                "application/json")
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("updated").GetBoolean());
    }

    [Fact]
    public async Task DeliveryStatusWebhook_WithUnknownStatus_ReturnsSkipped()
    {
        // Arrange — status desconhecido → ignorado graciosamente
        var client = _factory.CreateClient();

        var webhookPayload = new
        {
            message_id = "provider-msg-id-456",
            status = "unknown_status_xyz",
            tenant_id = (string?)null
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/digest/delivery-status")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(webhookPayload, SnakeCaseOptions),
                Encoding.UTF8,
                "application/json")
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert — status desconhecido retorna 200 com skipped=true
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("skipped").GetBoolean());
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };
}
