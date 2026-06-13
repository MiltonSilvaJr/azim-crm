using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantAdministration.Infrastructure.Persistence;

namespace TenantAdministration.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento Fluent API para <see cref="TenantProvisioningRequest"/> na tabela
/// <c>tenant_provisioning_requests</c> (design.md §7.3 — idempotência da saga).
/// </summary>
internal sealed class TenantProvisioningRequestConfiguration
    : IEntityTypeConfiguration<TenantProvisioningRequest>
{
    // Funções estáticas nomeadas necessárias pois expression trees não suportam switch expressions.
    private static string ToDbValue(ProvisioningRequestStatus status)
        => status == ProvisioningRequestStatus.InProgress ? "in_progress"
           : status == ProvisioningRequestStatus.Succeeded ? "succeeded"
           : "failed";

    private static ProvisioningRequestStatus FromDbValue(string value)
        => value == "in_progress" ? ProvisioningRequestStatus.InProgress
           : value == "succeeded" ? ProvisioningRequestStatus.Succeeded
           : ProvisioningRequestStatus.Failed;

    public void Configure(EntityTypeBuilder<TenantProvisioningRequest> builder)
    {
        builder.ToTable("tenant_provisioning_requests");

        builder.HasKey(r => r.IdempotencyKey);
        builder.Property(r => r.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Slug)
            .HasColumnName("slug")
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(r => r.ResultTenantId)
            .HasColumnName("result_tenant_id")
            .IsRequired(false);

        // snake_case conforme CHECK constraint: 'in_progress', 'succeeded', 'failed'
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => ToDbValue(v),
                v => FromDbValue(v));

        builder.Property(r => r.ResultIdentityTenantId)
            .HasColumnName("result_identity_tenant_id")
            .IsRequired(false);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");
    }
}
