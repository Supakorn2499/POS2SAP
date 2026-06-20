// explore_payment_schema.js — run: node explore_payment_schema.js
const sql = require('mssql');

const config = {
  server: '203.151.92.185',
  port: 1444,
  database: 'HQ_FAMTIME',
  user: 'vtecPOS',
  password: 'vtecpwnet',
  options: { trustServerCertificate: true, connectTimeout: 30000 }
};

async function run() {
  const pool = await sql.connect(config);

  // orderpaydetail columns
  console.log('\n===== orderpaydetail columns =====');
  const r1 = await pool.request().query(`
    SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'orderpaydetail'
    ORDER BY ORDINAL_POSITION
  `);
  r1.recordset.forEach(c => console.log(`  ${c.COLUMN_NAME.padEnd(35)} ${c.DATA_TYPE.padEnd(15)} nullable=${c.IS_NULLABLE}`));

  // paytype columns
  console.log('\n===== paytype columns =====');
  const r2 = await pool.request().query(`
    SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'paytype'
    ORDER BY ORDINAL_POSITION
  `);
  r2.recordset.forEach(c => console.log(`  ${c.COLUMN_NAME.padEnd(35)} ${c.DATA_TYPE.padEnd(15)} nullable=${c.IS_NULLABLE}`));

  // sample orderpaydetail rows
  console.log('\n===== orderpaydetail sample (5 rows) =====');
  const r3 = await pool.request().query(`SELECT TOP 5 * FROM orderpaydetail ORDER BY PayDetailID DESC`);
  if (r3.recordset.length > 0) {
    console.log(Object.keys(r3.recordset[0]).join(' | '));
    r3.recordset.forEach(row => console.log(Object.values(row).join(' | ')));
  }

  // sample paytype rows
  console.log('\n===== paytype all rows =====');
  const r4 = await pool.request().query(`SELECT * FROM paytype ORDER BY PayTypeID`);
  if (r4.recordset.length > 0) {
    console.log(Object.keys(r4.recordset[0]).join(' | '));
    r4.recordset.forEach(row => console.log(Object.values(row).join(' | ')));
  }

  // join example — payment breakdown for a recent transaction
  console.log('\n===== payment breakdown sample (join ordertransaction + orderpaydetail + paytype) =====');
  const r5 = await pool.request().query(`
    SELECT TOP 20
      a.ReceiptNumber,
      a.TranKey,
      a.SaleDate,
      p.PayTypeID,
      p.PayTypeName,
      opd.PayAmount,
      opd.PayChange,
      opd.PayReference,
      opd.PayDate,
      opd.CardNumber,
      opd.CardExpire,
      opd.BankName,
      opd.ApprovalCode
    FROM ordertransaction a
    JOIN orderpaydetail opd ON opd.TranKey = a.TranKey
    JOIN paytype p ON p.PayTypeID = opd.PayTypeID
    WHERE a.TransactionStatusID = 2
      AND ISNULL(a.Deleted, 0) = 0
    ORDER BY a.SaleDate DESC, a.ReceiptNumber
  `);
  if (r5.recordset.length > 0) {
    console.log(Object.keys(r5.recordset[0]).join(' | '));
    r5.recordset.forEach(row => console.log(Object.values(row).join(' | ')));
  } else {
    console.log('No rows found — try adjusting the join condition');
  }

  await pool.close();
}

run().catch(e => { console.error(e.message); process.exit(1); });
