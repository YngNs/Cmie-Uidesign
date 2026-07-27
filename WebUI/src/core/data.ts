import type {
  Alarm, ChannelDef, DeviceInfo, HistoryTest, MotorModel,
  ReportTemplate, TestItemNode, UserInfo,
} from './types'

// ============================================================
// 采集通道 —— 完整还原原软件"实时数据"页字段
// （线电压/电流/功率/频率/功率因数/轴功率/转速/扭矩 + 12 路温度）
// ============================================================
export const CHANNELS: ChannelDef[] = [
  { key: 'Uab', label: '线电压 Uab', unit: 'V', group: '电压', color: '#2563eb', min: 0, max: 500, precision: 2 },
  { key: 'Ubc', label: '线电压 Ubc', unit: 'V', group: '电压', color: '#1d4ed8', min: 0, max: 500, precision: 2 },
  { key: 'Uca', label: '线电压 Uca', unit: 'V', group: '电压', color: '#3b82f6', min: 0, max: 500, precision: 2 },
  { key: 'Uavg', label: '平均电压 Uavg', unit: 'V', group: '电压', color: '#60a5fa', min: 0, max: 500, precision: 2 },
  { key: 'Ia', label: '电流 Ia', unit: 'A', group: '电流', color: '#dc2626', min: 0, max: 300, precision: 3 },
  { key: 'Ib', label: '电流 Ib', unit: 'A', group: '电流', color: '#b91c1c', min: 0, max: 300, precision: 3 },
  { key: 'Ic', label: '电流 Ic', unit: 'A', group: '电流', color: '#ef4444', min: 0, max: 300, precision: 3 },
  { key: 'Iavg', label: '平均电流 Iavg', unit: 'A', group: '电流', color: '#f87171', min: 0, max: 300, precision: 3 },
  { key: 'P', label: '有功功率 P', unit: 'kW', group: '电参数', color: '#d97706', min: 0, max: 160, precision: 3 },
  { key: 'Q', label: '无功功率 Q', unit: 'kvar', group: '电参数', color: '#f59e0b', min: 0, max: 120, precision: 3 },
  { key: 'PF', label: '功率因数 PF', unit: '', group: '电参数', color: '#0d9488', min: 0, max: 1, precision: 3 },
  { key: 'f', label: '频率 f', unit: 'Hz', group: '电参数', color: '#0891b2', min: 0, max: 60, precision: 2 },
  { key: 'Pshaft', label: '轴功率 Pshaft', unit: 'kW', group: '机械参数', color: '#7c3aed', min: 0, max: 140, precision: 3 },
  { key: 'n', label: '转速 n', unit: 'rpm', group: '机械参数', color: '#65a30d', min: 0, max: 3000, precision: 0 },
  { key: 'T', label: '扭矩 T', unit: 'N·m', group: '机械参数', color: '#c026d3', min: -50, max: 900, precision: 2 },
  { key: 'T_w1', label: '定子 U 相绕组', unit: '℃', group: '温度', color: '#e11d48', min: 0, max: 155, precision: 1 },
  { key: 'T_w2', label: '定子 V 相绕组', unit: '℃', group: '温度', color: '#f43f5e', min: 0, max: 155, precision: 1 },
  { key: 'T_w3', label: '定子 W 相绕组', unit: '℃', group: '温度', color: '#fb7185', min: 0, max: 155, precision: 1 },
  { key: 'T_stator_avg', label: '定子平均温度', unit: '℃', group: '温度', color: '#e8798f', min: 0, max: 155, precision: 1 },
  { key: 'T_w4', label: '转子 U 相绕组', unit: '℃', group: '温度', color: '#be123c', min: 0, max: 155, precision: 1 },
  { key: 'T_w5', label: '转子 V 相绕组', unit: '℃', group: '温度', color: '#9f1239', min: 0, max: 155, precision: 1 },
  { key: 'T_w6', label: '转子 W 相绕组', unit: '℃', group: '温度', color: '#881337', min: 0, max: 155, precision: 1 },
  { key: 'T_rotor_avg', label: '转子平均温度', unit: '℃', group: '温度', color: '#d14b6f', min: 0, max: 155, precision: 1 },
  { key: 'T_de', label: 'DE1 轴承', unit: '℃', group: '温度', color: '#ea580c', min: 0, max: 120, precision: 1 },
  { key: 'T_nde', label: 'NDE1 轴承', unit: '℃', group: '温度', color: '#f97316', min: 0, max: 120, precision: 1 },
  { key: 'T_in', label: '进风温度', unit: '℃', group: '温度', color: '#0284c7', min: 0, max: 60, precision: 1 },
  { key: 'T_out', label: '出风温度', unit: '℃', group: '温度', color: '#0ea5e9', min: 0, max: 80, precision: 1 },
  { key: 'T_core', label: '铁芯温度', unit: '℃', group: '温度', color: '#9333ea', min: 0, max: 155, precision: 1 },
  { key: 'T_surf', label: '表面温度', unit: '℃', group: '温度', color: '#a855f7', min: 0, max: 120, precision: 1 },
  { key: 'T_amb', label: '环境温度', unit: '℃', group: '温度', color: '#16a34a', min: -10, max: 50, precision: 1 },
]

export const CHANNEL_MAP = Object.fromEntries(CHANNELS.map(c => [c.key, c]))

/** 主监控曲线的可选通道和默认选中项。 */
export const DEFAULT_CURVE_KEYS = [
  'n', 'T', 'Uavg', 'Iavg', 'P', 'Q', 'PF', 'T_stator_avg', 'T_rotor_avg',
] as const

// ============================================================
// 采集设备 —— 对应原软件驱动类：ClassWT3K / ClassPLC / MW100 / TP700 等
// ============================================================
export const DEVICES: DeviceInfo[] = [
  { id: 'wt3k', name: '功率分析仪 1', model: 'Yokogawa WT3000', role: '电参数测量（被试机）', protocol: 'Modbus-TCP', address: '192.168.1.21:502', status: 'online', latencyMs: 12 },
  { id: 'wt3k2', name: '功率分析仪 2', model: 'Yokogawa WT3000', role: '电参数测量（陪试机）', protocol: 'Modbus-TCP', address: '192.168.1.22:502', status: 'online', latencyMs: 15 },
  { id: 'plc', name: 'PLC 控制器', model: 'Siemens S7-1200', role: '试验流程控制 / 联锁保护', protocol: 'S7-Comm', address: '192.168.1.10', status: 'online', latencyMs: 8 },
  { id: 'tp700', name: 'TP700 通信终端', model: 'TP700', role: 'Modbus TCP 数据通信', protocol: 'Modbus-TCP', address: '192.168.1.24:502', status: 'online', latencyMs: 18 },
  { id: 'tq', name: '转矩转速仪', model: 'XK3012 + JN338', role: '轴功率 / 转速 / 扭矩', protocol: 'RS-232', address: 'COM4 · 19200-8-N-1', status: 'online', latencyMs: 22 },
  { id: 'mw100', name: '温度采集仪', model: 'Yokogawa MW100', role: '多通道温度采集', protocol: 'Modbus-TCP', address: '192.168.1.30:502', status: 'online', latencyMs: 24 },
]

// ============================================================
// 试验项目树 —— 完整还原原软件"试验项目"标签页
// ============================================================
export const TEST_TREE: TestItemNode[] = [
  { id: 'resistance', label: '直流电阻和绝缘电阻测量', kind: 'record' },
  { id: 'noload', label: '空载特性试验', kind: 'record' },
  { id: 'load', label: '负载特性试验', kind: 'record' },
  {
    id: 'eff', label: '效率分析', kind: 'group', children: [
      { id: 'eff-a', label: 'A法数据分析', kind: 'analysis' },
      { id: 'eff-b', label: 'B法数据分析', kind: 'analysis' },
      { id: 'eff-e', label: 'E法数据分析', kind: 'analysis' },
      { id: 'eff-circle', label: '圆图法效率分析', kind: 'analysis' },
    ],
  },
  {
    id: 'temprise', label: '温升试验', kind: 'group', children: [
      {
        id: 'temprise-rated', label: '额定工况负载温升试验', kind: 'group', children: [
          { id: 'temprise-rated-rec', label: '额定工况负载温升记录', kind: 'record' },
          { id: 'temprise-rated-stator', label: '额定工况负载温升定子热电阻分析', kind: 'analysis' },
          { id: 'temprise-rated-rotor', label: '额定工况负载温升转子热电阻分析', kind: 'analysis' },
        ],
      },
      {
        id: 'temprise-lf', label: '低频负载温升试验', kind: 'group', children: [
          { id: 'temprise-lf-rec', label: '低频负载温升记录', kind: 'record' },
          { id: 'temprise-lf-stator', label: '低频负载温升定子热电阻分析', kind: 'analysis' },
          { id: 'temprise-lf-rotor', label: '低频负载温升转子热电阻分析', kind: 'analysis' },
        ],
      },
    ],
  },
  {
    id: 'locked', label: '堵转特性试验', kind: 'group', children: [
      { id: 'locked-50', label: '工频堵转试验', kind: 'record' },
      { id: 'locked-lf', label: '低频堵转试验', kind: 'record' },
    ],
  },
  { id: 'withstand', label: '短时升压/耐电压/超速/短时过载/过电流', kind: 'record' },
  { id: 'impulse', label: '匝间冲击/开路电压/转动惯量/旋转方向', kind: 'record' },
  { id: 'inertia', label: '转动惯量测定', kind: 'record' },
  { id: 'vibration', label: '振动和噪声试验', kind: 'record' },
  { id: 'tn', label: 'TN试验', kind: 'record' },
  { id: 'maxtq', label: '圆图法最大转矩计算', kind: 'analysis' },
]

/** 试验项目 id → 默认记录表列（用于核心监控页表格） */
export const RECORD_COLUMNS: { key: string; label: string }[] = [
  { key: 'Uab', label: '线电压 Uab /V' },
  { key: 'Ubc', label: '线电压 Ubc /V' },
  { key: 'Uca', label: '线电压 Uca /V' },
  { key: 'Ia', label: '线电流 Ia /A' },
  { key: 'Ib', label: '线电流 Ib /A' },
  { key: 'Ic', label: '线电流 Ic /A' },
  { key: 'P', label: '有功功率 /kW' },
  { key: 'Q', label: '无功功率 /kvar' },
  { key: 'Pshaft', label: '轴功率 /kW' },
]

// ============================================================
// 电机型号库 —— 对应 frmTypeManager 型号参数
// ============================================================
export const MOTOR_MODELS: MotorModel[] = [
  {
    id: 'm1', motorNo: '2026DJ0031', model: 'YE3-315S-4', manufacturer: '上海电气集团上海电机厂有限公司',
    serialNo: 'SE26-04821', ratedVoltage: 380, ratedCurrent: 201, ratedPower: 110, ratedFreq: 50,
    ratedSpeed: 1485, ratedPF: 0.89, wiring: '△', insulation: 'F 级', duty: 'S1 连续工作制',
    ip: 'IP55', poles: 4, cooling: 'IC411 自扇冷', sampleName: '三相异步电动机',
  },
  {
    id: 'm2', motorNo: '2026DJ0027', model: 'YE4-280M-4', manufacturer: '上海电气集团上海电机厂有限公司',
    serialNo: 'SE26-03902', ratedVoltage: 380, ratedCurrent: 164, ratedPower: 90, ratedFreq: 50,
    ratedSpeed: 1480, ratedPF: 0.90, wiring: '△', insulation: 'F 级', duty: 'S1 连续工作制',
    ip: 'IP55', poles: 4, cooling: 'IC411 自扇冷', sampleName: '高效率三相异步电动机',
  },
  {
    id: 'm3', motorNo: '2026DJ0019', model: 'YKK500-4', manufacturer: '湘潭电机股份有限公司',
    serialNo: 'XT25-11037', ratedVoltage: 10000, ratedCurrent: 63.5, ratedPower: 900, ratedFreq: 50,
    ratedSpeed: 1490, ratedPF: 0.88, wiring: 'Y', insulation: 'F 级', duty: 'S1 连续工作制',
    ip: 'IP54', poles: 4, cooling: 'IC611 空空冷', sampleName: '高压三相异步电动机',
  },
  {
    id: 'm4', motorNo: '2026DJ0008', model: 'YE3-200L2-2', manufacturer: '江苏大中电机股份有限公司',
    serialNo: 'DZ26-00291', ratedVoltage: 380, ratedCurrent: 67.9, ratedPower: 37, ratedFreq: 50,
    ratedSpeed: 2950, ratedPF: 0.90, wiring: '△', insulation: 'F 级', duty: 'S1 连续工作制',
    ip: 'IP55', poles: 2, cooling: 'IC411 自扇冷', sampleName: '三相异步电动机',
  },
]

// ============================================================
// 历史试验库（Mock，对应 Access 试验数据库）
// ============================================================
export const HISTORY_TESTS: HistoryTest[] = [
  { id: 'h1', testNo: '2026-0716-01', itemLabel: '空载特性试验', motorModel: 'YE3-315S-4', motorNo: '2026DJ0031', operator: '工程师 01', date: '2026-07-16 09:15', duration: '00:42:18', points: 12, result: '合格', ratedPower: 110, ratedVoltage: 380, efficiency: 94.8 },
  { id: 'h2', testNo: '2026-0716-02', itemLabel: '负载特性试验', motorModel: 'YE3-315S-4', motorNo: '2026DJ0031', operator: '工程师 01', date: '2026-07-16 14:02', duration: '01:15:44', points: 16, result: '合格', ratedPower: 110, ratedVoltage: 380, efficiency: 94.6, tempRise: 72.3 },
  { id: 'h3', testNo: '2026-0715-01', itemLabel: '额定工况负载温升记录', motorModel: 'YE4-280M-4', motorNo: '2026DJ0027', operator: '工程师 02', date: '2026-07-15 08:30', duration: '03:12:05', points: 40, result: '合格', ratedPower: 90, ratedVoltage: 380, tempRise: 68.9 },
  { id: 'h4', testNo: '2026-0714-03', itemLabel: '工频堵转试验', motorModel: 'YE3-315S-4', motorNo: '2026DJ0031', operator: '工程师 01', date: '2026-07-14 16:20', duration: '00:08:32', points: 9, result: '合格', ratedPower: 110, ratedVoltage: 380 },
  { id: 'h5', testNo: '2026-0714-01', itemLabel: '空载特性试验', motorModel: 'YE3-200L2-2', motorNo: '2026DJ0008', operator: '工程师 03', date: '2026-07-14 10:05', duration: '00:31:57', points: 11, result: '待判定', ratedPower: 37, ratedVoltage: 380 },
  { id: 'h6', testNo: '2026-0713-02', itemLabel: 'TN试验', motorModel: 'YE4-280M-4', motorNo: '2026DJ0027', operator: '工程师 02', date: '2026-07-13 13:40', duration: '00:56:11', points: 21, result: '合格', ratedPower: 90, ratedVoltage: 380, efficiency: 95.1 },
  { id: 'h7', testNo: '2026-0712-01', itemLabel: '直流电阻和绝缘电阻测量', motorModel: 'YKK500-4', motorNo: '2026DJ0019', operator: '工程师 01', date: '2026-07-12 09:00', duration: '00:18:26', points: 6, result: '合格', ratedPower: 900, ratedVoltage: 10000 },
  { id: 'h8', testNo: '2026-0711-04', itemLabel: '振动和噪声试验', motorModel: 'YE3-315S-4', motorNo: '2026DJ0031', operator: '工程师 03', date: '2026-07-11 15:45', duration: '00:24:39', points: 8, result: '不合格', ratedPower: 110, ratedVoltage: 380 },
  { id: 'h9', testNo: '2026-0710-02', itemLabel: '低频堵转试验', motorModel: 'YE4-280M-4', motorNo: '2026DJ0027', operator: '工程师 02', date: '2026-07-10 11:12', duration: '00:11:03', points: 7, result: '合格', ratedPower: 90, ratedVoltage: 380 },
  { id: 'h10', testNo: '2026-0709-01', itemLabel: '效率分析 · A法数据分析', motorModel: 'YE3-315S-4', motorNo: '2026DJ0031', operator: '工程师 01', date: '2026-07-09 10:30', duration: '01:02:47', points: 14, result: '合格', ratedPower: 110, ratedVoltage: 380, efficiency: 94.9 },
]

// ============================================================
// 报表模板 —— 对应原"报表输出"（Aspose.Cells / FlexCell 模板）
// ============================================================
export const REPORT_TEMPLATES: ReportTemplate[] = [
  { id: 'r1', name: '型式试验报告（完整）', scope: '全部试验项目汇总', format: 'Word', lastUsed: '2026-07-16' },
  { id: 'r2', name: '空载特性试验记录表', scope: '空载特性试验', format: 'Excel', lastUsed: '2026-07-16' },
  { id: 'r3', name: '负载特性试验记录表', scope: '负载特性试验', format: 'Excel', lastUsed: '2026-07-15' },
  { id: 'r4', name: '温升试验报告', scope: '温升试验（额定/低频）', format: 'Word', lastUsed: '2026-07-15' },
  { id: 'r5', name: '堵转特性试验记录表', scope: '堵转特性试验', format: 'Excel', lastUsed: '2026-07-14' },
  { id: 'r6', name: '效率特性曲线报告', scope: '效率分析 A/B/E 法', format: 'PDF', lastUsed: '2026-07-09' },
  { id: 'r7', name: '出厂检验合格证', scope: '出厂检验', format: 'PDF', lastUsed: '2026-07-08' },
]

export const USERS: UserInfo[] = [
  { name: '工程师 01', role: '试验工程师', permission: '操作员' },
  { name: '工程师 02', role: '试验工程师', permission: '操作员' },
  { name: '工程师 03', role: '数据分析', permission: '分析员' },
  { name: '超级用户', role: '系统管理员', permission: '管理员' },
]

export const INITIAL_ALARMS: Alarm[] = [
  { id: 1, time: '09:12:47', level: '提示', source: '试验流程', code: 'PROC-301', message: '试验步骤切换：电压点 3 → 电压点 4', status: 'recovered', ackTime: '09:12:47' },
  { id: 2, time: '09:05:18', level: '警告', source: '温度采集仪', code: 'TEMP-101', message: '定子 V 相绕组温度接近报警上限（68.5℃ / 限值 80℃）', status: 'recovered', ackTime: '09:06:02' },
  { id: 3, time: '08:58:33', level: '提示', source: 'PLC 控制器', code: 'PLC-110', message: '陪试机励磁投入完成，允许加载', status: 'ack', ackTime: '08:59:10' },
  { id: 4, time: '08:41:05', level: '警告', source: '功率分析仪 1', code: 'PWR-203', message: '输入功率波动超过设定带宽 ±2%', status: 'recovered', ackTime: '08:43:26' },
]

export const COMPANY = '上海电气集团上海电机厂有限公司'
export const SOFTWARE = '三相异步电机测试软件'
export const VERSION = 'V5.2.1 (Web 概念版)'
