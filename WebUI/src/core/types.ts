// ============================================================
// 核心业务类型定义 —— 对应原 VB 软件（FrmMain / ModGlobeVariable / clsTestPara）
// ============================================================

/** 试验运行状态机：空闲 / 运行中 / 已暂停 / 已停止 / 急停 */
export type TestRunState = 'idle' | 'running' | 'paused' | 'stopped' | 'estop'

export const RUN_STATE_LABEL: Record<TestRunState, string> = {
  idle: '待机',
  running: '试验运行中',
  paused: '已暂停',
  stopped: '已停止',
  estop: '急停锁定',
}

/** 采集通道定义（实时数据页字段，对应原软件"实时数据"标签页） */
export interface ChannelDef {
  key: string
  label: string
  unit: string
  group: '电压' | '电流' | '电参数' | '机械参数' | '温度'
  color: string
  min: number
  max: number
  precision: number
}

/** 单帧采样值 */
export interface Sample {
  /** 相对试验开始的秒数 */
  t: number
  time: string
  values: Record<string, number>
}

/** 用户点击"记录"保存的数据点 */
export interface RecordPoint {
  seq: number
  time: string
  t: number
  values: Record<string, number>
  note?: string
}

export type AlarmLevel = '提示' | '警告' | '严重'
export type AlarmStatus = 'active' | 'ack' | 'recovered'

export interface Alarm {
  id: number
  time: string
  level: AlarmLevel
  source: string
  code: string
  message: string
  status: AlarmStatus
  ackTime?: string
}

export type DeviceStatus = 'online' | 'offline' | 'connecting'

export interface DeviceInfo {
  id: string
  name: string
  model: string
  role: string
  protocol: string
  address: string
  status: DeviceStatus
  latencyMs: number
}

/** 电机型号参数（对应原"新建试验 / 进入试验"表单与 frmTypeManager） */
export interface MotorModel {
  id: string
  motorNo: string        // 电机编号
  model: string          // 电机型号
  manufacturer: string   // 生产厂家
  serialNo: string       // 出厂编号
  ratedVoltage: number   // 额定电压 V
  ratedCurrent: number   // 额定电流 A
  ratedPower: number     // 额定功率 kW
  ratedFreq: number      // 额定频率 Hz
  ratedSpeed: number     // 额定转速 rpm
  ratedPF: number        // 额定功率因数
  wiring: string         // 接法
  insulation: string     // 绝缘等级
  duty: string           // 运行工作制
  ip: string             // 防护等级
  poles: number          // 电机极数
  cooling: string        // 冷却方式
  sampleName: string     // 样品名称
}

/** 项目保存的是创建时的铭牌快照，不持有型号库内部 id。 */
export type MotorSnapshot = Omit<MotorModel, 'id'>

interface TestItemBase {
  id: string
  label: string
}

/** 目录只负责分组，不是可执行试验。 */
export interface TestItemGroup extends TestItemBase {
  kind: 'group'
  children: readonly TestItemNode[]
}

/** 只有叶子可以成为项目的当前试验。 */
export interface TestItemLeaf extends TestItemBase {
  kind: 'record' | 'analysis'
}

/** 试验项目树节点（对应原"试验项目"标签页树） */
export type TestItemNode = TestItemGroup | TestItemLeaf

export interface FlatTestItem extends TestItemLeaf {
  /** 从根节点到当前叶子的中文标签路径。 */
  path: string[]
  /** 所属目录 id，按从外到内排序。 */
  groupIds: string[]
}

export type TestItemStatus = 'not-started' | 'in-progress' | 'completed'

export interface TestItemProgress {
  status: TestItemStatus
  updatedAt: string
  completedAt?: string
}

export type ProjectProgress = Record<string, TestItemProgress>

export interface TestProject {
  id: string
  projectNo: string
  createdAt: string
  status: 'active' | 'completed' | 'archived'
  operator: string
  sourceModelId: string
  motorSnapshot: MotorSnapshot
  activeItemId: string
  itemProgress: ProjectProgress
}

export interface NewProjectInput {
  projectNo: string
  operator: string
  sourceModelId: string
  motorSnapshot: MotorSnapshot
}

export type NewProjectField =
  | 'projectNo'
  | 'operator'
  | 'sourceModelId'
  | 'sampleName'
  | 'motorNo'
  | 'model'
  | 'manufacturer'
  | 'serialNo'
  | 'ratedVoltage'
  | 'ratedCurrent'
  | 'ratedPower'
  | 'ratedFreq'
  | 'ratedSpeed'
  | 'ratedPF'
  | 'poles'
  | 'wiring'
  | 'insulation'
  | '_form'

export type NewProjectErrors = Partial<Record<NewProjectField, string>>

export type CreateProjectResult =
  | { ok: true; project: TestProject }
  | { ok: false; errors: NewProjectErrors }

/** 历史试验（对应数据库中的已完成试验） */
export interface HistoryTest {
  id: string
  testNo: string
  itemLabel: string
  motorModel: string
  motorNo: string
  operator: string
  date: string
  duration: string
  points: number
  result: '合格' | '不合格' | '待判定'
  ratedPower: number
  ratedVoltage: number
  efficiency?: number
  tempRise?: number
}

export interface ReportTemplate {
  id: string
  name: string
  scope: string
  format: 'Excel' | 'Word' | 'PDF'
  lastUsed: string
}

export interface UserInfo {
  name: string
  role: string
  permission: string
}
