import { useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import {
  Activity, AlertTriangle, BarChart3, Check, ChevronDown, ChevronRight, ClipboardList,
  Download, FileBarChart, Folder, Gauge, History, LayoutDashboard, Pause, Play,
  Plus, Radio, RefreshCw, Save, Settings, ShieldCheck, Square, Trash2, Wrench, X,
} from 'lucide-react'
import type { EChartsOption } from 'echarts'
import { Chart } from './core/Chart'
import { CHANNEL_MAP, DEFAULT_CURVE_KEYS, MOTOR_MODELS, RECORD_COLUMNS, REPORT_TEMPLATES, TEST_TREE } from './core/data'
import { MEASUREMENT_GROUPS, STAT_METRIC_KEYS, getTrendAxisLayout, getWindowAverage } from './core/metrics'
import { fmtElapsed, isContextSwitchAllowed, selectCurrentProject, useTestStore } from './core/store'
import { TEST_ITEMS, TEST_ITEM_MAP, countCompletedItems, getGroupProgress } from './core/testItems'
import type {
  CreateProjectResult, MotorModel, MotorSnapshot, NewProjectErrors, NewProjectField,
  NewProjectInput, ProjectProgress, RecordPoint, TestItemNode, TestItemStatus, TestProject, TestRunState,
} from './core/types'

const SCHEMES = [
  { id: 's1', number: '01', name: '精密实验台', short: '实验台', description: '项目树、记录表和特性曲线同屏', tone: 'lab' },
  { id: 's2', number: '02', name: '暗场控制室', short: '控制室', description: '波形墙、告警轨和固定命令台', tone: 'control' },
] as const

type SchemeId = (typeof SCHEMES)[number]['id']

function getInitialScheme(): SchemeId {
  const requested = new URLSearchParams(window.location.search).get('scheme') ?? localStorage.getItem('motor-scheme')
  return SCHEMES.some(scheme => scheme.id === requested) ? requested as SchemeId : 's1'
}

const NAV = [
  { id: 'project', label: '项目概览', icon: LayoutDashboard },
  { id: 'monitor', label: '实时监控', icon: Activity },
  { id: 'tests', label: '试验执行', icon: ClipboardList },
  { id: 'records', label: '记录与分析', icon: BarChart3 },
  { id: 'reports', label: '报告', icon: FileBarChart },
  { id: 'settings', label: '设备与设置', icon: Settings },
] as const

type ViewId = (typeof NAV)[number]['id']
type PendingAction = { title: string; body: string; danger?: boolean; onConfirm: () => void }

const FOCUSABLE_SELECTOR = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',')

const STATUS_LABEL: Record<TestItemStatus, string> = {
  'not-started': '未开始',
  'in-progress': '进行中',
  completed: '已完成',
}

function runLabel(state: TestRunState) {
  return { idle: '待机', running: '采集中', paused: '已暂停', stopped: '已停止', estop: '急停锁定' }[state]
}

function format(key: string, value: number | undefined) {
  if (!Number.isFinite(value)) return '--'
  const channel = CHANNEL_MAP[key]
  return (value as number).toFixed(channel?.precision ?? 2)
}

function snapshotModel(model: MotorModel): MotorSnapshot {
  const { id: _id, ...snapshot } = model
  return { ...snapshot }
}

function useModalKeyboard(dialogRef: React.RefObject<HTMLElement>, onClose: () => void) {
  const closeRef = useRef(onClose)
  const returnFocusRef = useRef<HTMLElement | null>(document.activeElement instanceof HTMLElement ? document.activeElement : null)
  closeRef.current = onClose

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    const focusFrame = requestAnimationFrame(() => {
      const initialFocus = dialog.querySelector<HTMLElement>('[data-dialog-initial-focus]')
        ?? dialog.querySelector<HTMLElement>(FOCUSABLE_SELECTOR)
      const focusTarget = initialFocus ?? dialog
      focusTarget.focus()
    })
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        closeRef.current()
        return
      }
      if (event.key !== 'Tab') return
      const focusable = [...dialog.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)]
      if (focusable.length === 0) {
        event.preventDefault()
        dialog.focus()
        return
      }
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }
    dialog.addEventListener('keydown', handleKeyDown)
    return () => {
      cancelAnimationFrame(focusFrame)
      dialog.removeEventListener('keydown', handleKeyDown)
      returnFocusRef.current?.focus()
    }
  }, [dialogRef])
}

export default function App() {
  const location = useLocation()
  const navigate = useNavigate()
  const currentProject = useTestStore(selectCurrentProject)
  const [schemeId, setSchemeId] = useState<SchemeId>(getInitialScheme)
  const [pending, setPending] = useState<PendingAction | null>(null)
  const path = location.pathname.replace(/^\//, '')
  const view: ViewId = NAV.some(item => item.id === path) ? path as ViewId : 'monitor'

  useEffect(() => {
    if (!NAV.some(item => item.id === path)) navigate('/monitor', { replace: true })
  }, [navigate, path])

  const scheme = SCHEMES.find(item => item.id === schemeId) ?? SCHEMES[0]
  const changeScheme = (id: SchemeId) => {
    localStorage.setItem('motor-scheme', id)
    setSchemeId(id)
  }

  return (
    <main className={`app-shell scheme-${scheme.tone}`}>
      <header className="topbar">
        <div className="brand"><Gauge size={20} /><span>CMIE</span><b>三相异步电机试验平台</b></div>
        <div className="project-context">
          <span>当前项目</span>
          <strong>{currentProject.projectNo} / {currentProject.motorSnapshot.model}</strong>
          <span className="context-sep">|</span>
          <span>电机编号 {currentProject.motorSnapshot.motorNo}</span>
        </div>
        <div className="topbar-actions"><StatusPill /><SchemeMenu selected={schemeId} onChange={changeScheme} /></div>
      </header>
      <div className="shell-body">
        <aside className="sidebar" aria-label="工作区导航">
          <div className="scheme-marker"><span>方案 {scheme.number}</span><b>{scheme.name}</b></div>
          <nav>{NAV.map(item => <NavButton key={item.id} item={item} active={view === item.id} onClick={() => navigate(`/${item.id}`)} />)}</nav>
          <div className="sidebar-footer"><ShieldCheck size={15} /><span>试验操作 · {currentProject.operator}</span></div>
        </aside>
        <section className="work-area">
          <PageHeader view={view} scheme={scheme.name} />
          <ViewOutlet view={view} scheme={schemeId} requestConfirm={setPending} />
        </section>
      </div>
      <MobileNav active={view} onChange={id => navigate(`/${id}`)} />
      <ToastLayer />
      {pending && (
        <ConfirmDialog
          action={pending}
          onCancel={() => setPending(null)}
          onConfirm={() => { pending.onConfirm(); setPending(null) }}
        />
      )}
    </main>
  )
}

function NavButton({ item, active, onClick }: { item: typeof NAV[number]; active: boolean; onClick: () => void }) {
  const Icon = item.icon
  return <button className={`nav-button ${active ? 'active' : ''}`} onClick={onClick}><Icon size={18} /><span>{item.label}</span></button>
}

function StatusPill() {
  const state = useTestStore(store => store.runState)
  return <span className={`status-pill state-${state}`}><i />{runLabel(state)}</span>
}

function SchemeMenu({ selected, onChange }: { selected: SchemeId; onChange: (id: SchemeId) => void }) {
  return (
    <label className="scheme-select">
      <span>界面方案</span>
      <select value={selected} onChange={event => onChange(event.target.value as SchemeId)} aria-label="切换界面方案">
        {SCHEMES.map(scheme => <option key={scheme.id} value={scheme.id}>{scheme.number} {scheme.name}</option>)}
      </select>
    </label>
  )
}

function PageHeader({ view, scheme }: { view: ViewId; scheme: string }) {
  const item = NAV.find(candidate => candidate.id === view)!
  const Icon = item.icon
  return (
    <div className="page-header">
      <div><p>试验项目 / {scheme}</p><h1><Icon size={22} />{item.label}</h1></div>
      <div className="header-meta"><span><Radio size={14} />采样 500 ms</span><span>2026-07-16 09:18:42</span></div>
    </div>
  )
}

function ViewOutlet({ view, scheme, requestConfirm }: { view: ViewId; scheme: SchemeId; requestConfirm: (action: PendingAction) => void }) {
  if (view === 'monitor') return <MonitorView scheme={scheme} requestConfirm={requestConfirm} />
  if (view === 'project') return <ProjectView />
  if (view === 'tests') return <TestsView requestConfirm={requestConfirm} />
  if (view === 'records') return <RecordsView requestConfirm={requestConfirm} />
  if (view === 'reports') return <ReportsView />
  return <SettingsView requestConfirm={requestConfirm} />
}

function MonitorView({ scheme, requestConfirm }: { scheme: SchemeId; requestConfirm: (action: PendingAction) => void }) {
  const store = useTestStore()
  const {
    latest, samples, records, runState, t, curveKeys, toggleCurveKey, recordPoint,
    startTest, pauseTest, resumeTest, stopTest, alarms, ackAlarm, devices, selectTestItem,
  } = store
  const project = selectCurrentProject(store)
  const activeItem = TEST_ITEM_MAP.get(project.activeItemId) ?? TEST_ITEMS[0]
  const dark = scheme === 's2'
  const didAutostart = useRef(false)
  const contextLocked = !isContextSwitchAllowed(runState)

  useEffect(() => {
    if (!didAutostart.current && new URLSearchParams(window.location.search).get('run') === '1' && runState === 'idle') {
      didAutostart.current = true
      startTest()
    }
  }, [runState, startTest])

  const chartConfig = useMemo(() => {
    const recentSamples = samples.slice(-80)
    const axisLayout = getTrendAxisLayout(curveKeys)
    const axisTextColor = dark ? '#94a3b8' : '#64748b'
    const splitLineColor = dark ? '#26354a' : '#e2e8f0'
    const yAxis = axisLayout.groups.length > 0
      ? axisLayout.groups.map((group, index) => ({
          type: 'value' as const,
          name: group.name,
          nameLocation: 'end' as const,
          nameGap: 8,
          min: group.min,
          max: group.max,
          position: group.position,
          offset: group.offset,
          axisLine: { show: true, lineStyle: { color: group.color } },
          axisTick: { show: true },
          axisLabel: { color: axisTextColor, fontSize: 10 },
          nameTextStyle: { color: group.color, fontSize: 10, fontWeight: 600 },
          splitLine: { show: index === 0, lineStyle: { color: splitLineColor } },
        }))
      : [{
          type: 'value' as const,
          axisLabel: { color: axisTextColor },
          splitLine: { lineStyle: { color: splitLineColor } },
        }]

    const option: EChartsOption = {
      animation: false,
      grid: { top: 48, right: axisLayout.right, bottom: 30, left: axisLayout.left },
      tooltip: { trigger: 'axis' },
      legend: {
        type: 'scroll',
        top: 2,
        left: 10,
        right: 10,
        textStyle: { color: dark ? '#cbd5e1' : '#475569' },
      },
      xAxis: {
        type: 'category',
        data: recentSamples.map(sample => sample.time),
        axisLabel: { color: axisTextColor },
      },
      yAxis,
      series: curveKeys.flatMap(key => {
        const channel = CHANNEL_MAP[key]
        if (!channel) return []
        return [{
          name: channel.label,
          type: 'line' as const,
          yAxisIndex: axisLayout.axisIndexByKey[key],
          showSymbol: false,
          smooth: true,
          data: recentSamples.map(sample => {
            const value = sample.values[key]
            return Number.isFinite(value) ? Number(value.toFixed(channel.precision)) : null
          }),
          lineStyle: { width: 2, color: channel.color },
          itemStyle: { color: channel.color },
        }]
      }),
    }

    return {
      option,
      minWidth: axisLayout.minWidth,
    }
  }, [curveKeys, dark, samples])

  return (
    <div className={`monitor-layout monitor-${scheme}`}>
      <section className="monitor-primary">
        <div className="command-strip">
          <div className="command-context">
            <span>当前试验</span>
            <TestItemSelector
              selectedId={project.activeItemId}
              progress={project.itemProgress}
              disabled={contextLocked}
              onSelect={selectTestItem}
            />
            <small>{project.projectNo} · {activeItem.kind === 'analysis' ? '分析项目' : '记录项目'}</small>
          </div>
          <span className="elapsed">已运行 {fmtElapsed(t)}</span>
          <ControlButtons
            state={runState}
            onStart={startTest}
            onPause={pauseTest}
            onResume={resumeTest}
            onRecord={recordPoint}
            onStop={() => requestConfirm({
              title: '停止当前试验？',
              body: '停止后保留已记录的采样点，设备输出将按模拟流程降为零。',
              danger: true,
              onConfirm: stopTest,
            })}
          />
        </div>

        <div className="measurement-groups" aria-label="实时测量信息">
          {MEASUREMENT_GROUPS.map(group => (
            <section className="measurement-group" key={group.label}>
              <div className="measurement-heading"><b>{group.label}</b><span>{group.keys.length} 项</span></div>
              <div className="metric-grid">
                {group.keys.map(key => CHANNEL_MAP[key] ? <MetricCard key={key} code={key} value={latest[key]} /> : null)}
              </div>
            </section>
          ))}
        </div>

        <div className="chart-panel panel">
          <div className="panel-title">
            <div><Activity size={17} /><b>{scheme === 's2' ? '多指标连续波形' : '实时采集趋势'}</b></div>
            <div className="channel-toggles">
              {DEFAULT_CURVE_KEYS.map(key => (
                <button
                  key={key}
                  aria-pressed={curveKeys.includes(key)}
                  className={curveKeys.includes(key) ? 'selected' : ''}
                  onClick={() => toggleCurveKey(key)}
                >{CHANNEL_MAP[key]?.label}</button>
              ))}
            </div>
          </div>
          <div className="chart-viewport">
            <Chart
              option={chartConfig.option}
              className="live-chart"
              style={{ minWidth: chartConfig.minWidth ? `${chartConfig.minWidth}px` : undefined }}
            />
          </div>
        </div>
        <RecordsTable
          records={records}
          projectNo={project.projectNo}
          itemLabel={activeItem.label}
          requestConfirm={requestConfirm}
        />
      </section>

      <aside className="monitor-aside">
        <section className="panel stats-panel">
          <div className="panel-title"><b>统计信息</b><span>最新值 · 最近 80 帧平均</span></div>
          {STAT_METRIC_KEYS.map(key => CHANNEL_MAP[key] ? (
            <StatRow key={key} code={key} samples={samples} current={latest[key]} />
          ) : null)}
        </section>
        <section className="panel alarm-panel">
          <div className="panel-title"><div><AlertTriangle size={16} /><b>实时告警</b></div><span>{alarms.filter(alarm => alarm.status === 'active').length} 待确认</span></div>
          {alarms.slice(0, 4).map(alarm => (
            <div className={`alarm-row level-${alarm.level}`} key={alarm.id}>
              <div><b>{alarm.code}</b><span>{alarm.message}</span></div>
              {alarm.status === 'active'
                ? <button onClick={() => ackAlarm(alarm.id)}>确认</button>
                : <small>{alarm.ackTime || '已恢复'}</small>}
            </div>
          ))}
        </section>
        <section className="panel device-mini">
          <div className="panel-title"><b>设备健康度</b><span>{devices.filter(device => device.status === 'online').length}/{devices.length} 在线</span></div>
          {devices.slice(0, 4).map(device => (
            <div key={device.id}><i className={`device-dot ${device.status}`} />{device.name}<span>{device.latencyMs || '-'} ms</span></div>
          ))}
        </section>
      </aside>
    </div>
  )
}

function ControlButtons({ state, onStart, onPause, onResume, onRecord, onStop }: {
  state: TestRunState
  onStart: () => void
  onPause: () => void
  onResume: () => void
  onRecord: () => void
  onStop: () => void
}) {
  return (
    <div className="controls">
      {(state === 'idle' || state === 'stopped') && <button className="primary" onClick={onStart}><Play size={15} />开始</button>}
      {state === 'running' && <button onClick={onPause}><Pause size={15} />暂停</button>}
      {state === 'paused' && <button className="primary" onClick={onResume}><Play size={15} />继续</button>}
      <button onClick={onRecord} disabled={state !== 'running' && state !== 'paused'}><Save size={15} />记录</button>
      <button className="danger" onClick={onStop} disabled={state !== 'running' && state !== 'paused'}><Square size={15} />停止</button>
    </div>
  )
}

function MetricCard({ code, value }: { code: string; value: number | undefined }) {
  const channel = CHANNEL_MAP[code]
  if (!channel) return null
  return (
    <article className="metric-card">
      <span>{channel.label}</span>
      <strong className="num">{format(code, value)}</strong>
      <small>{channel.unit || ' '}</small>
    </article>
  )
}

function StatRow({ code, samples, current }: { code: string; samples: { values: Record<string, number> }[]; current: number | undefined }) {
  const average = getWindowAverage(samples, code)
  return (
    <div className="stat-row">
      <b>{CHANNEL_MAP[code]?.label}</b>
      <span className="num">{format(code, current)} <i>{CHANNEL_MAP[code]?.unit}</i></span>
      <small>80 帧均值 {format(code, average ?? (Number.isFinite(current) ? current : undefined))}</small>
    </div>
  )
}

function RecordsTable({ records, projectNo, itemLabel, requestConfirm }: {
  records: RecordPoint[]
  projectNo: string
  itemLabel: string
  requestConfirm: (action: PendingAction) => void
}) {
  const deleteRecord = useTestStore(store => store.deleteRecord)
  return (
    <section className="panel record-panel">
      <div className="panel-title">
        <div><History size={17} /><b>{itemLabel} · 已记录采样点</b><span>{records.length} 条</span></div>
        <button onClick={() => downloadCsv(records, projectNo, itemLabel)}>导出 CSV</button>
      </div>
      <div className="table-wrap">
        <table>
          <thead><tr><th>点号</th><th>时间</th>{RECORD_COLUMNS.map(column => <th key={column.key}>{column.label}</th>)}<th>操作</th></tr></thead>
          <tbody>
            {records.length ? records.map(record => (
              <tr key={record.seq}>
                <td>{record.seq}</td><td>{record.time}</td>
                {RECORD_COLUMNS.map(column => <td className="num" key={column.key}>{format(column.key, record.values[column.key])}</td>)}
                <td><button aria-label={`删除第 ${record.seq} 条记录`} onClick={() => requestConfirm({
                  title: `删除记录点 ${record.seq}？`,
                  body: `该采样点时间为 ${record.time}，删除后不可恢复。`,
                  danger: true,
                  onConfirm: () => deleteRecord(record.seq),
                })}><Trash2 size={14} /></button></td>
              </tr>
            )) : <tr><td colSpan={RECORD_COLUMNS.length + 3} className="empty">开始采集后，点击“记录”保存关键工况。</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function TestItemSelector({ selectedId, progress, disabled, onSelect }: {
  selectedId: string
  progress: ProjectProgress
  disabled: boolean
  onSelect: (id: string) => boolean
}) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const popoverRef = useRef<HTMLDivElement>(null)
  const selected = TEST_ITEM_MAP.get(selectedId) ?? TEST_ITEMS[0]
  const status = progress[selected.id]?.status ?? 'not-started'

  useEffect(() => {
    if (disabled) setOpen(false)
  }, [disabled])

  useEffect(() => {
    if (!open) return
    const focusFrame = requestAnimationFrame(() => {
      const selectedItem = popoverRef.current?.querySelector<HTMLElement>('[data-selected="true"]')
      const firstItem = popoverRef.current?.querySelector<HTMLElement>('[data-test-item-id]')
      const itemToFocus = selectedItem ?? firstItem
      itemToFocus?.focus()
    })
    const handlePointerDown = (event: PointerEvent) => {
      if (event.target instanceof Node && !rootRef.current?.contains(event.target)) setOpen(false)
    }
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.preventDefault()
      setOpen(false)
      triggerRef.current?.focus()
    }
    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleEscape)
    return () => {
      cancelAnimationFrame(focusFrame)
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleEscape)
    }
  }, [open])

  const choose = (id: string) => {
    if (onSelect(id)) {
      setOpen(false)
      requestAnimationFrame(() => triggerRef.current?.focus())
    }
  }

  const handlePopoverKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return
    const items = [...(popoverRef.current?.querySelectorAll<HTMLButtonElement>('[data-test-item-id]') ?? [])]
    if (items.length === 0) return
    event.preventDefault()
    const activeIndex = items.findIndex(item => item === document.activeElement)
    if (event.key === 'Home') items[0].focus()
    else if (event.key === 'End') items[items.length - 1].focus()
    else if (event.key === 'ArrowDown') items[activeIndex < 0 ? 0 : (activeIndex + 1) % items.length].focus()
    else items[activeIndex < 0 ? items.length - 1 : (activeIndex - 1 + items.length) % items.length].focus()
  }

  return (
    <div
      className="test-selector"
      ref={rootRef}
      onBlur={event => {
        if (open && event.relatedTarget instanceof Node && !event.currentTarget.contains(event.relatedTarget)) setOpen(false)
      }}
    >
      <button
        type="button"
        className="test-selector-trigger"
        ref={triggerRef}
        aria-expanded={open}
        aria-haspopup="dialog"
        aria-controls="monitor-test-selector"
        disabled={disabled}
        title={disabled ? '采集运行、暂停或急停时不能切换试验' : '选择当前试验'}
        onClick={() => setOpen(value => !value)}
        onKeyDown={event => {
          if (event.key === 'ArrowDown' && !open) {
            event.preventDefault()
            setOpen(true)
          }
        }}
      >
        <b>{selected.label}</b><StatusBadge status={status} /><ChevronDown size={15} />
      </button>
      {open && (
        <div
          className="test-selector-popover"
          id="monitor-test-selector"
          role="dialog"
          aria-label="选择试验项目"
          ref={popoverRef}
          onKeyDown={handlePopoverKeyDown}
        >
          <TestItemTree progress={progress} selectedId={selectedId} onSelect={choose} compact />
        </div>
      )}
    </div>
  )
}

function StatusBadge({ status }: { status: TestItemStatus }) {
  return <span className={`item-status status-${status}`}>{status === 'completed' && <Check size={12} />}{STATUS_LABEL[status]}</span>
}

function TestItemTree({ progress, selectedId, onSelect, disabled = false, compact = false }: {
  progress: ProjectProgress
  selectedId: string
  onSelect?: (id: string) => void
  disabled?: boolean
  compact?: boolean
}) {
  return <div className={`test-tree ${compact ? 'compact' : ''}`}>{renderTestNodes(TEST_TREE, progress, selectedId, onSelect, disabled, 0)}</div>
}

function renderTestNodes(
  nodes: readonly TestItemNode[],
  progress: ProjectProgress,
  selectedId: string,
  onSelect: ((id: string) => void) | undefined,
  disabled: boolean,
  depth: number,
): React.ReactNode {
  return nodes.map(node => {
    if (node.kind === 'group') {
      const status = getGroupProgress(node, progress)
      return (
        <div className="test-tree-group" data-depth={depth} key={node.id}>
          <div className="test-group-heading" style={{ paddingLeft: `${10 + depth * 14}px` }}>
            <Folder size={14} /><b>{node.label}</b><StatusBadge status={status} />
          </div>
          {renderTestNodes(node.children, progress, selectedId, onSelect, disabled, depth + 1)}
        </div>
      )
    }
    const status = progress[node.id]?.status ?? 'not-started'
    return (
      <button
        type="button"
        aria-pressed={selectedId === node.id}
        className={`test-leaf ${selectedId === node.id ? 'selected' : ''}`}
        style={{ paddingLeft: `${12 + depth * 18}px` }}
        disabled={disabled || !onSelect}
        key={node.id}
        data-test-item-id={node.id}
        data-selected={selectedId === node.id ? 'true' : undefined}
        onClick={() => onSelect?.(node.id)}
      >
        <span className="item-kind">{node.kind === 'analysis' ? '分析' : '记录'}</span>
        <b>{node.label}</b>
        <StatusBadge status={status} />
        {onSelect && <ChevronRight size={14} />}
      </button>
    )
  })
}

function ProjectView() {
  const store = useTestStore()
  const { projects, currentProjectId, selectProject, createProject, runState } = store
  const project = selectCurrentProject(store)
  const [dialogOpen, setDialogOpen] = useState(false)
  const completed = countCompletedItems(project.itemProgress)
  const locked = !isContextSwitchAllowed(runState)
  const motor = project.motorSnapshot

  return (
    <div className="project-layout">
      <section className="panel project-card">
        <div className="panel-title"><b>当前试验项目</b><span>{project.status === 'active' ? '进行中' : '已完成'}</span></div>
        <div className="project-number"><span>项目/试验编号</span><b>{project.projectNo}</b></div>
        <h2>{motor.sampleName}</h2>
        <p>{motor.model} · {motor.manufacturer}</p>
        <dl className="project-details">
          <dt>样品名称</dt><dd>{motor.sampleName}</dd>
          <dt>电机型号</dt><dd>{motor.model}</dd>
          <dt>生产厂家</dt><dd>{motor.manufacturer}</dd>
          <dt>电机编号</dt><dd>{motor.motorNo}</dd>
          <dt>出厂编号</dt><dd>{motor.serialNo}</dd>
          <dt>额定功率</dt><dd>{motor.ratedPower} kW</dd>
          <dt>额定电压</dt><dd>{motor.ratedVoltage} V</dd>
          <dt>额定电流</dt><dd>{motor.ratedCurrent} A</dd>
          <dt>额定频率</dt><dd>{motor.ratedFreq} Hz</dd>
          <dt>额定转速</dt><dd>{motor.ratedSpeed} rpm</dd>
          <dt>额定功率因数</dt><dd>{motor.ratedPF.toFixed(2)}</dd>
          <dt>电机极数</dt><dd>{motor.poles} 极</dd>
          <dt>接线 / 绝缘</dt><dd>{motor.wiring} / {motor.insulation}</dd>
          <dt>操作员</dt><dd>{project.operator}</dd>
        </dl>
      </section>

      <section className="panel project-flow-panel">
        <div className="panel-title"><b>项目试验流程</b><span>{completed} / {TEST_ITEMS.length} 已完成</span></div>
        <TestItemTree
          progress={project.itemProgress}
          selectedId={project.activeItemId}
          onSelect={id => { store.selectTestItem(id) }}
          disabled={locked}
        />
      </section>

      <section className="panel project-list-panel">
        <div className="panel-title">
          <b>项目列表</b>
          <button disabled={locked} title={locked ? '采集运行、暂停或急停时不能新建项目' : undefined} onClick={() => setDialogOpen(true)}><Plus size={15} />新建项目</button>
        </div>
        <div className="motor-list">
          {projects.map(candidate => (
            <button
              className={candidate.id === currentProjectId ? 'selected' : ''}
              disabled={locked}
              title={locked ? '当前状态不能切换项目' : undefined}
              key={candidate.id}
              onClick={() => selectProject(candidate.id)}
            >
              <b>{candidate.projectNo}</b>
              <span>{candidate.motorSnapshot.model} · 电机编号 {candidate.motorSnapshot.motorNo}</span>
              <small>{countCompletedItems(candidate.itemProgress)} / {TEST_ITEMS.length} 已完成</small>
              <ChevronRight size={16} />
            </button>
          ))}
        </div>
      </section>

      {dialogOpen && (
        <NewProjectDialog
          currentProject={project}
          createProject={createProject}
          onClose={() => setDialogOpen(false)}
        />
      )}
    </div>
  )
}

interface ProjectDraft extends NewProjectInput {}

function NewProjectDialog({ currentProject, createProject, onClose }: {
  currentProject: TestProject
  createProject: (input: NewProjectInput) => CreateProjectResult
  onClose: () => void
}) {
  const dialogRef = useRef<HTMLElement>(null)
  useModalKeyboard(dialogRef, onClose)
  const initialModel = MOTOR_MODELS.find(model => model.id === currentProject.sourceModelId) ?? MOTOR_MODELS[0]
  const [draft, setDraft] = useState<ProjectDraft>(() => ({
    projectNo: '',
    operator: currentProject.operator,
    sourceModelId: initialModel.id,
    motorSnapshot: snapshotModel(initialModel),
  }))
  const [errors, setErrors] = useState<NewProjectErrors>({})

  const setSnapshot = (field: keyof MotorSnapshot, value: string | number) => {
    setDraft(current => ({
      ...current,
      motorSnapshot: { ...current.motorSnapshot, [field]: value },
    }))
  }
  const fieldError = (field: NewProjectField) => errors[field]
  const accessibility = (field: NewProjectField) => ({
    'aria-invalid': Boolean(fieldError(field)),
    'aria-describedby': fieldError(field) ? `project-${field}-error` : undefined,
  })

  const chooseModel = (modelId: string) => {
    const model = MOTOR_MODELS.find(candidate => candidate.id === modelId)
    if (!model) return
    setDraft(current => ({ ...current, sourceModelId: model.id, motorSnapshot: snapshotModel(model) }))
    setErrors(current => ({ ...current, sourceModelId: undefined }))
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const result = createProject(draft)
    if (result.ok) { onClose(); return }
    setErrors(result.errors)
    requestAnimationFrame(() => {
      const invalidField = dialogRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')
      const formError = dialogRef.current?.querySelector<HTMLElement>('.form-error-summary')
      const errorTarget = invalidField ?? formError
      errorTarget?.focus()
    })
  }

  return (
    <div className="modal-backdrop" role="presentation">
      <section className="project-dialog" role="dialog" aria-modal="true" aria-labelledby="new-project-title" aria-describedby="new-project-description" ref={dialogRef} tabIndex={-1}>
        <div className="dialog-header">
          <div><h2 id="new-project-title">新建试验项目</h2><span id="new-project-description">项目参数将保存为独立铭牌快照</span></div>
          <button type="button" aria-label="关闭新建项目" onClick={onClose}><X size={18} /></button>
        </div>
        <form onSubmit={submit} noValidate>
          {errors._form && <div className="form-error form-error-summary" role="alert" tabIndex={-1}>{errors._form}</div>}
          <div className="project-form-grid">
            <FormField field="projectNo" label="项目/试验编号" error={fieldError('projectNo')}>
              <input id="project-projectNo" data-dialog-initial-focus autoFocus value={draft.projectNo} onChange={event => setDraft(current => ({ ...current, projectNo: event.target.value }))} {...accessibility('projectNo')} />
            </FormField>
            <FormField field="operator" label="操作员" error={fieldError('operator')}>
              <input id="project-operator" value={draft.operator} onChange={event => setDraft(current => ({ ...current, operator: event.target.value }))} {...accessibility('operator')} />
            </FormField>
            <FormField field="sourceModelId" label="型号库预填" error={fieldError('sourceModelId')}>
              <select id="project-sourceModelId" value={draft.sourceModelId} onChange={event => chooseModel(event.target.value)} {...accessibility('sourceModelId')}>
                {MOTOR_MODELS.map(model => <option key={model.id} value={model.id}>{model.model} · {model.ratedPower} kW</option>)}
              </select>
            </FormField>
            <FormField field="sampleName" label="样品名称" error={fieldError('sampleName')}>
              <input id="project-sampleName" value={draft.motorSnapshot.sampleName} onChange={event => setSnapshot('sampleName', event.target.value)} {...accessibility('sampleName')} />
            </FormField>
            <FormField field="motorNo" label="电机编号" error={fieldError('motorNo')}>
              <input id="project-motorNo" value={draft.motorSnapshot.motorNo} onChange={event => setSnapshot('motorNo', event.target.value)} {...accessibility('motorNo')} />
            </FormField>
            <FormField field="model" label="电机型号" error={fieldError('model')}>
              <input id="project-model" value={draft.motorSnapshot.model} onChange={event => setSnapshot('model', event.target.value)} {...accessibility('model')} />
            </FormField>
            <FormField field="manufacturer" label="生产厂家" error={fieldError('manufacturer')} wide>
              <input id="project-manufacturer" value={draft.motorSnapshot.manufacturer} onChange={event => setSnapshot('manufacturer', event.target.value)} {...accessibility('manufacturer')} />
            </FormField>
            <FormField field="serialNo" label="出厂编号" error={fieldError('serialNo')}>
              <input id="project-serialNo" value={draft.motorSnapshot.serialNo} onChange={event => setSnapshot('serialNo', event.target.value)} {...accessibility('serialNo')} />
            </FormField>
            <FormField field="ratedVoltage" label="额定电压 (V)" error={fieldError('ratedVoltage')}>
              <input id="project-ratedVoltage" type="number" min="0" step="any" value={draft.motorSnapshot.ratedVoltage} onChange={event => setSnapshot('ratedVoltage', Number(event.target.value))} {...accessibility('ratedVoltage')} />
            </FormField>
            <FormField field="ratedCurrent" label="额定电流 (A)" error={fieldError('ratedCurrent')}>
              <input id="project-ratedCurrent" type="number" min="0" step="any" value={draft.motorSnapshot.ratedCurrent} onChange={event => setSnapshot('ratedCurrent', Number(event.target.value))} {...accessibility('ratedCurrent')} />
            </FormField>
            <FormField field="ratedPower" label="额定功率 (kW)" error={fieldError('ratedPower')}>
              <input id="project-ratedPower" type="number" min="0" step="any" value={draft.motorSnapshot.ratedPower} onChange={event => setSnapshot('ratedPower', Number(event.target.value))} {...accessibility('ratedPower')} />
            </FormField>
            <FormField field="ratedFreq" label="额定频率 (Hz)" error={fieldError('ratedFreq')}>
              <input id="project-ratedFreq" type="number" min="0" step="any" value={draft.motorSnapshot.ratedFreq} onChange={event => setSnapshot('ratedFreq', Number(event.target.value))} {...accessibility('ratedFreq')} />
            </FormField>
            <FormField field="ratedSpeed" label="额定转速 (rpm)" error={fieldError('ratedSpeed')}>
              <input id="project-ratedSpeed" type="number" min="0" step="any" value={draft.motorSnapshot.ratedSpeed} onChange={event => setSnapshot('ratedSpeed', Number(event.target.value))} {...accessibility('ratedSpeed')} />
            </FormField>
            <FormField field="ratedPF" label="额定功率因数" error={fieldError('ratedPF')}>
              <input id="project-ratedPF" type="number" min="0" max="1" step="0.01" value={draft.motorSnapshot.ratedPF} onChange={event => setSnapshot('ratedPF', Number(event.target.value))} {...accessibility('ratedPF')} />
            </FormField>
            <FormField field="poles" label="电机极数" error={fieldError('poles')}>
              <input id="project-poles" type="number" min="2" step="2" value={draft.motorSnapshot.poles} onChange={event => setSnapshot('poles', Number(event.target.value))} {...accessibility('poles')} />
            </FormField>
            <FormField field="wiring" label="接线方式" error={fieldError('wiring')}>
              <input id="project-wiring" value={draft.motorSnapshot.wiring} onChange={event => setSnapshot('wiring', event.target.value)} {...accessibility('wiring')} />
            </FormField>
            <FormField field="insulation" label="绝缘等级" error={fieldError('insulation')}>
              <input id="project-insulation" value={draft.motorSnapshot.insulation} onChange={event => setSnapshot('insulation', event.target.value)} {...accessibility('insulation')} />
            </FormField>
          </div>
          <div className="dialog-actions"><button type="button" onClick={onClose}>取消</button><button type="submit" className="primary">创建并切换</button></div>
        </form>
      </section>
    </div>
  )
}

function FormField({ field, label, error, wide = false, children }: {
  field: NewProjectField
  label: string
  error?: string
  wide?: boolean
  children: React.ReactNode
}) {
  return (
    <div className={`form-field ${wide ? 'wide' : ''}`}>
      <label htmlFor={`project-${field}`}>{label}</label>
      {children}
      {error && <span className="form-error" id={`project-${field}-error`}>{error}</span>}
    </div>
  )
}

function TestsView({ requestConfirm }: { requestConfirm: (action: PendingAction) => void }) {
  const store = useTestStore()
  const { runState, newTestSession, selectTestItem } = store
  const project = selectCurrentProject(store)
  const currentItem = TEST_ITEM_MAP.get(project.activeItemId) ?? TEST_ITEMS[0]
  const locked = !isContextSwitchAllowed(runState)
  return (
    <div className="tests-view">
      <section className="panel test-list">
        <div className="panel-title"><b>试验项目</b><span>{countCompletedItems(project.itemProgress)} / {TEST_ITEMS.length} 已完成</span></div>
        <TestItemTree progress={project.itemProgress} selectedId={project.activeItemId} onSelect={id => { selectTestItem(id) }} disabled={locked} />
      </section>
      <section className="panel step-canvas">
        <div className="panel-title"><b>标准作业流</b><span>当前：{currentItem.label}</span></div>
        <ol className="steps">
          {['确认项目与铭牌', '校验安全联锁', '连接并校验设备', '开始采集与记录', '运行分析并归档', '选择报告章节'].map((label, index) => (
            <li key={label} className={index < 3 ? 'done' : index === 3 ? 'current' : ''}>
              <i>{index < 3 ? <Check size={14} /> : index + 1}</i>
              <div><b>{label}</b><span>{index === 3 ? `${currentItem.label}：${runLabel(runState)}` : index < 3 ? '已完成检查' : '等待上一阶段完成'}</span></div>
            </li>
          ))}
        </ol>
        <div className="canvas-actions">
          <button onClick={newTestSession}><RefreshCw size={16} />重置模拟会话</button>
          <button className="primary" onClick={() => requestConfirm({
            title: '确认进入采集阶段？',
            body: `系统将以“${currentItem.label}”为上下文创建新的模拟采集段。`,
            onConfirm: () => useTestStore.getState().toast('已进入采集阶段'),
          })}>进入实时采集</button>
        </div>
      </section>
    </div>
  )
}

function RecordsView({ requestConfirm }: { requestConfirm: (action: PendingAction) => void }) {
  const store = useTestStore()
  const { records, history, compareIds, toggleCompare, toast } = store
  const project = selectCurrentProject(store)
  const currentItem = TEST_ITEM_MAP.get(project.activeItemId) ?? TEST_ITEMS[0]
  const [method, setMethod] = useState('A法')
  const [versions, setVersions] = useState([{
    name: `${currentItem.label} · 当前草稿`,
    owner: project.operator,
    default: true,
    points: records.length,
  }])
  const saveVersion = () => {
    setVersions(current => [{
      name: `${currentItem.label} · 版本 ${current.length + 1}`,
      owner: project.operator,
      default: false,
      points: records.length,
    }, ...current])
    toast('已保存新的记录版本')
  }

  return (
    <div className="analysis-view">
      <section className="panel version-panel">
        <div className="panel-title"><b>记录版本</b><button onClick={saveVersion}><Save size={15} />保存版本</button></div>
        {versions.map((version, index) => (
          <div className="version-row" key={version.name}>
            <div><b>{version.name}</b><span>{version.owner} · {version.points} 个点</span></div>
            {version.default ? <em>默认有效</em> : <button onClick={() => setVersions(list => list.map((item, itemIndex) => ({ ...item, default: itemIndex === index })))}>设为默认</button>}
          </div>
        ))}
        <div className="panel-title secondary"><b>历史记录对比</b><span>最多 4 组</span></div>
        {history.slice(0, 6).map(item => (
          <label className="compare-row" key={item.id}>
            <input type="checkbox" checked={compareIds.includes(item.id)} onChange={() => toggleCompare(item.id)} />
            <span><b>{item.testNo}</b>{item.itemLabel}</span><em>{item.result}</em>
          </label>
        ))}
      </section>
      <section className="panel result-canvas">
        <div className="panel-title">
          <div><b>A/B/C/E/H 方法分析</b><span>{project.projectNo} · {currentItem.label}</span></div>
          <div className="method-tabs">{['A法', 'B法', 'C法', 'E法', 'H法'].map(item => <button key={item} className={method === item ? 'selected' : ''} onClick={() => setMethod(item)}>{item}</button>)}</div>
        </div>
        <div className="result-grid"><Result label="额定效率" value="94.82" unit="%" /><Result label="功率因数" value="0.891" unit="" /><Result label="转差率" value="0.98" unit="%" /><Result label="总损耗" value="6.04" unit="kW" /></div>
        <div className="loss-bars">{['定子铜耗', '转子铜耗', '铁耗', '机械损耗', '杂散损耗'].map((label, index) => <div key={label}><span>{label}</span><i style={{ width: `${76 - index * 11}%` }} /><b>{(2.31 - index * .31).toFixed(2)} kW</b></div>)}</div>
        <div className="canvas-actions"><button onClick={() => toast(`${method} 原型分析已刷新`)}><BarChart3 size={16} />运行原型分析</button><button className="primary" onClick={() => requestConfirm({ title: '覆盖当前默认分析结果？', body: '该动作仅更新网页原型中的默认结果标记。', onConfirm: () => toast('默认分析结果已更新') })}>设为报告来源</button></div>
      </section>
    </div>
  )
}

function ReportsView() {
  const project = useTestStore(selectCurrentProject)
  const toast = useTestStore(store => store.toast)
  const [selected, setSelected] = useState(['r1', 'r2', 'r4'])
  const exportReport = () => {
    const rows = REPORT_TEMPLATES.filter(template => selected.includes(template.id)).map(template => `<li>${template.name}</li>`).join('')
    downloadText(
      `${safeFileName(project.projectNo)}-试验报告原型.html`,
      `<h1>三相异步电机试验报告（原型）</h1><p>项目编号：${project.projectNo}</p><p>电机编号：${project.motorSnapshot.motorNo}</p><ul>${rows}</ul>`,
      'text/html;charset=utf-8',
    )
    toast('报告原型已导出为 HTML 文件')
  }
  return (
    <div className="report-view">
      <section className="panel">
        <div className="panel-title"><div><b>报告章节</b><span>项目 {project.projectNo}</span></div><button className="primary" onClick={exportReport}><Download size={16} />导出原型</button></div>
        {REPORT_TEMPLATES.map(template => (
          <label className="report-row" key={template.id}>
            <input type="checkbox" checked={selected.includes(template.id)} onChange={() => setSelected(current => current.includes(template.id) ? current.filter(id => id !== template.id) : [...current, template.id])} />
            <span><b>{template.name}</b><small>{template.scope} · {template.format}</small></span><em>{template.id === 'r3' ? '缺少默认记录' : '就绪'}</em>
          </label>
        ))}
      </section>
      <section className="panel report-summary"><b>导出检查</b><Checklist items={['项目铭牌参数已确认', '空载试验结果已归档', '温升记录已选择', 'A/B/C/E/H 分析已选择', '报告仅为网页原型，不含签章']} /></section>
    </div>
  )
}

function SettingsView({ requestConfirm }: { requestConfirm: (action: PendingAction) => void }) {
  const { devices, connectDevice, disconnectDevice, toast } = useTestStore()
  return (
    <div className="settings-view">
      <section className="panel">
        <div className="panel-title"><b>设备与通信</b><span>逐设备状态</span></div>
        {devices.map(device => (
          <div className="device-row" key={device.id}>
            <i className={`device-dot ${device.status}`} />
            <div><b>{device.name} · {device.model}</b><span>{device.protocol} · {device.address} · {device.role}</span></div>
            <em>{device.status === 'online' ? `${device.latencyMs} ms` : device.status === 'connecting' ? '连接中' : '离线'}</em>
            {device.status === 'online'
              ? <button onClick={() => requestConfirm({ title: `断开 ${device.name}？`, body: '断开后将阻止新的试验采集。', danger: true, onConfirm: () => disconnectDevice(device.id) })}>断开</button>
              : <button onClick={() => connectDevice(device.id)}>连接</button>}
          </div>
        ))}
      </section>
      <section className="panel config-card">
        <div className="panel-title"><b>试验参数</b><span>模拟配置</span></div>
        <label>采样周期<select defaultValue="500"><option value="500">500 ms</option><option value="1000">1000 ms</option></select></label>
        <label>PT 变比<input defaultValue="1.000" /></label>
        <label>CT 变比<input defaultValue="1.000" /></label>
        <label>扭矩量程<select defaultValue="1000"><option value="1000">1000 N·m</option><option value="2000">2000 N·m</option></select></label>
        <div className="canvas-actions"><button onClick={() => toast('配置已校验并保存')}>保存配置</button><button className="danger" onClick={() => requestConfirm({ title: '执行扭矩清零？', body: '这是模拟设备命令。确认后会记录一次操作反馈。', danger: true, onConfirm: () => toast('扭矩通道已完成模拟清零') })}><Wrench size={16} />扭矩清零</button></div>
      </section>
    </div>
  )
}

function Result({ label, value, unit }: { label: string; value: string; unit: string }) {
  return <div><span>{label}</span><strong className="num">{value}</strong><small>{unit}</small></div>
}

function Checklist({ items }: { items: string[] }) {
  return <ul className="checklist">{items.map((item, index) => <li key={item}><i className={index < 4 ? 'yes' : ''}>{index < 4 ? <Check size={13} /> : ''}</i>{item}</li>)}</ul>
}

function MobileNav({ active, onChange }: { active: ViewId; onChange: (id: ViewId) => void }) {
  return <nav className="mobile-nav">{NAV.slice(0, 5).map(item => <NavButton key={item.id} item={item} active={active === item.id} onClick={() => onChange(item.id)} />)}</nav>
}

function ToastLayer() {
  const { toasts, dismissToast } = useTestStore()
  return <div className="toast-layer" aria-live="polite">{toasts.map(toast => <div className={`toast ${toast.kind}`} key={toast.id}>{toast.text}<button aria-label="关闭提示" onClick={() => dismissToast(toast.id)}><X size={15} /></button></div>)}</div>
}

function ConfirmDialog({ action, onCancel, onConfirm }: { action: PendingAction; onCancel: () => void; onConfirm: () => void }) {
  const dialogRef = useRef<HTMLElement>(null)
  useModalKeyboard(dialogRef, onCancel)
  return (
    <div className="modal-backdrop" role="presentation">
      <section className="confirm-dialog" role="dialog" aria-modal="true" aria-labelledby="confirm-title" ref={dialogRef} tabIndex={-1}>
        <AlertTriangle size={22} /><h2 id="confirm-title">{action.title}</h2><p>{action.body}</p>
        <div><button onClick={onCancel}>取消</button><button className={action.danger ? 'danger' : 'primary'} data-dialog-initial-focus autoFocus onClick={onConfirm}>确认执行</button></div>
      </section>
    </div>
  )
}

function safeFileName(value: string) {
  return value.replace(/[\\/:*?"<>|]/g, '-').trim() || '未命名项目'
}

function downloadText(name: string, content: string, type: string) {
  const url = URL.createObjectURL(new Blob([content], { type }))
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = name
  anchor.click()
  URL.revokeObjectURL(url)
}

function downloadCsv(records: RecordPoint[], projectNo: string, itemLabel: string) {
  const data = [
    ['点号', '时间', ...RECORD_COLUMNS.map(column => column.label)].join(','),
    ...records.map(record => [
      record.seq,
      record.time,
      ...RECORD_COLUMNS.map(column => record.values[column.key]),
    ].join(',')),
  ].join('\n')
  downloadText(`${safeFileName(projectNo)}-${safeFileName(itemLabel)}-记录点.csv`, `\ufeff${data}`, 'text/csv;charset=utf-8')
}
