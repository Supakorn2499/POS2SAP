/**
 * Diagnose ReceiptNumber vs document lines mismatch.
 * Usage: node diagnose_receipt_lines.js "RC01072026/00001"
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
    options: { trustServerCertificate: true, connectTimeout: 60000, requestTimeout: 120000 },
  };
}

async function main() {
  const docNo = process.argv[2] || 'RC01072026/00001';
  const apiDir = path.join(__dirname, '../POS2SAP.API');
  const cfgPath = [
    path.join(apiDir, 'appsettings.Development.json'),
    path.join(apiDir, 'appsettings.json'),
  ].find((p) => fs.existsSync(p));
  const cfg = JSON.parse(fs.readFileSync(cfgPath, 'utf8'));
  const pool = await sql.connect(parseConn(cfg.ConnectionStrings.DefaultConnection));

  const heads = await pool.request().input('DocNo', sql.NVarChar, docNo).query(`
    SELECT
      a.TranKey,
      a.ReceiptNumber,
      a.SaleDate,
      a.ShopID,
      a.ComputerID,
      a.TransactionStatusID,
      a.Deleted,
      a.ReceiptPayPrice,
      ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode) AS BranchCode,
      ISNULL(s.BranchName, '') AS BranchName,
      ISNULL(s.ShopName, '') AS ShopName
    FROM ordertransaction a
    LEFT JOIN shop_data s ON s.ShopID = a.ShopID
    WHERE a.ReceiptNumber = @DocNo
    ORDER BY a.SaleDate, a.TranKey
  `);

  console.log(`=== Heads with ReceiptNumber = ${docNo} ===`);
  console.log(`count=${heads.recordset.length}`);
  for (const h of heads.recordset) {
    console.log(JSON.stringify(h, null, 2));
  }

  for (const h of heads.recordset) {
    const lines = await pool.request().input('TranKey', sql.NVarChar, h.TranKey).query(`
      SELECT
        b.OrderDetailID,
        b.DisplayOrdering,
        b.ProductID,
        ISNULL(c.ProductCode, CAST(b.ProductID AS NVARCHAR(50))) AS ItemCode,
        ISNULL(c.ProductName, '') AS ProductName,
        ISNULL(b.TotalQty, 0) AS Qty,
        ISNULL(b.ProductBeforeVAT, 0) + ISNULL(b.ProductVAT, 0) AS GTotal,
        ISNULL(b.Comment, '') AS Comment,
        ISNULL(b.Deleted, 0) AS Deleted,
        ISNULL(CAST(b.InventoryID AS NVARCHAR(20)), '') AS WhsCode
      FROM orderdetail b
      LEFT JOIN products c ON c.ProductID = b.ProductID
      WHERE b.TranKey = @TranKey
      ORDER BY b.DisplayOrdering, b.OrderDetailID
    `);
    console.log(`\n=== Lines for TranKey=${h.TranKey} Branch=${h.BranchCode} ===`);
    console.log(`lineCount=${lines.recordset.length}`);
    for (const l of lines.recordset) {
      console.log(`  #${l.DisplayOrdering} ${l.ItemCode} | ${l.ProductName} | qty=${l.Qty} | total=${l.GTotal} | del=${l.Deleted}`);
    }
  }

  // Also search products matching receipt names
  const nameHints = await pool.request().query(`
    SELECT TOP 20 ProductID, ProductCode, ProductName
    FROM products
    WHERE ProductName LIKE N'%Fettuccine%'
       OR ProductName LIKE N'%Garlic ranch%'
       OR ProductName LIKE N'%เฟตตู%'
       OR ProductName LIKE N'%Chicken pesto%'
       OR ProductName LIKE N'%สลัดไก่เพสโต%'
    ORDER BY ProductName
  `);
  console.log('\n=== Related products ===');
  for (const p of nameHints.recordset) {
    console.log(`  ${p.ProductCode} | ${p.ProductName}`);
  }

  await pool.close();
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
