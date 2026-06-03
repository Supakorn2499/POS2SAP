const http = require('http');

function request(options, body) {
  return new Promise((resolve, reject) => {
    const req = http.request(options, (res) => {
      let data = '';
      res.on('data', (c) => data += c);
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
    const loginPayload = JSON.stringify({ StaffLogin: 'ciuser', StaffPassword: 'Test@1234' });
    const login = await request({ ...base, path: '/api/auth/login', method: 'POST', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(loginPayload) } }, loginPayload);
    const token = login.body?.data?.accessToken ?? login.body?.data?.AccessToken;
    if (!token) { console.error('Login failed', login); process.exit(1); }

    const get = await request({ ...base, path: '/api/monitor/logs?interfaceType=ARInvoice&page=1&pageSize=20', method: 'GET', headers: { 'Authorization': `Bearer ${token}` } });
    console.log('status', get.status);
    console.log('body.items.length =', (get.body?.data?.items || []).length);
    console.log(JSON.stringify(get.body, null, 2));
  } catch (err) {
    console.error(err);
  }
})();
