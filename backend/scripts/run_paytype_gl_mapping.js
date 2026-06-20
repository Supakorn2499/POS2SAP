// run_paytype_gl_mapping.js — runs paytype_gl_mapping.sql on HQ_FAMTIME
const sql  = require('mssql');
const fs   = require('fs');
const path = require('path');

const config = {
  server: '203.151.92.185', port: 1444, database: 'HQ_FAMTIME',
  user: 'vtecPOS', password: 'vtecpwnet',
  options: { trustServerCertificate: true, connectTimeout: 30000, requestTimeout: 60000 }
};

async function run() {
  const pool = await sql.connect(config);
  console.log('Connected to HQ_FAMTIME');

  const sqlFile = path.join(__dirname, '..', 'POS2SAP.API', 'sql', 'paytype_gl_mapping.sql');
  const sqlText = fs.readFileSync(sqlFile, 'utf8');

  // Split on GO statements (T-SQL batch separator) — none in this file, but handle anyway
  const batches = sqlText.split(/^\s*GO\s*$/im).filter(b => b.trim().length > 0);

  for (const batch of batches) {
    const result = await pool.request().query(batch);
    if (result.recordset) {
      result.recordset.forEach(r => console.log(r));
    }
  }

  // Verify: show what was inserted
  console.log('\n===== paytype_gl_mapping rows =====');
  const v = await pool.request().query(`
    SELECT PayTypeID, PayTypeName, SapPayCategory, SapGlAccount, SapPayTypeName, IsActive
    FROM paytype_gl_mapping
    ORDER BY SortOrder, PayTypeID
  `);
  v.recordset.forEach(r =>
    console.log(
      `  [${String(r.PayTypeID).padStart(3)}] ${String(r.PayTypeName).padEnd(40)} ` +
      `cat=${String(r.SapPayCategory).padEnd(12)} gl=${String(r.SapGlAccount||'NULL').padEnd(15)} ` +
      `sapName=${r.SapPayTypeName || ''}`
    )
  );

  console.log('\n===== interface_configs IncomingPayment.* =====');
  const c = await pool.request().query(`
    SELECT config_key, config_value, is_active
    FROM interface_configs
    WHERE config_key LIKE 'IncomingPayment.%'
    ORDER BY config_key
  `);
  c.recordset.forEach(r =>
    console.log(`  ${String(r.config_key).padEnd(40)} = ${r.config_value}`)
  );

  await pool.close();
  console.log('\nDone.');
}

run().catch(e => { console.error('ERROR:', e.message); process.exit(1); });
