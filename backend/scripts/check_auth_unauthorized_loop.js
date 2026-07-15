/**
 * Regression: JWT unauthorized symptom + refresh recovery shape.
 * Run: node backend/scripts/check_auth_unauthorized_loop.js
 *
 * Asserts:
 * 1) Invalid/missing JWT → HTTP 401 { message: "Unauthorized" }  (user toast text)
 * 2) /api/app-logs requires auth
 * 3) /api/auth/refresh rejects empty body (endpoint exists)
 */
const http = require('http');

function req(method, path, body, token) {
  return new Promise((resolve, reject) => {
    const data = body ? JSON.stringify(body) : null;
    const r = http.request(
      {
        hostname: 'localhost',
        port: 5163,
        path,
        method,
        headers: {
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
          ...(data
            ? { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(data) }
            : {}),
        },
      },
      (res) => {
        let b = '';
        res.on('data', (c) => (b += c));
        res.on('end', () => {
          let parsed = b;
          try {
            parsed = JSON.parse(b || '{}');
          } catch {
            /* keep raw */
          }
          resolve({ status: res.statusCode, body: parsed });
        });
      }
    );
    r.on('error', reject);
    if (data) r.write(data);
    r.end();
  });
}

function assert(cond, msg) {
  if (!cond) {
    console.error('FAIL:', msg);
    process.exit(1);
  }
}

(async () => {
  const bad = await req('GET', '/api/monitor/branches', null, 'not-a-valid-jwt');
  const none = await req('GET', '/api/monitor/branches');
  const listLogs = await req('GET', '/api/app-logs', null, 'not-a-valid-jwt');
  const refreshBad = await req('POST', '/api/auth/refresh', { refreshToken: '' });

  assert(bad.status === 401, `expected 401 with bad token, got ${bad.status}`);
  assert(
    String(bad.body?.message || '').toLowerCase().includes('unauthorized'),
    `expected Unauthorized message, got ${JSON.stringify(bad.body)}`
  );
  assert(none.status === 401, `expected 401 without token, got ${none.status}`);
  assert(listLogs.status === 401, `expected app-logs 401, got ${listLogs.status}`);
  assert(
    refreshBad.status === 400 || refreshBad.status === 401,
    `expected refresh validation 400/401, got ${refreshBad.status}`
  );

  console.log('OK: auth unauthorized loop + refresh endpoint reachable');
  console.log('  bad token →', bad.status, bad.body);
  console.log('  app-logs  →', listLogs.status);
  console.log('  refresh   →', refreshBad.status, refreshBad.body?.message || '');
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
