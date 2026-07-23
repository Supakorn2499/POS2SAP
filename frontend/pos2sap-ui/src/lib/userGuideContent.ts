import type { LucideIcon } from 'lucide-react';
import {
  LayoutDashboard,
  ListFilter,
  FileInput,
  Wallet,
  Boxes,
  ScrollText,
  Settings,
  SunMoon,
  KeyRound,
  FileSpreadsheet,
  AlertTriangle,
} from 'lucide-react';

export type GuideStep = { title: string; body: string };
export type GuideSection = {
  id: string;
  icon: LucideIcon;
  title: string;
  summary: string;
  steps?: GuideStep[];
  tips?: string[];
  href?: string;
};

export type GuideDoc = {
  heroTitle: string;
  heroSubtitle: string;
  tocLabel: string;
  openPage: string;
  sections: GuideSection[];
};

const th: GuideDoc = {
  heroTitle: 'คู่มือการใช้งาน POS2SAP',
  heroSubtitle:
    'ระบบเชื่อมต่อข้อมูลจาก POS ไป SAP Business One — อ่านดึงบิล แปลง แล้วส่งขึ้น SAP พร้อมติดตามสถานะแบบเรียลไทม์',
  tocLabel: 'สารบัญ',
  openPage: 'เปิดหน้านี้',
  sections: [
    {
      id: 'overview',
      icon: KeyRound,
      title: 'เริ่มต้นใช้งาน',
      summary: 'เข้าสู่ระบบด้วยบัญชี staff จาก POS แล้วใช้เมนูด้านซ้ายเพื่อทำงานแต่ละส่วน',
      steps: [
        { title: 'เข้าสู่ระบบ', body: 'ใส่ Username / Password ของ staff แล้วกด Sign in' },
        { title: 'สลับภาษา / ธีม', body: 'มุมขวาบน: TH–EN และปุ่มพระจันทร์/อาทิตย์ สำหรับโหมดสว่าง–มืด' },
        { title: 'ออกจากระบบ', body: 'กด Logout มุมขวาบนเมื่อเลิกใช้งาน' },
      ],
      tips: [
        'หากขึ้น Unauthorized ระหว่างส่ง SAP ระบบจะลอง refresh token อัตโนมัติ — ถ้ายังไม่ได้ ให้ Login ใหม่ครั้งหนึ่ง',
      ],
    },
    {
      id: 'dashboard',
      icon: LayoutDashboard,
      title: 'Dashboard',
      summary: 'ภาพรวมสถานะการส่งข้อมูลตามประเภทเอกสารและช่วงเวลา',
      href: '/dashboard',
      steps: [
        { title: 'ดูการ์ดสถานะ', body: 'รอส่ง / กำลังส่ง / สำเร็จ / ล้มเหลว / รอ Retry' },
        { title: 'เลือกรอบข้อมูล', body: 'เลือกประเภทเอกสารและช่วงเวลา แล้วดู Log ล่าสุด + สาขายอดนิยม' },
        { title: 'เจาะรายการ', body: 'จากตาราง Log ล่าสุด กดรายละเอียดเพื่อไปหน้า Monitor' },
      ],
    },
    {
      id: 'monitor',
      icon: ListFilter,
      title: 'Monitor',
      summary: 'ค้นหา กรอง และส่ง/Retry รายการ interface_logs ทีละบิลหรือทั้งชุด',
      href: '/monitor',
      steps: [
        {
          title: 'กรองข้อมูล',
          body: 'เลือกประเภทเอกสาร (AR Invoice / Incoming Payment / Delivery) ค้นหาเลขบิล สถานะ สาขา และวันที่บิล (dd/mm/yyyy)',
        },
        { title: 'Trigger ส่งทั้งหมด', body: 'ส่งรายการ PENDING/RETRY ตามประเภทที่เลือกไป SAP' },
        { title: 'ส่งรายบิล / Retry', body: 'ปุ่มในแถวตารางสำหรับส่งซ้ำหรือลองใหม่ตามสถานะ' },
        {
          title: 'Export to Excel',
          body: 'ดาวน์โหลด CSV ตามฟิลเตอร์ปัจจุบัน รวมคอลัมน์ PosData สำหรับตรวจสอบ JSON',
        },
        {
          title: 'รายละเอียด',
          body: 'เปิดดู PosData / SapRequest / SapResponse และข้อความ error จาก SAP',
        },
      ],
      tips: [
        'วันที่ในตารางอาจแสดงปีพุทธศักราชตาม locale — ช่องกรองใช้รูปแบบ dd/mm/yyyy (เช่น 01/07/2026)',
      ],
    },
    {
      id: 'import',
      icon: FileInput,
      title: 'Import',
      summary: 'ดึงบิลจาก POS เข้าคิว PENDING โดยยังไม่ส่ง SAP จนกว่าจะ Trigger',
      href: '/import',
      steps: [
        { title: 'เลือกช่วงวันที่', body: 'ระบุวันที่ From–To (dd/mm/yyyy) สาขา และประเภทเอกสาร' },
        { title: 'Preview', body: 'ดูรายการที่จะ Import ว่า NEW หรือซ้ำ (DUP)' },
        { title: 'ยืนยัน Import', body: 'บันทึกลง interface_logs สถานะ PENDING' },
      ],
      tips: [
        'ข้อมูลก่อนวัน Cutover (ตั้งใน Config) จะไม่ถูกดึง',
      ],
    },
    {
      id: 'glmapping',
      icon: Wallet,
      title: 'GL Mapping',
      summary: 'จับคู่ประเภทการชำระเงิน POS กับบัญชี GL / หมวด SAP สำหรับ Incoming Payment',
      href: '/glmapping',
      steps: [
        { title: 'แก้ไขรายการที่ตั้งค่าแล้ว', body: 'เลือกหมวด CASH / TRANSFER / CREDIT_CARD / SKIP และใส่ GL' },
        { title: 'เพิ่มจากรายการว่าง', body: 'ในส่วน Available กดเพิ่ม แล้วตั้งค่า GL' },
        { title: 'บันทึก', body: 'แถบด้านล่างจะโชว์เมื่อมีการแก้ไข — กด Save changes' },
        {
          title: 'Export / Import Excel',
          body: 'Export เป็น CSV → แก้ใน Excel → Save As CSV UTF-8 → Import (upsert)',
        },
      ],
      tips: [
        'ระบบยังไม่รองรับไฟล์ .xlsx โดยตรง — ต้อง Save As เป็น CSV',
        'แถว CREDIT_CARD ต้องใส่รหัสบัตรใน SAP (OCRC) เช่น 1 — ไม่ใช่ชื่อแสดงผล เช่น "พักบัตรเครดิตรอเคลียร์"',
      ],
    },
    {
      id: 'pgmapping',
      icon: Boxes,
      title: 'Product Group Mapping',
      summary: 'จับคู่กลุ่มสินค้า POS กับ Item Group ของ SAP',
      href: '/productgroupmapping',
      steps: [
        { title: 'ใส่รหัส SAP', body: 'กำหนด Sap Item Group Code/Name สำหรับกลุ่มที่ Active' },
        { title: 'Export / Import', body: 'ใช้ CSV เหมือนหน้า GL Mapping สำหรับอัปเดตจำนวนมาก' },
      ],
    },
    {
      id: 'applogs',
      icon: ScrollText,
      title: 'App Logs',
      summary: 'ดูไฟล์ log ของ API (Serilog) เพื่อวิเคราะห์ปัญหา SAP / Job / Auth',
      href: '/app-logs',
      steps: [
        { title: 'เลือกไฟล์และจำนวนบรรทัด', body: 'เลือก pos2sap-yyyyMMdd.log แล้วดูท้ายไฟล์' },
        { title: 'ค้นหา', body: 'กรองคำเช่น SAP, Unauthorized, Exception' },
        { title: 'ล้าง log', body: 'ล้างไฟล์ที่เลือก หรือล้างทั้งหมดเมื่อไฟล์โตขึ้น' },
      ],
    },
    {
      id: 'config',
      icon: Settings,
      title: 'Config',
      summary: 'ตั้งค่า schedule, cutover, URL/Key ของ SAP แยกตาม interface',
      href: '/config',
      steps: [
        { title: 'Import & Cutover', body: 'กำหนดวันเริ่ม Interface (dd/mm/yyyy) และโหมดวันที่ import' },
        { title: 'Schedule', body: 'เปิด/ปิดงานอัตโนมัติ และช่วงเวลาทำงาน' },
        { title: 'SAP ต่อประเภท', body: 'ตั้ง URL / API Key ของ AR, Incoming Payment, Delivery' },
      ],
      tips: ['หลังแก้ค่า ให้กดบันทึกทีละช่องหรือบันทึกทั้งกลุ่ม'],
    },
    {
      id: 'excel',
      icon: FileSpreadsheet,
      title: 'Export / Import กับ Excel',
      summary: 'หลายหน้า Export เป็น CSV ที่ Excel เปิดได้ (มี BOM รองรับภาษาไทย)',
      steps: [
        { title: 'Export', body: 'กด Export to Excel จะได้ไฟล์ .csv' },
        { title: 'แก้ไข', body: 'เปิดด้วย Excel แล้วแก้ค่าตามคอลัมน์' },
        { title: 'บันทึกกลับ', body: 'Save As → CSV UTF-8 (Comma delimited)' },
        { title: 'Import', body: 'ที่หน้า Mapping กด Import จาก Excel แล้วเลือกไฟล์ .csv' },
      ],
      tips: ['ถ้าเลือก .xlsx ระบบจะแจ้งให้แปลงเป็น CSV แทน'],
    },
    {
      id: 'theme',
      icon: SunMoon,
      title: 'ธีมและสถานะ',
      summary: 'โหมดสว่างใช้โทนพาสเทล โหมดมืดใช้โทนเข้มแยกกัน — สถานะบิลมีสีบอกความหมาย',
      steps: [
        { title: 'รอส่ง (PENDING)', body: 'รออยู่ในคิวยังไม่ส่งหรือรอบส่งถัดไป' },
        { title: 'กำลังส่ง (PROCESSING)', body: 'กำลังเรียก SAP' },
        { title: 'สำเร็จ (SUCCESS)', body: 'SAP รับแล้ว มีเลขเอกสาร SAP' },
        { title: 'ล้มเหลว (FAILED)', body: 'ส่งไม่สำเร็จ ดู error ในรายละเอียด' },
        { title: 'รอ Retry (RETRY)', body: 'รอส่งใหม่อัตโนมัติหรือกด Retry เอง' },
      ],
    },
    {
      id: 'troubleshoot',
      icon: AlertTriangle,
      title: 'แก้ปัญหาเบื้องต้น',
      summary: 'เมื่อส่ง SAP ไม่ผ่านหรือหน้าจอค้าง',
      steps: [
        { title: 'ดู Monitor Detail', body: 'เปิด SapResponse / ErrorMessage ของบิลที่มีปัญหา' },
        { title: 'ดู App Logs', body: 'ค้นหา DocNum หรือคำว่า SAP ในไฟล์ log วันนั้น' },
        { title: 'Unauthorized', body: 'Logout แล้ว Login ใหม่หนึ่งครั้งเพื่อรับ refresh token' },
        { title: 'Mapping ไม่ครบ', body: 'ตรวจ GL / Product Group ที่ยัง PENDING ก่อนส่ง Incoming Payment หรือ Delivery' },
      ],
    },
  ],
};

const en: GuideDoc = {
  heroTitle: 'POS2SAP User Guide',
  heroSubtitle:
    'Move POS transactions into SAP Business One — import bills, transform payloads, post to SAP, and track status.',
  tocLabel: 'Contents',
  openPage: 'Open page',
  sections: [
    {
      id: 'overview',
      icon: KeyRound,
      title: 'Getting started',
      summary: 'Sign in with a POS staff account, then use the left menu for each workflow.',
      steps: [
        { title: 'Sign in', body: 'Enter staff username and password, then Sign in.' },
        { title: 'Language / theme', body: 'Top-right: TH–EN switch and moon/sun toggle for light–dark mode.' },
        { title: 'Sign out', body: 'Use Logout in the top-right when finished.' },
      ],
      tips: [
        'If you see Unauthorized during SAP send, the app auto-refreshes the token — if it still fails, sign in once more.',
      ],
    },
    {
      id: 'dashboard',
      icon: LayoutDashboard,
      title: 'Dashboard',
      summary: 'High-level send status by document type and date range.',
      href: '/dashboard',
      steps: [
        { title: 'Status cards', body: 'Pending / Processing / Success / Failed / Retry counts.' },
        { title: 'Pick a slice', body: 'Choose interface type and period, then review recent logs and top branches.' },
        { title: 'Drill in', body: 'From Recent logs, open Details to jump into Monitor.' },
      ],
    },
    {
      id: 'monitor',
      icon: ListFilter,
      title: 'Monitor',
      summary: 'Search, filter, and send/retry interface log rows.',
      href: '/monitor',
      steps: [
        {
          title: 'Filter',
          body: 'Pick document type (AR Invoice / Incoming Payment / Delivery), search bill no., status, branch, and bill dates (dd/mm/yyyy).',
        },
        { title: 'Trigger all', body: 'Send PENDING/RETRY rows for the selected interface to SAP.' },
        { title: 'Per-row send / retry', body: 'Use row actions to resend or retry.' },
        {
          title: 'Export to Excel',
          body: 'Download a CSV of the current filter, including PosData JSON.',
        },
        {
          title: 'Details',
          body: 'Inspect PosData / SapRequest / SapResponse and SAP error text.',
        },
      ],
      tips: [
        'Filter dates use dd/mm/yyyy (e.g. 01/07/2026). Table dates may follow Buddhist year via locale.',
      ],
    },
    {
      id: 'import',
      icon: FileInput,
      title: 'Import',
      summary: 'Pull POS bills into PENDING logs without posting to SAP yet.',
      href: '/import',
      steps: [
        { title: 'Date range', body: 'Set From–To (dd/mm/yyyy), branch, and document type.' },
        { title: 'Preview', body: 'See NEW vs duplicate (DUP) rows before writing.' },
        { title: 'Confirm import', body: 'Inserts interface_logs rows as PENDING.' },
      ],
      tips: ['Data before the Config cutover date is never imported.'],
    },
    {
      id: 'glmapping',
      icon: Wallet,
      title: 'GL Mapping',
      summary: 'Map POS pay types to SAP GL / payment categories for Incoming Payment.',
      href: '/glmapping',
      steps: [
        { title: 'Edit mapped rows', body: 'Set CASH / TRANSFER / CREDIT_CARD / SKIP and GL account.' },
        { title: 'Add from Available', body: 'Add unmapped pay types, then fill GL fields.' },
        { title: 'Save', body: 'Use the bottom unsaved bar → Save changes.' },
        {
          title: 'Export / Import Excel',
          body: 'Export CSV → edit in Excel → Save As CSV UTF-8 → Import (upsert).',
        },
      ],
      tips: [
        '.xlsx is not supported — always Save As CSV.',
        'For CREDIT_CARD rows, SapPayTypeName must be the SAP OCRC credit-card code (e.g. 1), not a display name.',
      ],
    },
    {
      id: 'pgmapping',
      icon: Boxes,
      title: 'Product Group Mapping',
      summary: 'Map POS product groups to SAP item groups.',
      href: '/productgroupmapping',
      steps: [
        { title: 'Set SAP codes', body: 'Fill Sap Item Group Code/Name for active groups.' },
        { title: 'Export / Import', body: 'Same CSV workflow as GL Mapping for bulk updates.' },
      ],
    },
    {
      id: 'applogs',
      icon: ScrollText,
      title: 'App Logs',
      summary: 'Read API Serilog files to diagnose SAP / job / auth issues.',
      href: '/app-logs',
      steps: [
        { title: 'Pick a file', body: 'Choose pos2sap-yyyyMMdd.log and how many trailing lines to show.' },
        { title: 'Search', body: 'Filter for SAP, Unauthorized, Exception, etc.' },
        { title: 'Clear logs', body: 'Clear one file or all when disk use grows.' },
      ],
    },
    {
      id: 'config',
      icon: Settings,
      title: 'Config',
      summary: 'Schedule, cutover, and per-interface SAP endpoints/keys.',
      href: '/config',
      steps: [
        { title: 'Import & Cutover', body: 'Set interface start date (dd/mm/yyyy) and import date mode.' },
        { title: 'Schedule', body: 'Enable the background job and its time window.' },
        { title: 'SAP per interface', body: 'Configure AR / Incoming Payment / Delivery URLs and API keys.' },
      ],
      tips: ['Save each field or the whole group after edits.'],
    },
    {
      id: 'excel',
      icon: FileSpreadsheet,
      title: 'Excel export / import',
      summary: 'Pages export UTF-8 BOM CSV that Excel opens with Thai intact.',
      steps: [
        { title: 'Export', body: 'Click Export to Excel to download .csv.' },
        { title: 'Edit', body: 'Open in Excel and edit columns.' },
        { title: 'Save back', body: 'Save As → CSV UTF-8 (Comma delimited).' },
        { title: 'Import', body: 'On Mapping pages, Import from Excel and pick the .csv.' },
      ],
      tips: ['Choosing .xlsx shows a tip to convert to CSV.'],
    },
    {
      id: 'theme',
      icon: SunMoon,
      title: 'Theme & statuses',
      summary: 'Light uses soft pastels; dark uses a separate richer palette.',
      steps: [
        { title: 'PENDING', body: 'Queued, not yet sent.' },
        { title: 'PROCESSING', body: 'SAP call in progress.' },
        { title: 'SUCCESS', body: 'Accepted by SAP; SAP doc no. stored.' },
        { title: 'FAILED', body: 'Send failed — check detail error.' },
        { title: 'RETRY', body: 'Waiting for automatic or manual retry.' },
      ],
    },
    {
      id: 'troubleshoot',
      icon: AlertTriangle,
      title: 'Troubleshooting',
      summary: 'When SAP posts fail or the UI feels stuck.',
      steps: [
        { title: 'Monitor detail', body: 'Open SapResponse / ErrorMessage for the failing bill.' },
        { title: 'App Logs', body: 'Search the day\'s log for DocNum or SAP.' },
        { title: 'Unauthorized', body: 'Logout and login once to refresh tokens.' },
        { title: 'Incomplete mapping', body: 'Fix pending GL / Product Group rows before AP or Delivery sends.' },
      ],
    },
  ],
};

export function getUserGuide(lang: 'th' | 'en'): GuideDoc {
  return lang === 'th' ? th : en;
}
