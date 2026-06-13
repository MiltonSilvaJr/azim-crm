# Teste de Performance — Módulo Authentication

**Mapeia:** TASK-25, RNF 2.1, design.md § 11, § 19.

---

## 1. Objetivo

Verificar que as operações de autenticação do backend atendem ao SLO de latência definido em RNF 2.1:

> **p95 ≤ 1 segundo** nas operações de autenticação do backend, excluindo latência do IdP.

---

## 2. Baseline de Latência via Métrica

O módulo emite a métrica `auth_latency_ms` (histograma) via `.NET Meter` API (ver `src/Authentication.Infrastructure/Metrics/AuthMetrics.cs`).

- Nome da métrica: `auth_latency_ms`
- Exportada via OpenTelemetry Collector para GCP Cloud Monitoring.
- Alert configurado: `auth_latency_p95_slo_breach` em `observability/alerts.yaml`.
  - Dispara quando `p95 > 1000 ms` sustentado por 5 minutos.

O alert funciona como gate operacional contínuo de latência em produção e staging.

---

## 3. Componentes de Latência

| Componente | Latência estimada | Observação |
|---|---|---|
| Verificação de token (IDP) | Varia (excluído do SLO) | Delegado ao Firebase Admin SDK; latência do GCP |
| Consulta ao Redis (cache) | < 5 ms | Cache hit de memberships |
| Consulta ao PostgreSQL (cache miss) | 5–20 ms | OrganizationUserDirectory |
| Processamento no middleware | < 5 ms | SessionTokenValidator + AuthContextComposer |
| **Total backend (excl. IdP)** | **< 30 ms** | Estimativa em condições normais |

---

## 4. Teste de Carga (Pendente de Infraestrutura)

### 4.1 Status

O teste de carga com ferramenta dedicada (k6 ou NBomber) **não foi executado** nesta entrega porque não há infraestrutura de load test disponível no ambiente de desenvolvimento (worktree local).

### 4.2 Critério de Aceite

O gate de performance é considerado atendido quando:

1. A métrica `auth_latency_ms` (p95) permanece ≤ 1000 ms em produção/staging por, no mínimo, 5 minutos de carga representativa.
2. O alert `auth_latency_p95_slo_breach` não dispara em condições normais de operação.

### 4.3 Script de Referência (k6)

```javascript
// scripts/load-test/auth-latency.js
// Referência: RNF 2.1, TASK-25
// Executar: k6 run --env BASE_URL=https://staging.azim-crm.com scripts/load-test/auth-latency.js

import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 50 },  // ramp-up
    { duration: '2m',  target: 50 },  // carga sustentada
    { duration: '30s', target: 0 },   // ramp-down
  ],
  thresholds: {
    // SLO: p95 ≤ 1 s nas operações de backend (excl. IdP)
    'http_req_duration{endpoint:me}': ['p(95)<1000'],
    'http_req_failed': ['rate<0.01'],
  },
};

export default function () {
  const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
  const TOKEN = __ENV.TEST_TOKEN || 'test-jwt-token';

  const res = http.get(`${BASE_URL}/v1/auth/me`, {
    headers: {
      'Authorization': `Bearer ${TOKEN}`,
      'X-Tenant-Slug': 'test-tenant',
    },
    tags: { endpoint: 'me' },
  });

  check(res, {
    'status é 200': (r) => r.status === 200,
    'latência < 1000ms': (r) => r.timings.duration < 1000,
  });

  sleep(0.1);
}
```

### 4.4 Pendências de Go-Live

Antes do go-live, confirmar:

- [ ] Script k6 executado em ambiente de staging com carga real.
- [ ] Métrica `auth_latency_ms` exportada para Cloud Monitoring verificada.
- [ ] Alert `auth_latency_p95_slo_breach` validado em teste de regressão.
- [ ] VAL-AUTH-02 (TTL de sessão) confirmado com produto/segurança.
- [ ] VAL-09 (autenticação M2M) decidido.

---

## 5. Referências

- `src/Authentication.Infrastructure/Metrics/AuthMetrics.cs` — emissão de `auth_latency_ms`
- `observability/alerts.yaml` — alert `auth_latency_p95_slo_breach`
- `docs/product/modules/authentication/design.md` — RNF 2.1, § 11, § 19
- `docs/product/modules/authentication/requirements.md` — RNF 2
