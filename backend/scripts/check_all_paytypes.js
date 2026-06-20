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
  -- All payment types in POS system
  SELECT p.PayTypeID, p.PayTypeName,
         glm.SapPayCategory, glm.SapGlAccount, glm.IsActive as MappedActive
  FROM paytype p
  LEFT JOIN paytype_gl_mapping glm ON glm.PayTypeID = p.PayTypeID
  ORDER BY glm.SapPayCategory, p.PayTypeID
`)).then(r => {
  console.log('\nAll PayTypes:');
  console.table(r.recordset);
  process.exit(0);
}).catch(e => { console.error(e.message); process.exit(1); });
