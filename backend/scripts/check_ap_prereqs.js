const sql = require('mssql');
const fs = require('fs');
const cfg = JSON.parse(fs.readFileSync('../POS2SAP.API/appsettings.Development.json'));
const cs = cfg.ConnectionStrings.DefaultConnection;
const match = cs.match(/Server=([^;,]+),?(\d+)?.*?Database=([^;]+).*?User Id=([^;]+).*?Password=([^;]+)/i);
const config = {
  server: match[1], port: parseInt(match[2]||1433),
  database: match[3].trim(), user: match[4].trim(), password: match[5].trim(),
  options: { trustServerCertificate: true }
};

sql.connect(config).then(pool => pool.query(`
  SELECT 'paytype_gl_mapping rows' as check_item, CAST(COUNT(*) as varchar) as result FROM paytype_gl_mapping
  UNION ALL
  SELECT 'GL-PENDING (not yet set)', CAST(COUNT(*) as varchar) FROM paytype_gl_mapping WHERE SapGlAccount = '[GL-PENDING]'
  UNION ALL
  SELECT 'IncomingPayment configs active', CAST(COUNT(*) as varchar) FROM interface_configs WHERE config_key LIKE 'IncomingPayment.%' AND is_active=1
  UNION ALL
  SELECT 'sap_url_test value', config_value FROM interface_configs WHERE config_key = 'IncomingPayment.sap_url_test' AND is_active=1
  UNION ALL
  SELECT 'sap_api_key set', CASE WHEN config_value IS NOT NULL AND config_value != '' THEN 'YES' ELSE 'NO' END FROM interface_configs WHERE config_key = 'IncomingPayment.sap_api_key' AND is_active=1
`)).then(r => {
  r.recordset.forEach(row => console.log(row.check_item + ':', row.result));
  process.exit(0);
}).catch(e => { console.error(e.message); process.exit(1); });
