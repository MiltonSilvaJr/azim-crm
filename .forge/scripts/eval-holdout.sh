#!/usr/bin/env bash
# eval-holdout.sh — seleção de description por holdout train/test (§17.8.3).
# Anti-overfitting: split 60/40 determinista dos casos, pontua cada candidata em
# train e test SEPARADAMENTE e seleciona pela pontuação de TESTE (nunca de treino).
# A seleção é determinista (sem aleatoriedade) — o split por ordenação estável dos ids.
# Uso:
#   eval-holdout.sh <candidates.json>
# Formato de candidates.json:
#   {
#     "skill": "verify-build",
#     "cases": ["TC-01","TC-02","TC-03","TC-04","TC-05"],
#     "candidates": [
#       { "id": "c0", "description": "...", "scores": { "TC-01": 1, "TC-02": 0, ... } }
#     ]
#   }
# Grava <dir>/holdout.json ao lado do input e imprime uma linha com a vencedora.
set -euo pipefail

cand_file="${1:-}"
if [ -z "$cand_file" ] || [ ! -f "$cand_file" ]; then
  echo "Usage: eval-holdout.sh <candidates.json>" >&2
  exit 1
fi

node - "$cand_file" <<'NODEEOF'
const { readFileSync, writeFileSync } = require('fs');
const { dirname, join } = require('path');

const file = process.argv[2];
const data = JSON.parse(readFileSync(file, 'utf8'));

if (!Array.isArray(data.cases) || data.cases.length < 2) {
  console.error('FAIL: precisa de >=2 casos para holdout'); process.exit(1);
}
if (!Array.isArray(data.candidates) || data.candidates.length < 1) {
  console.error('FAIL: precisa de >=1 candidata'); process.exit(1);
}

// Split 60/40 determinista: ordena ids e fatia. floor(0.6*n) p/ train, resto p/ test.
const cases = [...data.cases].sort();
const nTrain = Math.max(1, Math.floor(cases.length * 0.6));
const train = cases.slice(0, nTrain);
const test = cases.slice(nTrain);
if (test.length === 0) { console.error('FAIL: test split vazio'); process.exit(1); }

const LIMIT = 1024; // limite de chars da description (spec Agent Skills)
const meanOver = (scores, ids) => {
  const vals = ids.map(id => scores[id]).filter(v => typeof v === 'number');
  return vals.length ? vals.reduce((a, b) => a + b, 0) / vals.length : 0;
};
const round = x => Number(x.toFixed(4));

const scored = data.candidates.map(c => {
  if (typeof c.description === 'string' && c.description.length > LIMIT) {
    console.error(`FAIL: candidata ${c.id} excede ${LIMIT} chars (${c.description.length})`);
    process.exit(1);
  }
  return {
    id: c.id,
    description: c.description,
    train_score: round(meanOver(c.scores || {}, train)),
    test_score: round(meanOver(c.scores || {}, test))
  };
});

// Seleciona pela pontuação de TESTE. Empate → maior train_score; depois ordem estável.
const winner = [...scored].sort((a, b) =>
  (b.test_score - a.test_score) || (b.train_score - a.train_score)
)[0];

const out = {
  skill: data.skill,
  split: { train, test, ratio: '60/40' },
  selection_metric: 'test_score',
  candidates: scored,
  winner: { id: winner.id, train_score: winner.train_score, test_score: winner.test_score, description: winner.description }
};

writeFileSync(join(dirname(file), 'holdout.json'), JSON.stringify(out, null, 2) + '\n');
console.log(
  `OK holdout: split ${train.length}/${test.length}; winner ${winner.id} ` +
  `(test=${winner.test_score}, train=${winner.train_score}) selecionada por test_score`
);
NODEEOF
