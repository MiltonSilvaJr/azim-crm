#!/usr/bin/env bash
# =============================================================================
# pii-scan.sh — Gate de CI: detecta padrões de PII em logs do módulo audit-log
# design §13 (RNF-002.2), TASK-19
#
# Uso:
#   ./scripts/pii-scan.sh <arquivo-de-log>          # escaneia arquivo específico
#   ./scripts/pii-scan.sh --stdin                   # lê de stdin
#   ./scripts/pii-scan.sh --sample-pass             # imprime amostra sem PII e sai 0
#   ./scripts/pii-scan.sh --sample-fail             # imprime amostra com PII e sai 1
#
# Códigos de saída:
#   0 — nenhum padrão de PII detectado (CI verde)
#   1 — padrão de PII detectado (CI falha)
#   2 — erro de uso (argumento inválido ou arquivo não encontrado)
#
# Padrões detectados (RNF-002.2):
#   E-mail   : RFC 5322 simplificado — local@domínio.tld
#   Telefone : formato brasileiro — (XX) NNNNN-NNNN, +55XXXXXXXXXXX, 11 dígitos, etc.
#
# Exclusões intencionais:
#   - Linhas com "MASKED" (mascaramento já aplicado — não é vazamento)
#   - Linhas que iniciam com "#" (comentários neste próprio script em testes)
# =============================================================================

set -euo pipefail

# ---------------------------------------------------------------------------
# Padrões de detecção de PII
# ---------------------------------------------------------------------------

# E-mail — RFC 5322 simplificado: local@domínio.tld
# Cobre: usuario@empresa.com, user.name+tag@sub.domain.com.br, etc.
EMAIL_PATTERN='[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}'

# Telefone brasileiro — cobre formatos comuns com âncoras para evitar falso-positivo em UUIDs:
#   +55 11 99999-9999  /  +5511999999999  (deve começar com +55)
#   (11) 99999-9999    /  (11) 9999-9999  (deve começar com parêntese de DDD)
# Não cobre sequências de dígitos sem contexto explícito — evita UUIDs e timestamps.
PHONE_PATTERN='(\+55[\s\-]?[0-9]{2}[\s\-]?[0-9]{4,5}[\s\-]?[0-9]{4}|\([0-9]{2}\)[\s\-]?[0-9]{4,5}[\s\-]?[0-9]{4})'

# ---------------------------------------------------------------------------
# Amostras de teste embutidas
# ---------------------------------------------------------------------------

SAMPLE_PASS="$(cat <<'LOGEOF'
2026-06-12T10:00:00Z [INF] AuditService: persistência concluída. EntityType=Contact EntityId=00000000-1111-0000-0000-000000000001 Action=create
2026-06-12T10:00:01Z [INF] TenantContextBehavior: tenant validado. TenantId=AAAAAAAA-0000-0000-0000-000000000001
2026-06-12T10:00:02Z [INF] Consulta processada. EntityType=Opportunity EntityId=BBBBBBBB-0000-0000-0000-000000000002 Page=1 PageSize=50
2026-06-12T10:00:03Z [WRN] AuthorizationBehavior: papel insuficiente. Role=Vendedor ErrorCode=AUD-ERR-002
2026-06-12T10:00:04Z [INF] delta_json={"email":{"before":"[MASKED]","after":"[MASKED]"},"phone":{"before":"[MASKED]","after":"[MASKED]"}}
LOGEOF
)"

SAMPLE_FAIL="$(cat <<'LOGEOF'
2026-06-12T10:00:00Z [INF] AuditService: persistência concluída. EntityType=Contact EntityId=00000000-1111-0000-0000-000000000001 Action=create
2026-06-12T10:00:01Z [ERR] AuditService: falha ao processar. user_email=usuario@empresa.com.br EntityType=Contact
2026-06-12T10:00:02Z [INF] TenantContextBehavior: tenant validado. TenantId=AAAAAAAA-0000-0000-0000-000000000001
2026-06-12T10:00:03Z [DBG] delta_json={"phone":{"before":"(11) 98765-4321","after":"(11) 91234-5678"}}
LOGEOF
)"

# ---------------------------------------------------------------------------
# Funções
# ---------------------------------------------------------------------------

usage() {
    echo "Uso: $0 <arquivo-de-log>" >&2
    echo "     $0 --stdin" >&2
    echo "     $0 --sample-pass" >&2
    echo "     $0 --sample-fail" >&2
    exit 2
}

scan_content() {
    local content="$1"
    local found=0

    # Remove linhas que já contêm [MASKED] — mascaramento aplicado corretamente
    local filtered
    filtered="$(echo "$content" | grep -v '\[MASKED\]' || true)"

    if [ -z "$filtered" ]; then
        return 0
    fi

    # Busca e-mail
    local email_hits
    email_hits="$(echo "$filtered" | grep -oE "$EMAIL_PATTERN" || true)"
    if [ -n "$email_hits" ]; then
        echo "[PII-SCAN] FALHA: padrão de e-mail detectado nos logs:" >&2
        echo "$email_hits" | head -5 | sed 's/./*/g' | while read -r line; do
            echo "  (redacted: ${#line} chars)" >&2
        done
        # Mostra contagem sem expor o valor real
        local count
        count="$(echo "$email_hits" | wc -l | tr -d ' ')"
        echo "[PII-SCAN] E-mails detectados: $count ocorrência(s)" >&2
        found=1
    fi

    # Busca telefone brasileiro
    local phone_hits
    phone_hits="$(echo "$filtered" | grep -oE "$PHONE_PATTERN" || true)"
    if [ -n "$phone_hits" ]; then
        echo "[PII-SCAN] FALHA: padrão de telefone brasileiro detectado nos logs:" >&2
        local count
        count="$(echo "$phone_hits" | wc -l | tr -d ' ')"
        echo "[PII-SCAN] Telefones detectados: $count ocorrência(s)" >&2
        found=1
    fi

    return $found
}

# ---------------------------------------------------------------------------
# Entrypoint
# ---------------------------------------------------------------------------

if [ $# -eq 0 ]; then
    usage
fi

case "$1" in
    --sample-pass)
        echo "[PII-SCAN] Executando com amostra limpa (sem PII)..."
        if scan_content "$SAMPLE_PASS"; then
            echo "[PII-SCAN] OK — nenhum padrão de PII detectado na amostra limpa."
            exit 0
        else
            echo "[PII-SCAN] ERRO INESPERADO: PII detectada na amostra limpa. Verifique os padrões." >&2
            exit 1
        fi
        ;;

    --sample-fail)
        echo "[PII-SCAN] Executando com amostra contendo PII (deve falhar)..."
        if scan_content "$SAMPLE_FAIL"; then
            echo "[PII-SCAN] ERRO: amostra com PII não foi detectada. Script com defeito." >&2
            exit 1
        else
            echo "[PII-SCAN] Correto — PII detectada na amostra de teste (comportamento esperado)."
            exit 1
        fi
        ;;

    --stdin)
        content="$(cat)"
        if scan_content "$content"; then
            echo "[PII-SCAN] OK — nenhum padrão de PII detectado nos logs."
            exit 0
        else
            echo "[PII-SCAN] FALHA — logs contêm padrões de PII. Verifique o Serilog destructuring." >&2
            exit 1
        fi
        ;;

    -*)
        echo "[PII-SCAN] Opção desconhecida: $1" >&2
        usage
        ;;

    *)
        log_file="$1"
        if [ ! -f "$log_file" ]; then
            echo "[PII-SCAN] Erro: arquivo não encontrado: $log_file" >&2
            exit 2
        fi
        content="$(cat "$log_file")"
        if scan_content "$content"; then
            echo "[PII-SCAN] OK — nenhum padrão de PII detectado em: $log_file"
            exit 0
        else
            echo "[PII-SCAN] FALHA — PII detectada em: $log_file" >&2
            exit 1
        fi
        ;;
esac
