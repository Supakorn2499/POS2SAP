// reset_and_seed_configs.js
// Clears global sap_url_* and seeds per-interface values + api key
const http = require('http');
const host = 'localhost';
const port = 5163;

const actions = [
  { key: 'sap_url_test', value: '' },
  { key: 'sap_url_prod', value: '' },
  { key: 'sap_auth_type', value: 'ApiKey' },
  { key: 'sap_api_key', value: 'lX4jk32jySiEqejyDV13xnKzXH9E1xHV' },
  { key: 'sap_env', value: 'TST' },
  { key: 'ARInvoice.sap_url_test', value: 'http://203.151.56.10/uat/api/ARInvoice' },
  { key: 'IncomingPayment.sap_url_test', value: 'http://203.151.56.10/uat/api/IncomingPayment' },
  { key: 'Delivery.sap_url_test', value: 'http://203.151.56.10/uat/api/delivery' }
];

function request(options, body) {
  return new Promise((resolve, reject) => {
    const req = http.request(options, (res) => {
      let data = '';
      res.on('data', (chunk) => data += chunk);
      res.on('end', () => {
        try { resolve({ status: res.statusCode, body: JSON.parse(data || '{}') }); }
        catch (e) { resolve({ status: res.statusCode, body: data }); }
      });
    });
    req.on('error', reject);
    if (body) req.write(body);
    req.end();
  });
}

(async () => {
  console.log('Resetting and seeding configs to http://'+host+':'+port);
  for (const a of actions) {
    try {
      const payload = JSON.stringify({ configValue: a.value });
      const res = await request({ hostname: host, port, path: `/api/config/${encodeURIComponent(a.key)}`, method: 'PUT', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(payload) } }, payload);
      console.log(`${a.key} -> ${res.status}`, res.body?.message ?? '');
    } catch (err) {
      console.error(`Error ${a.key}:`, err);
    }
  }
  console.log('Done.');
})();
