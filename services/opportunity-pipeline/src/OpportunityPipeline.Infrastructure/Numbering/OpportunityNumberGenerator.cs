using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using OpportunityPipeline.Infrastructure.Persistence;

namespace OpportunityPipeline.Infrastructure.Numbering;

/// <summary>
/// Implementação da geração atômica de OpportunityNumber por tenant.
/// Usa INSERT ... ON CONFLICT DO UPDATE ... RETURNING para serializar concorrência
/// por tenant sem bloquear outros tenants (DD-001, PBT-01).
///
/// A query executa dentro da transação de criação da oportunidade.
/// O lock de linha por tenant garante serialização sem afetar outros tenants.
/// O contador nunca volta — mesmo em rollback, o valor não é reutilizado.
///
/// Mapeia: Req 3, INV-5, DD-001, PBT-01, design §6.1, TASK-15.
/// </summary>
public sealed class OpportunityNumberGenerator(OpportunityDbContext context) : IOpportunityNumberGenerator
{
    // Prefixo canônico do número de oportunidade (Req 3, regex ^AZ-\d{4,}$)
    private const string Prefix = "AZ";

    /// <inheritdoc/>
    public async Task<OpportunityNumber> NextAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Executa SQL de geração atômica via Npgsql com parâmetro tipado.
        // Retorna o valor consumido (next_value - 1 após o incremento).
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Obtém o DbTransaction corrente (se houver) para participar da mesma transação.
            var efTx = context.Database.CurrentTransaction;
            var dbTx = efTx?.GetDbTransaction();

            // Garante que app.current_tenant está definido nesta conexão antes do INSERT.
            // GetDbConnection().OpenAsync() não passa pelo EF Core interceptor — por isso
            // o SET deve ser feito explicitamente aqui (ADR-0001 camada 3).
            await using (var setTenantCmd = connection.CreateCommand())
            {
                setTenantCmd.CommandText = $"SET app.current_tenant = '{tenantId}'";
                if (dbTx is not null)
                    setTenantCmd.Transaction = dbTx;
                await setTenantCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var command = connection.CreateCommand();

            // Participa da transação corrente, se houver
            // IDbContextTransaction não expõe GetDbTransaction diretamente — usa o DbTransaction subjacente
            if (dbTx is not null)
            {
                command.Transaction = dbTx;
            }

            command.CommandText = @"
                INSERT INTO opportunity_number_sequences (tenant_id, next_value)
                VALUES (@tenantId, 2)
                ON CONFLICT (tenant_id) DO UPDATE
                    SET next_value = opportunity_number_sequences.next_value + 1
                RETURNING next_value - 1";

            var param = command.CreateParameter();
            param.ParameterName = "tenantId";
            param.Value = tenantId;
            command.Parameters.Add(param);

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            if (result is null or DBNull)
                throw new InvalidOperationException(
                    "Falha ao gerar número de oportunidade — RETURNING não retornou valor (DD-001).");

            var sequenceValue = Convert.ToInt64(result);
            var formatted = $"{Prefix}-{sequenceValue:0000}";
            return new OpportunityNumber(formatted);
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }
}
