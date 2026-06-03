const http = require('http');

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
  try {
    const base = { hostname: 'localhost', port: 5163 };
    console.log('Logging in as ciuser...');
    const loginPayload = JSON.stringify({ StaffLogin: 'ciuser', StaffPassword: 'Test@1234' });
    const login = await request({ ...base, path: '/api/auth/login', method: 'POST', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(loginPayload) } }, loginPayload);
    const token = login.body?.data?.accessToken ?? login.body?.data?.AccessToken;
    if (!token) { console.error('Login failed'); process.exit(1); }

    console.log('Fetching configs...');
    const get = await request({ ...base, path: '/api/config', method: 'GET', headers: { 'Authorization': `Bearer ${token}` } });
    const rows = get.body?.data || [];
    const globalKey = rows.find(r => r.configKey === 'sap_api_key');
    if (!globalKey) { console.log('No global sap_api_key found, nothing to migrate'); process.exit(0); }
    const globalValue = globalKey.configValue || '';
    if (!globalValue) { console.log('Global sap_api_key is empty, nothing to migrate'); process.exit(0); }

    const interfaces = ['ARInvoice','IncomingPayment','Delivery'];
    for (const iface of interfaces) {
      const target = `${iface}.sap_api_key`;
      console.log(`Setting ${target}...`);
      const payload = JSON.stringify({ configValue: globalValue });
      const put = await request({ ...base, path: `/api/config/${encodeURIComponent(target)}`, method: 'PUT', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(payload), 'Authorization': `Bearer ${token}` } }, payload);
      console.log(target, put.status, JSON.stringify(put.body));
    }

    console.log('Migration complete');
  } catch (err) {
    console.error('ERROR', err);
    process.exit(1);
  }
})();
