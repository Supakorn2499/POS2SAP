/**
 * Check DL interface_logs for One Bangkok / 2026-07-01
 */
const fs = require('fs');
const path = require('path');
const sql = require('mssql');

function parseConn(cs) {
  const get = (k) => { const m = cs.match(new RegExp(`${k}=([^;]+)`, 'i')); return m ? m[1] : ''; };
  const serverRaw = get('Server') || get('Data Source');
  let server = serverRaw, port = 1433;
  if (serverRaw.includes(',')) { const [h, p] = serverRaw.split(','); server = h; port = parseInt(p, 10) || 1433; }
  return {
    server, port,
    database: get('Database') || get('Initial Catalog'),
    user: get('User Id') || get('User ID') || get('Uid'),
    password: get('Password') || get('Pwd'),
    options: { trustServerCertificate: true, connectTimeout: 60000, requestTimeout: 120000 },
  };
}

async function main() {
  const apiDir = path.join(__dirname, '../POS2SAP.API');
  const cfgPath = [path.join(apiDir, 'appsettings.Development.json'), path.join(apiDir, 'appsettings.json')].find((p) => fs.existsSync(p));
  const pool = await sql.connect(parseConn(JSON.parse(fs.readFileSync(cfgPath, 'utf8')).ConnectionStrings.DefaultConnection));

  const shop = await pool.request().query(`
    SELECT ShopID, shopcode, PTTShopCode, SLOC, shopname, BranchName
    FROM shop_data
    WHERE shopname LIKE '%One Bangkok%' OR BranchName LIKE '%00005%' OR PTTShopCode LIKE '%FM006%' OR shopcode LIKE '%FM006%'
  `);
  console.log('=== One Bangkok shop_data ===');
  console.log(JSON.stringify(shop.recordset, null, 2));

  const logs = await pool.request().query(`
    SELECT TOP 30 id, pos_doc_no, pos_doc_date, branch_code, branch_name, interface_type, status, created_at
    FROM interface_logs
    WHERE interface_type = 'DL' AND is_deleted = 0
      AND (
        branch_name LIKE N'%One Bangkok%'
        OR branch_name LIKE N'%00005%'
        OR branch_code LIKE '%FM006%'
        OR branch_code LIKE '%CLBR%'
        OR pos_doc_date >= '2026-07-01' AND pos_doc_date < '2026-07-02'
      )
    ORDER BY created_at DESC
  `);
  console.log('\n=== Recent DL logs (related) ===');
  console.log('count=', logs.recordset.length);
  for (const r of logs.recordset) console.log(JSON.stringify(r));

  const allDl = await pool.request().query(`
    SELECT COUNT(*) AS cnt FROM interface_logs WHERE interface_type='DL' AND is_deleted=0
  `);
  console.log('\nTotal DL logs:', allDl.recordset[0].cnt);

  const byBranch = await pool.request().query(`
    SELECT TOP 20 branch_code, branch_name, COUNT(*) AS cnt
    FROM interface_logs WHERE interface_type='DL' AND is_deleted=0
    GROUP BY branch_code, branch_name
    ORDER BY cnt DESC
  `);
  console.log('\n=== DL by branch_code ===');
  console.log(JSON.stringify(byBranch.recordset, null, 2));

  await pool.close();
}

main().catch((e) => { console.error(e); process.exit(1); });
