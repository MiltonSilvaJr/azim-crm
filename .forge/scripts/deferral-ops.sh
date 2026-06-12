#!/usr/bin/env bash
# deferral-ops.sh — operações deterministas em deferrals.json (§17.4).
# Uso:
#   deferral-ops.sh raise   <change-id> --reason "<texto>" [--blocks "<item,...>"]
#   deferral-ops.sh resolve <change-id> <deferral-id> --note "<texto>"
#   deferral-ops.sh test    <change-id> <deferral-id>
#   deferral-ops.sh status  <change-id>   # one-line: OK (3 resolved/tested, 0 open)
set -euo pipefail

FORGE_ROOT="${FORGE_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
ACTIVE="$FORGE_ROOT/.forge/specs/active"

cmd="${1:-}"; shift || true
change_id="${1:-}"; shift || true

[ -n "$cmd" ] && [ -n "$change_id" ] || {
  echo "Usage: deferral-ops.sh raise|resolve|test|status <change-id> [args...]" >&2; exit 1
}

spec_dir="$ACTIVE/$change_id"
df="$spec_dir/deferrals.json"
progress_file="$spec_dir/progress.json"

_now() { date -u +"%Y-%m-%dT%H:%M:%SZ"; }
_write_json() { local f="$1"; local tmp; tmp="$(mktemp "${f}.XXXXXX")"; printf '%s\n' "$2" > "$tmp"; mv "$tmp" "$f"; }

_init_deferrals() {
  [ -f "$df" ] || _write_json "$df" "{\"change_id\":\"$change_id\",\"deferrals\":[]}"
}

_update_open_count() {
  [ -f "$progress_file" ] || return 0
  node -e "
    const pf='$progress_file', df='$df';
    const p=JSON.parse(require('fs').readFileSync(pf,'utf8'));
    const d=JSON.parse(require('fs').readFileSync(df,'utf8'));
    p.open_deferrals=d.deferrals.filter(x=>x.status==='open').length;
    p.updated_at='$(_now)';
    require('fs').writeFileSync(pf,JSON.stringify(p,null,2));
  "
}

case "$cmd" in

raise)
  reason=""
  blocks=()
  while [ $# -gt 0 ]; do
    case "$1" in
      --reason) reason="$2"; shift 2 ;;
      --blocks) IFS=',' read -ra blocks <<< "$2"; shift 2 ;;
      *) shift ;;
    esac
  done
  [ -n "$reason" ] || { echo "FAIL: --reason obrigatório" >&2; exit 1; }
  _init_deferrals
  result="$(node - "$df" "$reason" "$(_now)" "${blocks[*]:-}" <<'NODEEOF'
const { readFileSync } = require('fs');
const [, , df, reason, now, blocksRaw] = process.argv;
const data = JSON.parse(readFileSync(df, 'utf8'));
const blocks = blocksRaw ? blocksRaw.split(' ').filter(Boolean) : [];
const n = data.deferrals.length + 1;
const id = 'DEFER-' + String(n).padStart(2, '0');
data.deferrals.push({ id, raised_in: now, description: reason, reason, blocks, depends_on: [], status: 'open', resolved_at: null, resolution_note: null });
console.log(JSON.stringify(data, null, 2));
NODEEOF
)"
  _write_json "$df" "$result"
  _update_open_count
  new_id="$(node -e "const d=JSON.parse(require('fs').readFileSync('$df','utf8')); console.log(d.deferrals[d.deferrals.length-1].id)")"
  echo "OK raise — $new_id registrado"
  ;;

resolve)
  deferral_id="${1:-}"; shift || true
  [ -n "$deferral_id" ] || { echo "FAIL: deferral-id obrigatório" >&2; exit 1; }
  note=""
  while [ $# -gt 0 ]; do case "$1" in --note) note="$2"; shift 2 ;; *) shift ;; esac; done
  [ -n "$note" ] || { echo "FAIL: --note obrigatório" >&2; exit 1; }
  _init_deferrals
  result="$(node - "$df" "$deferral_id" "$note" "$(_now)" <<'NODEEOF'
const { readFileSync } = require('fs');
const [, , df, defId, note, now] = process.argv;
const data = JSON.parse(readFileSync(df, 'utf8'));
const d = data.deferrals.find(x => x.id === defId);
if (!d) { console.error('deferral ' + defId + ' não encontrado'); process.exit(1); }
if (d.status !== 'open') { console.error('deferral ' + defId + ' não está open (status: ' + d.status + ')'); process.exit(1); }
d.status = 'resolved';
d.resolved_at = now;
d.resolution_note = note;
console.log(JSON.stringify(data, null, 2));
NODEEOF
)"
  _write_json "$df" "$result"
  _update_open_count
  echo "OK resolve — $deferral_id marcado como resolved"
  ;;

test)
  deferral_id="${1:-}"; [ -n "$deferral_id" ] || { echo "FAIL: deferral-id obrigatório" >&2; exit 1; }
  _init_deferrals
  result="$(node - "$df" "$deferral_id" <<'NODEEOF'
const { readFileSync } = require('fs');
const [, , df, defId] = process.argv;
const data = JSON.parse(readFileSync(df, 'utf8'));
const d = data.deferrals.find(x => x.id === defId);
if (!d) { console.error('deferral ' + defId + ' não encontrado'); process.exit(1); }
if (d.status !== 'resolved') { console.error('deferral ' + defId + ' deve estar resolved antes de testar (status: ' + d.status + ')'); process.exit(1); }
d.status = 'tested';
console.log(JSON.stringify(data, null, 2));
NODEEOF
)"
  _write_json "$df" "$result"
  _update_open_count
  echo "OK test — $deferral_id marcado como tested"
  ;;

status)
  _init_deferrals
  node - "$df" <<'NODEEOF'
const { readFileSync } = require('fs');
const [, , df] = process.argv;
const data = JSON.parse(readFileSync(df, 'utf8'));
const open = data.deferrals.filter(d => d.status === 'open').length;
const resolved = data.deferrals.filter(d => d.status === 'resolved').length;
const tested = data.deferrals.filter(d => d.status === 'tested').length;
const total = data.deferrals.length;
if (open > 0) {
  const ids = data.deferrals.filter(d => d.status === 'open').map(d => d.id).join(', ');
  console.log('OPEN (' + open + '/' + total + ' open: ' + ids + ')');
} else {
  console.log('OK (' + tested + ' tested, ' + resolved + ' resolved, 0 open)');
}
NODEEOF
  ;;

*)
  echo "FAIL: comando desconhecido '$cmd'" >&2; exit 1
  ;;
esac
