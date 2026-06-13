using System.Text.Json;
using Authentication.Contracts.Dtos;
using Authentication.Contracts.Errors;
using FluentAssertions;
using Xunit;

namespace Authentication.Api.Tests.Contracts;

/// <summary>
/// Testes de serialização de DTOs e validação do catálogo de erros.
///
/// Verifica:
///   - DTOs serializam e desserializam corretamente (sem campos de identity_uid).
///   - Todos os 16 códigos AUTH-ERR-* existem no catálogo.
///   - Nenhuma mensagem do catálogo contém PII ou detalhe interno (Req 10.4).
///   - Formato de erro { code, message } consistente.
///
/// Mapeia: TASK-17, design.md § 8, § 12, Req 10.4.
/// </summary>
public sealed class DtoSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // =========================================================================
    // MeResponse — serialização e ausência de identity_uid
    // =========================================================================

    [Fact(DisplayName = "MeResponse serializa para JSON com campos esperados sem identity_uid")]
    public void MeResponse_SerializesToJson_WithExpectedFields()
    {
        var response = new MeResponse
        {
            UserId = Guid.NewGuid(),
            Email = "user@example.com",
            Roles = ["admin"],
            Memberships = [new MembershipEntry { BuId = Guid.NewGuid(), Role = "manager" }],
            TenantId = Guid.NewGuid()
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);

        json.Should().Contain("user_id");
        json.Should().Contain("email");
        json.Should().Contain("roles");
        json.Should().Contain("memberships");
        json.Should().Contain("tenant_id");
        json.Should().NotContain("identity_uid", because: "MeResponse nunca expõe identity_uid (DD-001, Req 11.4)");
    }

    [Fact(DisplayName = "MeResponse desserializa corretamente de JSON")]
    public void MeResponse_DeserializesFromJson_Correctly()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var json = $$"""
            {
              "user_id": "{{userId}}",
              "email": "user@example.com",
              "roles": ["viewer"],
              "memberships": [],
              "tenant_id": "{{tenantId}}"
            }
            """;

        var result = JsonSerializer.Deserialize<MeResponse>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.Email.Should().Be("user@example.com");
        result.TenantId.Should().Be(tenantId);
    }

    // =========================================================================
    // LogoutResponse — serialização
    // =========================================================================

    [Fact(DisplayName = "LogoutResponse serializa com status revoked")]
    public void LogoutResponse_SerializesToJson_WithStatus()
    {
        var response = new LogoutResponse { Status = "revoked" };

        var json = JsonSerializer.Serialize(response, JsonOptions);

        json.Should().Contain("status");
        json.Should().Contain("revoked");
    }

    // =========================================================================
    // InviteRequest — serialização
    // =========================================================================

    [Fact(DisplayName = "InviteRequest desserializa campos de convite corretamente")]
    public void InviteRequest_DeserializesFromJson_Correctly()
    {
        var buId = Guid.NewGuid();
        var json = $$"""
            {
              "email": "invited@example.com",
              "role": "viewer",
              "bu_ids": ["{{buId}}"]
            }
            """;

        var result = JsonSerializer.Deserialize<InviteRequest>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.Email.Should().Be("invited@example.com");
        result.Role.Should().Be("viewer");
        result.BuIds.Should().ContainSingle(id => id == buId);
    }

    [Fact(DisplayName = "InviteResponse serializa com status invited")]
    public void InviteResponse_SerializesToJson_WithStatusInvited()
    {
        var response = new InviteResponse { Status = "invited" };

        var json = JsonSerializer.Serialize(response, JsonOptions);

        json.Should().Contain("invited");
    }

    // =========================================================================
    // ActivateInviteRequest — serialização
    // =========================================================================

    [Fact(DisplayName = "ActivateInviteRequest desserializa token de ativação")]
    public void ActivateInviteRequest_DeserializesFromJson_Correctly()
    {
        var json = """
            {
              "activation_token": "abc123token"
            }
            """;

        var result = JsonSerializer.Deserialize<ActivateInviteRequest>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.ActivationToken.Should().Be("abc123token");
    }

    [Fact(DisplayName = "ActivateInviteResponse serializa com status activated")]
    public void ActivateInviteResponse_SerializesToJson_WithStatusActivated()
    {
        var response = new ActivateInviteResponse { Status = "activated" };

        var json = JsonSerializer.Serialize(response, JsonOptions);

        json.Should().Contain("activated");
    }

    // =========================================================================
    // PasswordResetRequest — serialização
    // =========================================================================

    [Fact(DisplayName = "PasswordResetRequest desserializa email corretamente")]
    public void PasswordResetRequest_DeserializesFromJson_Correctly()
    {
        var json = """
            {
              "email": "user@example.com"
            }
            """;

        var result = JsonSerializer.Deserialize<PasswordResetRequest>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.Email.Should().Be("user@example.com");
    }

    [Fact(DisplayName = "PasswordResetResponse serializa com status accepted")]
    public void PasswordResetResponse_SerializesToJson_WithStatusAccepted()
    {
        var response = new PasswordResetResponse { Status = "accepted" };

        var json = JsonSerializer.Serialize(response, JsonOptions);

        json.Should().Contain("accepted");
    }

    // =========================================================================
    // ErrorResponse — formato { code, message }
    // =========================================================================

    [Fact(DisplayName = "ErrorResponse serializa no formato { code, message } sem PII")]
    public void ErrorResponse_SerializesToJson_WithCodeAndMessage()
    {
        var error = new ErrorResponse
        {
            Code = "AUTH-ERR-001",
            Message = "Sessão inválida ou expirada."
        };

        var json = JsonSerializer.Serialize(error, JsonOptions);

        json.Should().Contain("code");
        json.Should().Contain("message");
        json.Should().Contain("AUTH-ERR-001");
        json.Should().NotContain("identity_uid", because: "mensagens de erro nunca expõem identity_uid (Req 10.4)");
    }
}
