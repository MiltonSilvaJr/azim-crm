using OpportunityPipeline.Infrastructure.Observability;

namespace OpportunityPipeline.Infrastructure.Tests.Observability;

/// <summary>
/// Testes do scan automatizado de PII em logs/traces (TASK-24, RNF 10.4, design §11).
/// Verifica que o PiiLogScanner detecta corretamente campos proibidos
/// e que logs simulados de operações não contêm PII.
/// Gate de CI: scan PII verde = nenhum campo proibido em logs de operações críticas.
/// Mapeia: TASK-24, RNF 10.4, design §11, LGPD, INV-13.
/// </summary>
[Trait("Category", "PiiScan")]
public sealed class PiiScanTests
{
    // =========================================================================
    // PII-01 — Scanner detecta e-mail
    // =========================================================================

    [Fact(DisplayName = "PII-01: PiiLogScanner detecta e-mail em linha de log")]
    public void Scanner_DetectsEmail_InLogLine()
    {
        var line = @"[INFO] Opportunity created {""opportunity_id"":""abc123"",""contact_email"":""joao.silva@empresa.com""}";
        PiiLogScanner.ContainsPii(line).Should().BeTrue("e-mail deve ser detectado como PII.");
    }

    // =========================================================================
    // PII-02 — Scanner detecta CPF
    // =========================================================================

    [Fact(DisplayName = "PII-02: PiiLogScanner detecta CPF formatado em linha de log")]
    public void Scanner_DetectsCpf_InLogLine()
    {
        var line = "[WARN] user cpf=123.456.789-00 accessed resource";
        PiiLogScanner.ContainsPii(line).Should().BeTrue("CPF deve ser detectado como PII.");
    }

    // =========================================================================
    // PII-03 — Scanner detecta telefone BR
    // =========================================================================

    [Fact(DisplayName = "PII-03: PiiLogScanner detecta telefone BR em linha de log")]
    public void Scanner_DetectsPhone_InLogLine()
    {
        var line = "[DEBUG] contact phone=(11) 98765-4321 linked to opportunity";
        PiiLogScanner.ContainsPii(line).Should().BeTrue("telefone deve ser detectado como PII.");
    }

    // =========================================================================
    // PII-04 — Scanner detecta contact_name em JSON
    // =========================================================================

    [Fact(DisplayName = "PII-04: PiiLogScanner detecta contact_name em payload JSON de log")]
    public void Scanner_DetectsContactName_InJsonLog()
    {
        var line = @"[INFO] {""contact_name"":""Maria Oliveira"",""opportunity_id"":""xyz""}";
        PiiLogScanner.ContainsPii(line).Should().BeTrue("contact_name deve ser detectado como PII.");
    }

    // =========================================================================
    // PII-05 — Scanner não detecta logs limpos de operação de criação
    // =========================================================================

    [Fact(DisplayName = "PII-05: log de CreateOpportunity sem PII não é detectado pelo scanner")]
    public void Scanner_DoesNotFlagClean_CreateOpportunityLog()
    {
        // Simula log do LoggingBehavior (design §5.4, RNF 10.1) — sem PII
        var cleanLog = @"[INFO] {
            ""correlation_id"": ""abc123"",
            ""tenant_id"": ""11111111-1111-1111-1111-111111111111"",
            ""bu_id"": ""22222222-2222-2222-2222-222222222222"",
            ""command_type"": ""CreateOpportunityCommand"",
            ""elapsed_ms"": 42
        }";

        PiiLogScanner.ContainsPii(cleanLog).Should().BeFalse(
            "log de CreateOpportunity deve ter apenas IDs e metadados — sem PII.");
    }

    // =========================================================================
    // PII-06 — Scanner não detecta logs limpos de WinOpportunity
    // =========================================================================

    [Fact(DisplayName = "PII-06: log de WinOpportunity sem PII não é detectado pelo scanner")]
    public void Scanner_DoesNotFlagClean_WinOpportunityLog()
    {
        // Simula log do WinOpportunityHandler (design §11, RNF 10.3) — sem PII
        var cleanLog = @"[INFO] {
            ""correlation_id"": ""win-123"",
            ""tenant_id"": ""aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"",
            ""opportunity_id"": ""bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"",
            ""action"": ""Win"",
            ""commission_snapshot_created"": true,
            ""elapsed_ms"": 150
        }";

        PiiLogScanner.ContainsPii(cleanLog).Should().BeFalse(
            "log de WinOpportunity deve ter apenas IDs e metadados — sem PII.");
    }

    // =========================================================================
    // PII-07 — Scanner não detecta logs limpos de movimentação de estágio
    // =========================================================================

    [Fact(DisplayName = "PII-07: log de MoveStage sem PII não é detectado pelo scanner")]
    public void Scanner_DoesNotFlagClean_MoveStageLog()
    {
        var cleanLog = @"[INFO] {
            ""correlation_id"": ""move-456"",
            ""tenant_id"": ""cccccccc-cccc-cccc-cccc-cccccccccccc"",
            ""opportunity_id"": ""dddddddd-dddd-dddd-dddd-dddddddddddd"",
            ""from_stage_id"": ""eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"",
            ""to_stage_id"": ""ffffffff-ffff-ffff-ffff-ffffffffffff"",
            ""command_type"": ""MoveStageCommand"",
            ""elapsed_ms"": 35
        }";

        PiiLogScanner.ContainsPii(cleanLog).Should().BeFalse(
            "log de MoveStage deve ter apenas IDs e metadados — sem PII.");
    }

    // =========================================================================
    // PII-08 — ScanLines retorna lista vazia para batch de logs limpos
    // =========================================================================

    [Fact(DisplayName = "PII-08: ScanLines retorna lista vazia para batch de logs de operações limpos")]
    public void ScanLines_ReturnsEmpty_ForCleanOperationLogs()
    {
        // Simula 10 linhas de log de operações críticas — todas sem PII
        var cleanLogs = new[]
        {
            @"[INFO] {""correlation_id"":""a1"",""tenant_id"":""t1"",""command_type"":""CreateOpportunityCommand"",""elapsed_ms"":50}",
            @"[INFO] {""correlation_id"":""a2"",""tenant_id"":""t1"",""opportunity_id"":""o1"",""action"":""MoveStage"",""elapsed_ms"":30}",
            @"[INFO] {""correlation_id"":""a3"",""tenant_id"":""t1"",""opportunity_id"":""o1"",""action"":""Win"",""commission_snapshot_created"":true,""elapsed_ms"":120}",
            @"[INFO] {""correlation_id"":""a4"",""tenant_id"":""t1"",""opportunity_id"":""o2"",""action"":""Lose"",""loss_reason_id"":""lr1"",""elapsed_ms"":45}",
            @"[INFO] {""correlation_id"":""a5"",""tenant_id"":""t1"",""action"":""GetKanban"",""bu_id"":""b1"",""elapsed_ms"":180}",
            @"[DEBUG] Outbox published event_id=ev1 type=opportunity.created.v1",
            @"[DEBUG] RLS interceptor set app.current_tenant tenant_id=t1",
            @"[INFO] Health check: db=Healthy pubsub=Healthy",
            @"[INFO] Metrics: opportunities_created_total=5 outbox_pending_events=0",
            @"[WARN] (VAL-07) Oportunidade ganha sem comissão de parceiro — snapshot com valor zero registrado opportunity_id=o3",
        };

        var violations = PiiLogScanner.ScanLines(cleanLogs);

        violations.Should().BeEmpty(
            "todos os logs de operações críticas devem estar livres de PII " +
            "(RNF 10.4, design §11, LGPD, TASK-24).");
    }

    // =========================================================================
    // PII-09 — ScanLines retorna violações para batch misto (auto-verificação do scanner)
    // =========================================================================

    [Fact(DisplayName = "PII-09: ScanLines detecta PII em batch com linhas sujas (auto-verificação do gate)")]
    public void ScanLines_DetectsViolations_InDirtyBatch()
    {
        var mixedLogs = new[]
        {
            @"[INFO] {""correlation_id"":""a1"",""tenant_id"":""t1"",""command_type"":""CreateOpportunityCommand""}",  // limpo
            @"[ERROR] Failed processing contact_name=""Ana Souza"" email=ana.souza@empresa.com",                      // sujo — PII
            @"[INFO] {""correlation_id"":""a3"",""opportunity_id"":""o1"",""action"":""Win""}",                       // limpo
        };

        var violations = PiiLogScanner.ScanLines(mixedLogs);

        violations.Should().HaveCount(1,
            "apenas a linha com contact_name e e-mail deve ser detectada como violação de PII.");

        violations[0].LineNumber.Should().Be(2);
    }

    // =========================================================================
    // PII-10 — Eventos de domínio não contêm campos PII (validação de payload)
    // =========================================================================

    [Fact(DisplayName = "PII-10: payloads de eventos de domínio simulados não contêm PII")]
    public void DomainEventPayloads_DoNotContainPii()
    {
        // Simula payloads dos 8 eventos de domínio (design §4.4, RNF 10.4)
        var eventPayloads = new[]
        {
            @"{""event_type"":""opportunity.created.v1"",""opportunity_id"":""o1"",""opportunity_number"":""AZ-0001"",""tenant_id"":""t1"",""bu_id"":""b1"",""account_id"":""a1"",""owner_id"":""u1"",""stage_id"":""s1"",""origin_channel_id"":""c1"",""actor_id"":""u1"",""occurred_at"":""2026-06-14T00:00:00Z""}",
            @"{""event_type"":""opportunity.stage_changed.v1"",""opportunity_id"":""o1"",""from_stage_id"":""s1"",""to_stage_id"":""s2"",""from_category"":""open"",""to_category"":""open"",""actor_id"":""u1"",""occurred_at"":""2026-06-14T01:00:00Z""}",
            @"{""event_type"":""opportunity.won.v1"",""opportunity_id"":""o1"",""valor_total"":700000,""closed_at"":""2026-06-14T10:00:00Z"",""actor_id"":""u1""}",
            @"{""event_type"":""opportunity.lost.v1"",""opportunity_id"":""o2"",""loss_reason_id"":""lr1"",""closed_at"":""2026-06-14T11:00:00Z"",""actor_id"":""u1""}",
            @"{""event_type"":""opportunity.stale.v1"",""opportunity_id"":""o3"",""bu_id"":""b1"",""last_activity_at"":""2026-05-30T00:00:00Z"",""detected_at"":""2026-06-14T00:00:00Z"",""detection_period"":""2026-06-14""}",
            @"{""event_type"":""opportunity.reopened.v1"",""opportunity_id"":""o1"",""previous_category"":""won"",""actor_id"":""u1"",""occurred_at"":""2026-06-14T12:00:00Z""}",
            @"{""event_type"":""commission.calculated.v1"",""opportunity_id"":""o1"",""partner_id"":""p1"",""comissao_total"":35000,""is_snapshot"":false}",
            @"{""event_type"":""commission.snapshot_created.v1"",""opportunity_id"":""o1"",""partner_id"":""p1"",""commission_id"":""ci1"",""comissao_total"":35000,""snapshot_at"":""2026-06-14T10:00:00Z""}",
        };

        var violations = PiiLogScanner.ScanLines(eventPayloads);

        violations.Should().BeEmpty(
            "payloads de eventos de domínio não devem conter PII " +
            "(design §4.4, RNF 10.4, Req 20.3, LGPD, TASK-24).");
    }
}
