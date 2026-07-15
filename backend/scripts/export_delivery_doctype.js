/**
 * Export Delivery Doc Type mapping (same query as /api/delivery-doctype) to CSV/XLSX-friendly CSV.
 * Usage: node export_delivery_doctype.js
 */
const fs = require('fs');
const path = require('path');
const sql = require('mssql');

function parseConn(cs) {
  const get = (k) => {
    const m = cs.match(new RegExp(`${k}=([^;]+)`, 'i'));
    return m ? m[1] : '';
  };
  const serverRaw = get('Server') || get('Data Source');
  let server = serverRaw;
  let port = 1433;
  if (serverRaw.includes(',')) {
    const [h, p] = serverRaw.split(',');
    server = h;
    port = parseInt(p, 10) || 1433;
  }
  return {
    server,
    port,
    database: get('Database') || get('Initial Catalog'),
    user: get('User Id') || get('User ID') || get('Uid'),
    password: get('Password') || get('Pwd'),
    options: { trustServerCertificate: true, connectTimeout: 30000 },
  };
}

async function main() {
  const apiDir = path.join(__dirname, '../POS2SAP.API');
  const cfgPath = [
    path.join(apiDir, 'appsettings.Development.json'),
    path.join(apiDir, 'appsettings.json'),
  ].find((p) => fs.existsSync(p));
  if (!cfgPath) throw new Error('No appsettings*.json found');

  const cfg = JSON.parse(fs.readFileSync(cfgPath, 'utf8'));
  const cs = cfg.ConnectionStrings?.DefaultConnection;
  if (!cs) throw new Error('DefaultConnection missing');

  const pool = await sql.connect(parseConn(cs));
  const result = await pool.request().query(`
    SELECT
      dt.DocumentTypeID AS DocumentTypeId,
      dt.DocumentTypeHeader AS DocumentTypeCode,
      ISNULL(NULLIF(LTRIM(RTRIM(dt.DocumentTypeName)), ''), dt.DocumentTypeHeader) AS DocumentTypeName,
      CASE WHEN m.DocumentTypeID IS NOT NULL AND m.IsEnabled = 1 THEN 'Enabled' ELSE 'Disabled' END AS Status,
      CASE WHEN m.DocumentTypeID IS NOT NULL AND m.IsEnabled = 1 THEN 1 ELSE 0 END AS IsEnabled
    FROM documenttype dt
    LEFT JOIN dl_documenttype_mapping m
      ON m.DocumentTypeID = dt.DocumentTypeID AND m.IsEnabled = 1
    WHERE ISNULL(dt.Deleted, 0) = 0
      AND dt.MovementInStock = -1
    ORDER BY dt.DocumentTypeHeader
  `);

  const rows = result.recordset;
  const outDir = path.join(__dirname, '../../docs/exports');
  fs.mkdirSync(outDir, { recursive: true });
  const csvPath = path.join(outDir, 'delivery-doctype.csv');

  const header = ['DocumentTypeId', 'DocumentTypeCode', 'DocumentTypeName', 'Status', 'IsEnabled'];
  const esc = (v) => {
    const s = v == null ? '' : String(v);
    return /["',\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const lines = [header.join(',')].concat(
    rows.map((r) => header.map((h) => esc(r[h])).join(','))
  );
  // UTF-8 BOM so Excel opens Thai correctly
  fs.writeFileSync(csvPath, '\uFEFF' + lines.join('\n'), 'utf8');

  const enabled = rows.filter((r) => r.IsEnabled === 1).length;
  console.log(`Wrote ${rows.length} rows (${enabled} enabled) -> ${csvPath}`);
  rows.forEach((r) =>
    console.log(`${r.IsEnabled ? '[ON] ' : '     '}${r.DocumentTypeCode}\t${r.DocumentTypeName}`)
  );

  await pool.close();
}

main().catch((e) => {
  console.error(e.message || e);
  process.exit(1);
});
