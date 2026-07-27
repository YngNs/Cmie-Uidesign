import { create } from 'zustand'
import {
  CHANNELS, CHANNEL_MAP, DEVICES, DEFAULT_CURVE_KEYS, HISTORY_TESTS,
  INITIAL_ALARMS, MOTOR_MODELS, USERS,
} from './data'
import { TEST_ITEM_MAP, createProjectProgress } from './testItems'
import type {
  Alarm, CreateProjectResult, DeviceInfo, HistoryTest, MotorModel, MotorSnapshot,
  NewProjectErrors, NewProjectInput, RecordPoint, Sample, TestItemStatus,
  TestProject, TestRunState, UserInfo,
} from './types'

export function fmtTime(d: Date): string {
  const p = (x: number) => String(x).padStart(2, '0')
  return `${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`
}

export function fmtElapsed(sec: number): string {
  const p = (x: number) => String(x).padStart(2, '0')
  const h = Math.floor(sec / 3600)
  const m = Math.floor((sec % 3600) / 60)
  const s = Math.floor(sec % 60)
  return `${p(h)}:${p(m)}:${p(s)}`
}

export function isContextSwitchAllowed(state: TestRunState): boolean {
  return state === 'idle' || state === 'stopped'
}

export interface Toast {
  id: number
  text: string
  kind: 'ok' | 'warn' | 'err'
}

export interface TestStore {
  runState: TestRunState
  t: number
  samples: Sample[]
  latest: Record<string, number>
  records: RecordPoint[]
  projects: TestProject[]
  currentProjectId: string
  user: UserInfo
  startedAt: string
  devices: DeviceInfo[]
  alarms: Alarm[]
  history: HistoryTest[]
  compareIds: string[]
  curveKeys: string[]
  tempMask: Record<string, boolean>
  toasts: Toast[]

  tick: () => void
  startTest: () => void
  pauseTest: () => void
  resumeTest: () => void
  stopTest: () => void
  estop: () => void
  resetEstop: () => void
  recordPoint: (note?: string) => void
  deleteRecord: (seq: number) => void
  clearRecords: () => void
  ackAlarm: (id: number) => void
  ackAllAlarms: () => void
  connectDevice: (id: string) => void
  disconnectDevice: (id: string) => void
  createProject: (input: NewProjectInput) => CreateProjectResult
  selectProject: (id: string) => boolean
  selectTestItem: (id: string) => boolean
  setTestItemStatus: (itemId: string, status: TestItemStatus, projectId?: string) => boolean
  setUser: (name: string) => void
  toggleCurveKey: (key: string) => void
  toggleTemp: (key: string) => void
  toggleCompare: (id: string) => void
  newTestSession: () => void
  toast: (text: string, kind?: Toast['kind']) => void
  dismissToast: (id: number) => void
}

const INITIAL_PROJECT_NUMBERS = ['2026-0716-01', '2026-0715-01', '2026-0712-01', '2026-0714-01'] as const
const INITIAL_PROJECT_OPERATORS = ['工程师 01', '工程师 02', '工程师 01', '工程师 03'] as const
const INITIAL_PROJECT_TIME = '2026-07-16T09:00:00.000Z'

function snapshotModel(model: MotorModel): MotorSnapshot {
  const { id: _id, ...snapshot } = model
  return { ...snapshot }
}

export function createInitialProjects(): TestProject[] {
  return MOTOR_MODELS.map((model, index) => {
    const itemProgress = createProjectProgress(INITIAL_PROJECT_TIME)
    if (index === 0) {
      for (const id of ['resistance', 'noload', 'eff-a']) {
        itemProgress[id] = {
          status: 'completed',
          updatedAt: INITIAL_PROJECT_TIME,
          completedAt: INITIAL_PROJECT_TIME,
        }
      }
      itemProgress.load = { status: 'in-progress', updatedAt: INITIAL_PROJECT_TIME }
    }
    return {
      id: `project-${model.id}`,
      projectNo: INITIAL_PROJECT_NUMBERS[index],
      createdAt: INITIAL_PROJECT_TIME,
      status: 'active',
      operator: INITIAL_PROJECT_OPERATORS[index],
      sourceModelId: model.id,
      motorSnapshot: snapshotModel(model),
      activeItemId: index === 0 ? 'load' : 'resistance',
      itemProgress,
    }
  })
}

function trimSnapshot(snapshot: MotorSnapshot): MotorSnapshot {
  return {
    ...snapshot,
    motorNo: snapshot.motorNo.trim(),
    model: snapshot.model.trim(),
    manufacturer: snapshot.manufacturer.trim(),
    serialNo: snapshot.serialNo.trim(),
    wiring: snapshot.wiring.trim(),
    insulation: snapshot.insulation.trim(),
    duty: snapshot.duty.trim(),
    ip: snapshot.ip.trim(),
    cooling: snapshot.cooling.trim(),
    sampleName: snapshot.sampleName.trim(),
  }
}

export function normalizeNewProjectInput(input: NewProjectInput): NewProjectInput {
  return {
    projectNo: input.projectNo.trim(),
    operator: input.operator.trim(),
    sourceModelId: input.sourceModelId.trim(),
    motorSnapshot: trimSnapshot({ ...input.motorSnapshot }),
  }
}

export function validateNewProject(input: NewProjectInput, projects: readonly TestProject[]): NewProjectErrors {
  const value = normalizeNewProjectInput(input)
  const motor = value.motorSnapshot
  const errors: NewProjectErrors = {}
  const required: [keyof Pick<MotorSnapshot, 'sampleName' | 'motorNo' | 'model' | 'manufacturer' | 'serialNo' | 'wiring' | 'insulation'>, string][] = [
    ['sampleName', '请输入样品名称'],
    ['motorNo', '请输入电机编号'],
    ['model', '请输入电机型号'],
    ['manufacturer', '请输入生产厂家'],
    ['serialNo', '请输入出厂编号'],
    ['wiring', '请输入接线方式'],
    ['insulation', '请输入绝缘等级'],
  ]

  if (!value.projectNo) errors.projectNo = '请输入项目/试验编号'
  else if (projects.some(project => project.projectNo.toLocaleLowerCase() === value.projectNo.toLocaleLowerCase())) {
    errors.projectNo = '项目/试验编号已存在'
  }
  if (!value.operator) errors.operator = '请输入操作员'
  if (!value.sourceModelId || !MOTOR_MODELS.some(model => model.id === value.sourceModelId)) {
    errors.sourceModelId = '请选择有效的型号库条目'
  }
  for (const [key, message] of required) if (!motor[key]) errors[key] = message

  const positiveNumbers: [keyof Pick<MotorSnapshot, 'ratedVoltage' | 'ratedCurrent' | 'ratedPower' | 'ratedFreq' | 'ratedSpeed'>, string][] = [
    ['ratedVoltage', '额定电压必须为正数'],
    ['ratedCurrent', '额定电流必须为正数'],
    ['ratedPower', '额定功率必须为正数'],
    ['ratedFreq', '额定频率必须为正数'],
    ['ratedSpeed', '额定转速必须为正数'],
  ]
  for (const [key, message] of positiveNumbers) {
    if (!Number.isFinite(motor[key]) || motor[key] <= 0) errors[key] = message
  }
  if (!Number.isFinite(motor.ratedPF) || motor.ratedPF <= 0 || motor.ratedPF > 1) {
    errors.ratedPF = '额定功率因数必须大于 0 且不超过 1'
  }
  if (!Number.isInteger(motor.poles) || motor.poles <= 0 || motor.poles % 2 !== 0) {
    errors.poles = '电机极数必须为正偶数'
  }
  return errors
}

let toastSeq = 1
let alarmSeq = 100
let projectSeq = 1
let simTimer: ReturnType<typeof setInterval> | null = null

function simulateFrame(motor: MotorSnapshot, t: number, prev: Record<string, number>): Record<string, number> {
  const rnd = (a: number) => (Math.random() - 0.5) * 2 * a
  const V = Math.min(motor.ratedVoltage, 380)
  const I = Math.min(motor.ratedCurrent, 260)
  const P1 = Math.min(motor.ratedPower, 150)
  const ramp = Math.min(1, t / 20)
  const surge = t < 6 ? 1 + (6 - t) / 6 * 0.9 : 1
  const loadRatio = 0.76 + Math.sin(t / 47) * 0.02
  const u = V * (0.985 + Math.sin(t / 31) * 0.004) * (0.4 + 0.6 * Math.min(1, t / 3))
  const iv = I * loadRatio * surge * (0.3 + 0.7 * ramp)
  const p = P1 * loadRatio * (0.35 + 0.65 * ramp)
  const n = motor.ratedSpeed * 0.998 * ramp * (1 + rnd(0.0006))
  const pf = Math.min(0.99, motor.ratedPF * (0.55 + 0.45 * ramp) + rnd(0.004))
  const pshaft = p * 0.936
  const tq = n > 50 ? 9550 * pshaft / n : 0
  const warm = (target: number, tau: number, key: string, base: number) => {
    const eq = base + (target - base) * (1 - Math.exp(-t / tau))
    return prev[key] !== undefined ? prev[key] + (eq - prev[key]) * 0.08 + rnd(0.06) : eq
  }
  const amb = 26 + Math.sin(t / 300) * 0.5
  const v: Record<string, number> = {
    Uab: u * (1 + rnd(0.001)), Ubc: u * (1 + rnd(0.001)), Uca: u * (1 + rnd(0.001)),
    Ia: iv * (1 + rnd(0.004)), Ib: iv * (1 + rnd(0.004)), Ic: iv * (1 + rnd(0.004)),
    P: p * (1 + rnd(0.004)), Q: p * Math.tan(Math.acos(Math.max(0.2, pf))) * (1 + rnd(0.006)),
    PF: pf, f: 50 + rnd(0.015), n, Pshaft: pshaft * (1 + rnd(0.004)), T: tq * (1 + rnd(0.005)),
    T_w1: warm(72, 240, 'T_w1', 30), T_w2: warm(74, 260, 'T_w2', 30), T_w3: warm(71, 250, 'T_w3', 30),
    T_w4: warm(70, 255, 'T_w4', 30), T_w5: warm(69, 245, 'T_w5', 30), T_w6: warm(70, 252, 'T_w6', 30),
    T_de: warm(58, 300, 'T_de', 32), T_nde: warm(55, 320, 'T_nde', 32),
    T_in: amb + 2 + rnd(0.1), T_out: warm(48, 200, 'T_out', 30),
    T_core: warm(76, 280, 'T_core', 30), T_surf: warm(52, 260, 'T_surf', 30), T_amb: amb,
  }
  v.Uavg = (v.Uab + v.Ubc + v.Uca) / 3
  v.Iavg = (v.Ia + v.Ib + v.Ic) / 3
  v.T_stator_avg = (v.T_w1 + v.T_w2 + v.T_w3) / 3
  v.T_rotor_avg = (v.T_w4 + v.T_w5 + v.T_w6) / 3
  return v
}

function zeroFrame(): Record<string, number> {
  const values: Record<string, number> = {}
  for (const channel of CHANNELS) values[channel.key] = channel.group === '温度' ? 26 + Math.random() : 0
  values.T_amb = 26
  values.T_stator_avg = (values.T_w1 + values.T_w2 + values.T_w3) / 3
  values.T_rotor_avg = (values.T_w4 + values.T_w5 + values.T_w6) / 3
  return values
}

function resetSessionState() {
  return { runState: 'idle' as const, t: 0, samples: [], records: [], startedAt: '', latest: zeroFrame() }
}

function replaceProject(projects: readonly TestProject[], project: TestProject): TestProject[] {
  return projects.map(candidate => candidate.id === project.id ? project : candidate)
}

export const selectCurrentProject = (state: TestStore): TestProject =>
  state.projects.find(project => project.id === state.currentProjectId) ?? state.projects[0]

const initialProjects = createInitialProjects()

export const useTestStore = create<TestStore>((set, get) => ({
  ...resetSessionState(),
  projects: initialProjects,
  currentProjectId: initialProjects[0].id,
  user: USERS[0],
  devices: DEVICES.map(device => ({ ...device })),
  alarms: INITIAL_ALARMS.map(alarm => ({ ...alarm })),
  history: HISTORY_TESTS.map(history => ({ ...history })),
  compareIds: ['h1', 'h10'],
  curveKeys: [...DEFAULT_CURVE_KEYS],
  tempMask: {},
  toasts: [],

  tick: () => {
    const state = get()
    if (state.runState === 'running') {
      const project = selectCurrentProject(state)
      const t = state.t + 0.5
      const values = simulateFrame(project.motorSnapshot, t, state.latest)
      const sample: Sample = { t, time: fmtTime(new Date()), values }
      const samples = [...state.samples, sample]
      if (samples.length > 720) samples.shift()
      const alarms = [...state.alarms]
      if (values.T_w2 > 68 && !alarms.some(alarm => alarm.code === 'TEMP-102')) {
        alarms.unshift({ id: ++alarmSeq, time: sample.time, level: '警告', source: '温度采集仪', code: 'TEMP-102', message: `定子 V 相绕组温度 ${values.T_w2.toFixed(1)}℃ 接近报警上限 80℃`, status: 'active' })
        get().toast('定子 V 相绕组温度接近报警上限', 'warn')
      }
      if (Math.random() < 0.004) {
        alarms.unshift({ id: ++alarmSeq, time: sample.time, level: '提示', source: '电网监测', code: 'GRID-102', message: `电网电压波动 ΔU=${(Math.random() * 6 + 2).toFixed(1)}V，已自动恢复`, status: 'active' })
      }
      set({ t, latest: values, samples, alarms })
    } else if (state.runState === 'stopped') {
      const decay = (value: number) => Math.abs(value) < 0.5 ? 0 : value * 0.6
      const values: Record<string, number> = {}
      for (const key of Object.keys(state.latest)) {
        values[key] = CHANNEL_MAP[key]?.group === '温度' ? state.latest[key] : decay(state.latest[key])
      }
      set({ latest: values })
    }
  },

  startTest: () => {
    const state = get()
    if (state.runState === 'estop') { get().toast('急停锁定中，请先复位急停', 'err'); return }
    if (state.devices.some(device => device.id !== 'mw100' && device.status !== 'online')) {
      get().toast('存在离线设备，请检查通讯设置', 'err')
      return
    }
    const project = selectCurrentProject(state)
    const currentProgress = project.itemProgress[project.activeItemId]
    const projects = currentProgress?.status === 'not-started'
      ? replaceProject(state.projects, {
          ...project,
          itemProgress: {
            ...project.itemProgress,
            [project.activeItemId]: { status: 'in-progress', updatedAt: new Date().toISOString() },
          },
        })
      : state.projects
    set({
      ...resetSessionState(),
      projects,
      runState: 'running',
      startedAt: fmtTime(new Date()),
    })
    get().toast('试验已开始，数据采集中...')
  },
  pauseTest: () => {
    if (get().runState === 'running') { set({ runState: 'paused' }); get().toast('试验已暂停，数据保持', 'warn') }
  },
  resumeTest: () => {
    if (get().runState === 'paused') { set({ runState: 'running' }); get().toast('试验继续') }
  },
  stopTest: () => {
    if (!['running', 'paused'].includes(get().runState)) return
    set({ runState: 'stopped' })
    const state = get()
    const alarms = [{
      id: ++alarmSeq,
      time: fmtTime(new Date()),
      level: '提示' as const,
      source: '试验流程',
      code: 'PROC-900',
      message: `试验停止，共记录 ${state.records.length} 个数据点`,
      status: 'ack' as const,
    }, ...state.alarms]
    set({ alarms })
    get().toast('试验已停止，数据已保存')
  },
  estop: () => {
    const state = get()
    if (state.runState === 'idle' || state.runState === 'estop') return
    const temperatures = Object.fromEntries(Object.entries(state.latest).filter(([key]) => CHANNEL_MAP[key]?.group === '温度'))
    const alarm: Alarm = { id: ++alarmSeq, time: fmtTime(new Date()), level: '严重', source: '急停回路', code: 'ESTOP-001', message: '急停按钮动作，主回路已断开！', status: 'active' }
    set({ runState: 'estop', latest: { ...zeroFrame(), ...temperatures }, alarms: [alarm, ...state.alarms] })
    get().toast('急停动作！主回路断开', 'err')
  },
  resetEstop: () => {
    if (get().runState !== 'estop') return
    set({ runState: 'idle', t: 0 })
    get().toast('急停已复位，系统回到待机')
  },

  recordPoint: (note) => {
    const state = get()
    if (state.runState !== 'running' && state.runState !== 'paused') {
      get().toast('试验未运行，无法记录', 'warn')
      return
    }
    const seq = state.records.length + 1
    const record: RecordPoint = { seq, time: fmtTime(new Date()), t: state.t, values: { ...state.latest }, note }
    set({ records: [...state.records, record] })
    get().toast(`已记录第 ${seq} 点（${fmtElapsed(state.t)}）`)
  },
  deleteRecord: (seq) => set({ records: get().records.filter(record => record.seq !== seq).map((record, index) => ({ ...record, seq: index + 1 })) }),
  clearRecords: () => set({ records: [] }),

  ackAlarm: (id) => set({ alarms: get().alarms.map(alarm => alarm.id === id && alarm.status === 'active' ? { ...alarm, status: 'ack' as const, ackTime: fmtTime(new Date()) } : alarm) }),
  ackAllAlarms: () => set({ alarms: get().alarms.map(alarm => alarm.status === 'active' ? { ...alarm, status: 'ack' as const, ackTime: fmtTime(new Date()) } : alarm) }),

  connectDevice: (id) => {
    set({ devices: get().devices.map(device => device.id === id ? { ...device, status: 'connecting' as const } : device) })
    setTimeout(() => {
      set({ devices: get().devices.map(device => device.id === id ? { ...device, status: 'online' as const, latencyMs: 6 + Math.round(Math.random() * 40) } : device) })
      get().toast('设备连接成功')
    }, 1200)
  },
  disconnectDevice: (id) => {
    set({ devices: get().devices.map(device => device.id === id ? { ...device, status: 'offline' as const, latencyMs: 0 } : device) })
    get().toast('设备已断开', 'warn')
  },

  createProject: (input) => {
    const state = get()
    if (!isContextSwitchAllowed(state.runState)) {
      return { ok: false, errors: { _form: '采集运行、暂停或急停时不能新建项目' } }
    }
    const normalized = normalizeNewProjectInput(input)
    const errors = validateNewProject(normalized, state.projects)
    if (Object.keys(errors).length > 0) return { ok: false, errors }
    const now = new Date().toISOString()
    const project: TestProject = {
      id: `project-created-${projectSeq++}`,
      projectNo: normalized.projectNo,
      createdAt: now,
      status: 'active',
      operator: normalized.operator,
      sourceModelId: normalized.sourceModelId,
      motorSnapshot: { ...normalized.motorSnapshot },
      activeItemId: 'resistance',
      itemProgress: createProjectProgress(now),
    }
    set({ projects: [...state.projects, project], currentProjectId: project.id, ...resetSessionState() })
    get().toast(`项目 ${project.projectNo} 已创建并切换`)
    return { ok: true, project }
  },
  selectProject: (id) => {
    const state = get()
    if (!isContextSwitchAllowed(state.runState) || !state.projects.some(project => project.id === id)) return false
    if (id === state.currentProjectId) return true
    set({ currentProjectId: id, ...resetSessionState() })
    get().toast(`已切换项目 ${selectCurrentProject(get()).projectNo}`)
    return true
  },
  selectTestItem: (id) => {
    const state = get()
    if (!isContextSwitchAllowed(state.runState) || !TEST_ITEM_MAP.has(id)) return false
    const project = selectCurrentProject(state)
    if (project.activeItemId === id) return true
    set({ projects: replaceProject(state.projects, { ...project, activeItemId: id }), ...resetSessionState() })
    get().toast(`已选择试验：${TEST_ITEM_MAP.get(id)?.label ?? id}`)
    return true
  },
  setTestItemStatus: (itemId, status, projectId) => {
    const state = get()
    if (!TEST_ITEM_MAP.has(itemId)) return false
    const targetId = projectId ?? state.currentProjectId
    const project = state.projects.find(candidate => candidate.id === targetId)
    if (!project) return false
    const now = new Date().toISOString()
    const nextProgress = status === 'completed'
      ? { status, updatedAt: now, completedAt: now }
      : { status, updatedAt: now }
    set({ projects: replaceProject(state.projects, {
      ...project,
      itemProgress: { ...project.itemProgress, [itemId]: nextProgress },
    }) })
    return true
  },
  setUser: (name) => {
    const user = USERS.find(candidate => candidate.name === name)
    if (user) { set({ user }); get().toast(`已切换用户：${user.name}`) }
  },
  toggleCurveKey: (key) => {
    const keys = get().curveKeys
    set({ curveKeys: keys.includes(key) ? keys.filter(candidate => candidate !== key) : [...keys, key] })
  },
  toggleTemp: (key) => set({ tempMask: { ...get().tempMask, [key]: !get().tempMask[key] } }),
  toggleCompare: (id) => {
    const ids = get().compareIds
    if (ids.includes(id)) set({ compareIds: ids.filter(candidate => candidate !== id) })
    else if (ids.length < 4) set({ compareIds: [...ids, id] })
    else get().toast('最多同时对比 4 组试验', 'warn')
  },
  newTestSession: () => {
    set(resetSessionState())
    get().toast('已重置模拟采集段')
  },
  toast: (text, kind = 'ok') => {
    const id = toastSeq++
    set({ toasts: [...get().toasts, { id, text, kind }] })
    setTimeout(() => get().dismissToast(id), 3200)
  },
  dismissToast: (id) => set({ toasts: get().toasts.filter(toast => toast.id !== id) }),
}))

export function startSimulator() {
  if (simTimer) return
  simTimer = setInterval(() => useTestStore.getState().tick(), 500)
}

export function isTempVisible(mask: Record<string, boolean>, key: string) {
  return mask[key] !== false
}

export { CHANNEL_MAP, fmtTime as formatTime }
