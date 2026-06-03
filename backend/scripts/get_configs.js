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
    const loginPayload = JSON.stringify({ StaffLogin: 'ciuser', StaffPassword: 'Test@1234' });
    const login = await request({ hostname: 'localhost', port: 5163, path: '/api/auth/login', method: 'POST', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(loginPayload) } }, loginPayload);
    const token = login.body?.data?.accessToken ?? login.body?.data?.AccessToken;
    if (!token) { console.error('Login failed'); process.exit(1); }

    const get = await request({ hostname: 'localhost', port: 5163, path: '/api/config', method: 'GET', headers: { 'Authorization': `Bearer ${token}` } });
    const rows = get.body?.data || [];
    const keys = ['ARInvoice.AR','IncomingPayment.IC'];
    const found = rows.filter(r => keys.includes(r.configKey));
    console.log('Found rows:', JSON.stringify(found, null, 2));
  } catch (err) {
    console.error('ERROR', err);
    process.exit(1);
  }
})();
