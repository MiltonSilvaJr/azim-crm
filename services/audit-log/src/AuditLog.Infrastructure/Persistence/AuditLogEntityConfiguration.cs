using AuditLog.Domain.Aggregates;
using AuditLog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuditLog.Infrastructure.Persistence;

/// <summary>
/// Configuração de mapeamento EF Core para <see cref="AuditLogAggregate"/> → tabela <c>audit_logs</c>.
/// Convenção snake_case em todas as colunas conforme <c>.forge/rules/conventions/database-naming.md</c>.
/// Sem <c>updated_at</c> nem coluna de soft-delete (REQ-002.5, RNF-001).
/// EntityReference é mapeada via shadow properties + conversão post-load via interceptor.
/// </summary>
internal sealed class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLogAggregate>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Configure(EntityTypeBuilder<AuditLogAggregate> builder)
    {
        builder.ToTable("audit_logs");

        // Chave primária: id UUID
        // Usa ValueConverter explícito para que o Sanitize funcione corretamente em queries.
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(new ValueConverter<AuditLogId, Guid>(
                id => id.Value,
                value => AuditLogId.From(value)))
            .ValueGeneratedNever();

        // tenant_id — NOT NULL, chave de RLS (REQ-005.1)
        // ValueConverter explícito para habilitar comparações em HasQueryFilter e queries.
        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired()
            .HasConversion(new ValueConverter<TenantId, Guid>(
                tid => tid.Value,
                value => TenantId.From(value)));

        // user_id — NOT NULL, autor da operação (REQ-002.2)
        builder.Property(x => x.ActorId)
            .HasColumnName("user_id")
            .IsRequired()
            .HasConversion(new ValueConverter<ActorId, Guid>(
                aid => aid.Value,
                value => ActorId.From(value)));

        // EntityReference é um computed property — mapeado via backing fields internos.
        // O EF Core mapeia EntityTypePersisted e EntityIdPersisted diretamente.
        builder.Ignore(x => x.EntityReference);

        builder.Property(x => x.EntityTypePersisted)
            .HasColumnName("entity_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EntityIdPersisted)
            .HasColumnName("entity_id")
            .IsRequired();

        // action — NOT NULL, VARCHAR(20), valores: create/update/delete
        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion(
                action => action.ToString().ToLowerInvariant(),
                value => Enum.Parse<AuditAction>(value, ignoreCase: true));

        // delta_json — NOT NULL, JSONB
        builder.Property(x => x.Delta)
            .HasColumnName("delta_json")
            .IsRequired()
            .HasColumnType("jsonb")
            .HasConversion(
                delta => SerializeDelta(delta),
                json => DeserializeDelta(json));

        // created_at — NOT NULL, DEFAULT now() no banco (REQ-002.4)
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Índices conforme design §7.1
        // ix_audit_logs_tenant_entity: (tenant_id, entity_type, entity_id)
        builder.HasIndex(x => new { x.TenantId, x.EntityTypePersisted, x.EntityIdPersisted })
            .HasDatabaseName("ix_audit_logs_tenant_entity");

        // ix_audit_logs_tenant_created: (tenant_id, created_at DESC)
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
            .HasDatabaseName("ix_audit_logs_tenant_created")
            .IsDescending(false, true);

        // ix_audit_logs_tenant_user: (tenant_id, user_id)
        builder.HasIndex(x => new { x.TenantId, x.ActorId })
            .HasDatabaseName("ix_audit_logs_tenant_user");
    }

    // ------------------------------------------------------------------ Serialização do delta

    private static string SerializeDelta(AuditDelta delta)
    {
        var envelope = new DeltaEnvelope
        {
            Kind = delta.Kind.ToString().ToLowerInvariant(),
            After = delta.After,
            Before = delta.Before,
            Changes = delta.Changes?.ToDictionary(
                kvp => kvp.Key,
                kvp => new DeltaChange { Before = kvp.Value.Before, After = kvp.Value.After })
        };
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    private static AuditDelta DeserializeDelta(string json)
    {
        var envelope = JsonSerializer.Deserialize<DeltaEnvelope>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                "delta_json inválido: não pôde ser desserializado.");

        return envelope.Kind switch
        {
            "create" => AuditDelta.ForCreate(
                envelope.After
                    ?? throw new InvalidOperationException("delta_json create sem 'after'.")),
            "delete" => AuditDelta.ForDelete(
                envelope.Before
                    ?? throw new InvalidOperationException("delta_json delete sem 'before'.")),
            "update" => AuditDelta.ForUpdate(
                (envelope.Changes
                    ?? throw new InvalidOperationException("delta_json update sem 'changes'."))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => new AuditAttributeChange(kvp.Value.Before, kvp.Value.After))
                .AsReadOnly()),
            _ => throw new InvalidOperationException(
                $"delta_json kind desconhecido: '{envelope.Kind}'.")
        };
    }

    // ------------------------------------------------------------------ DTOs internos de serialização

    private sealed class DeltaEnvelope
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("after")]
        public IReadOnlyDictionary<string, object?>? After { get; set; }

        [JsonPropertyName("before")]
        public IReadOnlyDictionary<string, object?>? Before { get; set; }

        [JsonPropertyName("changes")]
        public Dictionary<string, DeltaChange>? Changes { get; set; }
    }

    private sealed class DeltaChange
    {
        [JsonPropertyName("before")]
        public object? Before { get; set; }

        [JsonPropertyName("after")]
        public object? After { get; set; }
    }
}
