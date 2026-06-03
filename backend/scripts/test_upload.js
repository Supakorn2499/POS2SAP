const http = require('http');

const data = JSON.stringify({
  Head: {
    DocNum: 'RC-P01042026/00020',
    DocDate: '20260401',
    PymntGroup: 'Cash',
    DocDueDate: '20260401',
    POSID: '128',
    CardCode: '29847',
    CardName: 'Manasvinee S',
    BranchCode: '00007',
    BranchName: 'แฟมไทม์ สเต็กแอนด์พาสด้า 00007',
    VatSum: 53.61,
    DocTotal: 819.5
  },
  Lines: [
    {
      DocNum: 'RC-P01042026/00020',
      LineNum: 0,
      ItemCode: 'F1_3',
      Dscription: 'Mixed fried',
      Quantity: 1,
      PriceBefDi: 95,
      Price: 95,
      PriceAfVat: 101.65,
      VatSum: 6.65,
      LineTotal: 95,
      GTotal: 101.65
    }
  ]
});
function post(path, payload, token) {
  return new Promise((resolve, reject) => {
    const body = typeof payload === 'string' ? payload : JSON.stringify(payload);
    const opts = {
      hostname: 'localhost',
      port: 5163,
      path,
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(body)
      }
    };
    if (token) opts.headers['Authorization'] = 'Bearer ' + token;

    const req = http.request(opts, (res) => {
      let body = '';
      res.on('data', (chunk) => body += chunk);
      res.on('end', () => resolve({ status: res.statusCode, body }));
    });
    req.on('error', (e) => reject(e));
    req.write(body);
    req.end();
  });
}

// Direct upload without auth (endpoint added to public routes for testing)
(async () => {
  try {
    const uploadResp = await post('/api/interface/upload', data, null);
    console.log('Upload status:', uploadResp.status);
    try { console.log('Upload response:', JSON.parse(uploadResp.body)); } catch (e) { console.log('Upload text:', uploadResp.body); }
  } catch (e) {
    console.error('Error', e);
  }
})();
