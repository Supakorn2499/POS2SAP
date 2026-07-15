/**
 * Smoke-test: load AR sales bills and map to Delivery JSON schema (same as PosDataService.MapArToDelivery).
 * Usage: node test_delivery_json.js [daysBack=3] [top=2]
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

function formatQuantity(value) {
  if (value == null) return '0';
  if (typeof value === 'string' && value.trim()) return value.trim();
  const d = Number(value);
  if (!Number.isFinite(d)) return '0';
  if (d === Math.trunc(d)) return String(Math.trunc(d));
  return String(d);
}

function mapArToDelivery(bill) {
  const lines = bill.DocumentLines || [];
  return {
    DocNum: bill.DocNum || '',
    DocDate: bill.DocDate || '',
    POSID: bill.POSID || '',
    CardCode: bill.CardCode || '',
    CardName: bill.CardName || '',
    BranchCode: bill.BranchCode || '',
    BranchName: bill.BranchName || '',
    VatBranch: '00000',
    DeliveryReason: 'เบิกเพื่อขาย',
    DeliveryReasonOther: '',
    Comments: bill.Comments || '',
    DocumentLines: lines.map((line, i) => {
      const uom = line.UomCode || '';
      return {
        DocNum: bill.DocNum || '',
        LineNum: i,
        ItemCode: line.ItemCode || '',
        Dscription: line.Dscription || '',
        Comment: undefined,
        FreeTxt: line.FreeTxt || '',
        Quantity: formatQuantity(line.Quantity),
        UomCode: uom,
        unitMsr: uom,
        WhsCode: line.WhsCode || '',
      };
    }).map(({ Comment, ...rest }) => rest),
  };
}

async function main() {
  const daysBack = parseInt(process.argv[2] || '7', 10);
  const top = Math.min(parseInt(process.argv[3] || '2', 10), 20);

  const apiDir = path.join(__dirname, '../POS2SAP.API');
  const cfgPath = [
    path.join(apiDir, 'appsettings.Development.json'),
    path.join(apiDir, 'appsettings.json'),
  ].find((p) => fs.existsSync(p));
  if (!cfgPath) throw new Error('No appsettings*.json');

  const cfg = JSON.parse(fs.readFileSync(cfgPath, 'utf8'));
  const pool = await sql.connect(parseConn(cfg.ConnectionStrings.DefaultConnection));

  const dateTo = new Date();
  dateTo.setHours(0, 0, 0, 0);
  const dateFrom = new Date(dateTo);
  dateFrom.setDate(dateFrom.getDate() - daysBack);
  const dateToExcl = new Date(dateTo);
  dateToExcl.setDate(dateToExcl.getDate() + 1);

  const heads = await pool.request()
    .input('DateFrom', sql.DateTime, dateFrom)
    .input('DateToExclusive', sql.DateTime, dateToExcl)
    .query(`
      SELECT TOP (${top})
        a.TranKey,
        a.ReceiptNumber AS DocNum,
        CONVERT(varchar(10), a.SaleDate, 23) AS DocDate,
        CAST(a.ComputerID AS NVARCHAR(20)) AS POSID,
        ISNULL(s.SLOC, '') AS CardCode,
        CASE
          WHEN ft.FullTaxInvoiceID IS NOT NULL THEN
            NULLIF(LTRIM(RTRIM(CONCAT(ISNULL(ft.InvoiceName, N''), N' ', ISNULL(ft.InvoiceName1, N'')))), N'')
          WHEN NULLIF(LTRIM(RTRIM(ISNULL(a.MemberName, N''))), N'') IS NOT NULL THEN a.MemberName
          ELSE ISNULL(s.BranchName, N'')
        END AS CardName,
        ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode) AS BranchCode,
        ISNULL(s.BranchName, '') AS BranchName,
        ISNULL(a.TransactionNote, '') AS Comments
      FROM ordertransaction a
      LEFT JOIN shop_data s ON s.ShopID = a.ShopID
      OUTER APPLY (
        SELECT TOP 1 l.FullTaxInvoiceID, l.FullTaxInvoiceComputerID
        FROM orderfulltaxinvoicelink l
        WHERE l.TranKey = a.TranKey AND l.FullTaxStatus = 2
        ORDER BY l.UpdateDate DESC
      ) link
      LEFT JOIN ordertransactionfulltaxinvoice ft
        ON ft.FullTaxInvoiceID = link.FullTaxInvoiceID
       AND ft.FullTaxInvoiceComputerID = link.FullTaxInvoiceComputerID
       AND ft.FullTaxStatus = 2
      WHERE a.TransactionStatusID = 2
        AND ISNULL(a.Deleted, 0) = 0
        AND a.SaleDate >= @DateFrom
        AND a.SaleDate < @DateToExclusive
      ORDER BY a.SaleDate DESC, a.ReceiptNumber DESC
    `);

  if (!heads.recordset.length) {
    console.error(`No AR bills in last ${daysBack} day(s). Try a larger window.`);
    process.exit(2);
  }

  const deliveries = [];
  for (const h of heads.recordset) {
    const lines = await pool.request()
      .input('TranKey', sql.NVarChar, h.TranKey)
      .query(`
        SELECT
          ISNULL(c.ProductCode, CAST(b.ProductID AS NVARCHAR(50))) AS ItemCode,
          ISNULL(c.ProductName, '') AS Dscription,
          ISNULL(b.Comment, '') AS FreeTxt,
          ISNULL(b.TotalQty, 0) AS Quantity,
          ISNULL(c.ProductUnitName, '') AS UomCode,
          ISNULL(CAST(b.InventoryID AS NVARCHAR(20)), '') AS WhsCode
        FROM orderdetail b
        LEFT JOIN products c ON c.ProductID = b.ProductID
        WHERE b.TranKey = @TranKey
        ORDER BY b.DisplayOrdering, b.OrderDetailID
      `);

    const bill = {
      DocNum: `${h.BranchCode}|${h.DocNum}`,
      DocDate: h.DocDate,
      POSID: h.POSID,
      CardCode: h.CardCode,
      CardName: h.CardName,
      BranchCode: h.BranchCode,
      BranchName: h.BranchName,
      Comments: h.Comments,
      DocumentLines: lines.recordset,
    };
    deliveries.push(mapArToDelivery(bill));
  }

  await pool.close();

  const outDir = path.join(__dirname, '../../docs/exports');
  fs.mkdirSync(outDir, { recursive: true });
  const outPath = path.join(outDir, 'delivery-from-ar-sample.json');
  fs.writeFileSync(outPath, JSON.stringify(deliveries, null, 2), 'utf8');

  console.log(`Wrote ${deliveries.length} Delivery JSON object(s) -> ${outPath}`);
  console.log('--- sample (first) ---');
  console.log(JSON.stringify(deliveries[0], null, 2));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
