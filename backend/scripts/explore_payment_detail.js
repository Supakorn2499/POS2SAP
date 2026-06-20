// explore_payment_detail.js — payment breakdown with correct columns
const sql = require('mssql');
const config = {
  server: '203.151.92.185', port: 1444, database: 'HQ_FAMTIME',
  user: 'vtecPOS', password: 'vtecpwnet',
  options: { trustServerCertificate: true, connectTimeout: 30000, requestTimeout: 60000 }
};
async function run() {
  const pool = await sql.connect(config);

  console.log('\n===== Payment breakdown — recent transactions =====');
  const r = await pool.request().query(`
    SELECT TOP 30
      a.ReceiptNumber,
      a.TranKey,
      CONVERT(varchar,a.SaleDate,23)    AS SaleDate,
      p.PayTypeID,
      p.PayTypeName,
      p.PayTypeCode,
      opd.PayAmount,
      opd.CreditCardNo,
      opd.CCApproveCode,
      opd.ExpireMonth,
      opd.ExpireYear,
      opd.BankNameID,
      opd.CreditCardType,
      opd.PayRemark,
      opd.VoucherNo
    FROM ordertransaction a
    JOIN orderpaydetail opd ON opd.TranKey = a.TranKey
    JOIN paytype p ON p.PayTypeID = opd.PayTypeID
    WHERE a.TransactionStatusID = 2
      AND ISNULL(a.Deleted,0) = 0
    ORDER BY a.SaleDate DESC, a.ReceiptNumber
  `);
  if (r.recordset.length > 0) {
    const cols = Object.keys(r.recordset[0]);
    console.log(cols.join('\t|\t'));
    r.recordset.forEach(row => console.log(cols.map(c => row[c] ?? '').join('\t|\t')));
  }

  // PayType distribution in use
  console.log('\n===== PayType usage counts =====');
  const r2 = await pool.request().query(`
    SELECT p.PayTypeID, p.PayTypeName, p.PayTypeCode, COUNT(*) AS TxnCount, SUM(opd.PayAmount) AS TotalAmount
    FROM orderpaydetail opd
    JOIN paytype p ON p.PayTypeID = opd.PayTypeID
    JOIN ordertransaction a ON a.TranKey = opd.TranKey
    WHERE a.TransactionStatusID = 2 AND ISNULL(a.Deleted,0) = 0
    GROUP BY p.PayTypeID, p.PayTypeName, p.PayTypeCode
    ORDER BY TxnCount DESC
  `);
  r2.recordset.forEach(row => console.log(
    `  [${String(row.PayTypeID).padStart(3)}] ${String(row.PayTypeName).padEnd(35)} Code=${String(row.PayTypeCode).padEnd(15)} Count=${row.TxnCount} Total=${row.TotalAmount}`
  ));

  await pool.close();
}
run().catch(e => { console.error(e.message); process.exit(1); });
