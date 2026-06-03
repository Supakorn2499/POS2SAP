// seed_default_configs.js
// Usage: node seed_default_configs.js

const http = require('http');

const host = 'localhost';
const port = 5163;

const configs = [
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
  console.log('Seeding default configs to http://'+host+':'+port);
  for (const c of configs) {
    try {
      const payload = JSON.stringify({ configValue: c.value });
      const res = await request({ hostname: host, port, path: `/api/config/${encodeURIComponent(c.key)}`, method: 'PUT', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(payload) } }, payload);
      console.log(`${c.key} -> ${res.status}`, res.body?.message ?? '');
    } catch (err) {
      console.error(`Error seeding ${c.key}:`, err);
    }
  }
  console.log('Done.');
})();
