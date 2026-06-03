const http = require('http');

const host = 'localhost';
const port = 5163;

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
    console.log('Logging in as ciuser...');
    const loginPayload = JSON.stringify({ StaffLogin: 'ciuser', StaffPassword: 'Test@1234' });
    const login = await request({ hostname: host, port, path: '/api/auth/login', method: 'POST', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(loginPayload) } }, loginPayload);
    if (!login || !login.body || !login.body.data || !login.body.data.accessToken) {
      console.error('Login failed:', login);
      process.exit(1);
    }
    const token = login.body.data.accessToken;
    console.log('Login successful');

    const interfaces = ['ARInvoice', 'IncomingPayment', 'Delivery'];
    for (const iface of interfaces) {
      const payload = JSON.stringify({ interfaceType: iface });
      try {
        const res = await request({ hostname: host, port, path: '/api/config/test', method: 'POST', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(payload), 'Authorization': `Bearer ${token}` } }, payload);
        console.log(`== ${iface} ==`);
        console.log('status:', res.status);
        console.log('body:', JSON.stringify(res.body));
      } catch (err) {
        console.error(`Error testing ${iface}:`, err.message || err);
      }
    }
  } catch (err) {
    console.error('Fatal error:', err);
    process.exit(1);
  }
})();
