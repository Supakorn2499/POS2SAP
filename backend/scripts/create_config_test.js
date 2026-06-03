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
    const loginPayload = JSON.stringify({ StaffLogin: 'admin', StaffPassword: 'Password@123' });
    const login = await request({ hostname: 'localhost', port: 5163, path: '/api/auth/login', method: 'POST', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(loginPayload) } }, loginPayload);
    console.log('LOGIN RESPONSE:', JSON.stringify(login, null, 2));
    const token = login.body?.data?.accessToken ?? login.body?.data?.AccessToken;
    if (!token) { console.error('Login failed or token missing'); process.exit(1); }

    const key = 'ARInvoice.sap_url_test';
    const value = 'http://203.151.56.10/uat/api/ARInvoice';
    const putPayload = JSON.stringify({ configValue: value });
    const put = await request({ hostname: 'localhost', port: 5163, path: `/api/config/${encodeURIComponent(key)}`, method: 'PUT', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(putPayload), 'Authorization': `Bearer ${token}` } }, putPayload);
    console.log('PUT RESPONSE:', JSON.stringify(put, null, 2));

    const get = await request({ hostname: 'localhost', port: 5163, path: '/api/config', method: 'GET', headers: { 'Authorization': `Bearer ${token}` } });
    console.log('GET RESPONSE:', JSON.stringify(get, null, 2));

    const found = (get.body?.data || []).find(c => c.configKey === key || c.config_key === key);
    console.log('FOUND:', found || 'not found');
  } catch (err) {
    console.error('ERROR', err);
    process.exit(1);
  }
})();
