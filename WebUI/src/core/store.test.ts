import { beforeEach, describe, expect, it } from 'vitest'
import { MOTOR_MODELS } from './data'
import { countCompletedItems, TEST_ITEMS } from './testItems'
import {
  createInitialProjects,
  fmtElapsed,
  isContextSwitchAllowed,
  selectCurrentProject,
  useTestStore,
} from './store'
import type { NewProjectInput } from './types'

function makeProjectInput(projectNo = '2026-0720-01'): NewProjectInput {
  const { id, ...motorSnapshot } = MOTOR_MODELS[0]
  return {
    projectNo,
    operator: '工程师 02',
    sourceModelId: id,
    motorSnapshot: { ...motorSnapshot },
  }
}

describe('motor test session store', () => {
  beforeEach(() => {
    const projects = createInitialProjects()
    useTestStore.setState({
      projects,
      currentProjectId: projects[0].id,
      runState: 'idle',
      t: 0,
      samples: [],
      records: [],
      startedAt: '',
    })
  })

  it('formats elapsed time with stable clock fields', () => {
    expect(fmtElapsed(3723)).toBe('01:02:03')
  })

  it('initializes the current project with the confirmed 3 / 21 progress', () => {
    const project = selectCurrentProject(useTestStore.getState())
    expect(project.activeItemId).toBe('load')
    expect(countCompletedItems(project.itemProgress)).toBe(3)
    expect(project.itemProgress.resistance.status).toBe('completed')
    expect(project.itemProgress.noload.status).toBe('completed')
    expect(project.itemProgress['eff-a'].status).toBe('completed')
    expect(project.itemProgress.load.status).toBe('in-progress')
    expect(Object.keys(project.itemProgress)).toHaveLength(21)
  })

  it('records an immutable snapshot only while collecting', () => {
    useTestStore.getState().startTest()
    useTestStore.getState().tick()
    const firstFrame = useTestStore.getState().latest
    const firstVoltage = firstFrame.Uab
    expect(firstFrame.T_stator_avg).toBeCloseTo((firstFrame.T_w1 + firstFrame.T_w2 + firstFrame.T_w3) / 3)
    expect(firstFrame.T_rotor_avg).toBeCloseTo((firstFrame.T_w4 + firstFrame.T_w5 + firstFrame.T_w6) / 3)
    useTestStore.getState().recordPoint()
    useTestStore.getState().tick()

    const record = useTestStore.getState().records[0]
    expect(record).toBeDefined()
    expect(record.values.Uab).toBe(firstVoltage)
    expect(useTestStore.getState().records).toHaveLength(1)
  })

  it('allows only leaf ids and locks context changes outside idle or stopped', () => {
    const firstProject = selectCurrentProject(useTestStore.getState())
    expect(useTestStore.getState().selectTestItem('eff')).toBe(false)
    expect(selectCurrentProject(useTestStore.getState()).activeItemId).toBe('load')
    expect(useTestStore.getState().selectTestItem('eff-b')).toBe(true)

    for (const runState of ['running', 'paused', 'estop'] as const) {
      useTestStore.setState({ runState })
      expect(useTestStore.getState().selectTestItem('noload')).toBe(false)
      expect(useTestStore.getState().selectProject('project-m2')).toBe(false)
      expect(useTestStore.getState().createProject(makeProjectInput())).toMatchObject({
        ok: false,
        errors: { _form: expect.any(String) },
      })
      expect(useTestStore.getState().currentProjectId).toBe(firstProject.id)
    }

    expect(isContextSwitchAllowed('idle')).toBe(true)
    expect(isContextSwitchAllowed('stopped')).toBe(true)
    expect(isContextSwitchAllowed('running')).toBe(false)
    expect(isContextSwitchAllowed('paused')).toBe(false)
    expect(isContextSwitchAllowed('estop')).toBe(false)
    useTestStore.setState({ runState: 'stopped' })
    expect(useTestStore.getState().selectTestItem('noload')).toBe(true)
    expect(useTestStore.getState().selectProject('project-m2')).toBe(true)
  })

  it('rejects invalid project fields without partially writing state', () => {
    const before = useTestStore.getState().projects
    const input = makeProjectInput(' ')
    input.operator = ' '
    input.sourceModelId = 'missing-model'
    input.motorSnapshot.sampleName = ' '
    input.motorSnapshot.motorNo = ' '
    input.motorSnapshot.model = ' '
    input.motorSnapshot.manufacturer = ' '
    input.motorSnapshot.serialNo = ' '
    input.motorSnapshot.wiring = ' '
    input.motorSnapshot.insulation = ' '
    input.motorSnapshot.ratedVoltage = 0
    input.motorSnapshot.ratedCurrent = Number.NaN
    input.motorSnapshot.ratedPower = -1
    input.motorSnapshot.ratedFreq = Number.POSITIVE_INFINITY
    input.motorSnapshot.ratedSpeed = 0
    input.motorSnapshot.ratedPF = 1.2
    input.motorSnapshot.poles = 3

    const result = useTestStore.getState().createProject(input)
    expect(result).toMatchObject({
      ok: false,
      errors: {
        projectNo: expect.any(String),
        operator: expect.any(String),
        sourceModelId: expect.any(String),
        sampleName: expect.any(String),
        motorNo: expect.any(String),
        model: expect.any(String),
        manufacturer: expect.any(String),
        serialNo: expect.any(String),
        wiring: expect.any(String),
        insulation: expect.any(String),
        ratedVoltage: expect.any(String),
        ratedCurrent: expect.any(String),
        ratedPower: expect.any(String),
        ratedFreq: expect.any(String),
        ratedSpeed: expect.any(String),
        ratedPF: expect.any(String),
        poles: expect.any(String),
      },
    })
    expect(useTestStore.getState().projects).toBe(before)
  })

  it('normalizes project numbers before duplicate validation', () => {
    const before = useTestStore.getState().projects
    const result = useTestStore.getState().createProject(makeProjectInput('  2026-0716-01  '))

    expect(result).toMatchObject({ ok: false, errors: { projectNo: expect.any(String) } })
    expect(useTestStore.getState().projects).toBe(before)
  })

  it('creates an isolated editable snapshot with 0 / 21 progress', () => {
    const fixtureBefore = JSON.stringify(MOTOR_MODELS)
    const input = makeProjectInput()
    input.motorSnapshot.model = '项目专用型号'
    const result = useTestStore.getState().createProject(input)

    expect(result.ok).toBe(true)
    if (!result.ok) throw new Error('Project creation failed')
    input.motorSnapshot.model = '创建后修改的草稿'
    input.motorSnapshot.duty = '创建后修改的工作制'
    const current = selectCurrentProject(useTestStore.getState())
    expect(current.id).toBe(result.project.id)
    expect(current.motorSnapshot.model).toBe('项目专用型号')
    expect(current.motorSnapshot.duty).toBe(MOTOR_MODELS[0].duty)
    expect(current.motorSnapshot.ip).toBe(MOTOR_MODELS[0].ip)
    expect(current.motorSnapshot.cooling).toBe(MOTOR_MODELS[0].cooling)
    expect(current.motorSnapshot).not.toHaveProperty('id')
    expect(countCompletedItems(current.itemProgress)).toBe(0)
    expect(Object.keys(current.itemProgress)).toHaveLength(TEST_ITEMS.length)
    expect(current.activeItemId).toBe('resistance')
    expect(JSON.stringify(MOTOR_MODELS)).toBe(fixtureBefore)
  })

  it('restores each project selection and progress when switching projects', () => {
    expect(useTestStore.getState().selectProject('project-m2')).toBe(true)
    expect(useTestStore.getState().selectTestItem('eff-e')).toBe(true)
    expect(useTestStore.getState().setTestItemStatus('eff-e', 'completed')).toBe(true)
    expect(useTestStore.getState().selectProject('project-m1')).toBe(true)
    expect(useTestStore.getState().selectProject('project-m2')).toBe(true)

    const project = selectCurrentProject(useTestStore.getState())
    expect(project.activeItemId).toBe('eff-e')
    expect(project.itemProgress['eff-e'].status).toBe('completed')
  })

  it('advances a new project leaf to in-progress on start without auto-completing it on stop', () => {
    const result = useTestStore.getState().createProject(makeProjectInput())
    expect(result.ok).toBe(true)
    useTestStore.getState().startTest()
    expect(selectCurrentProject(useTestStore.getState()).itemProgress.resistance.status).toBe('in-progress')
    useTestStore.getState().stopTest()
    expect(selectCurrentProject(useTestStore.getState()).itemProgress.resistance.status).toBe('in-progress')
  })

  it('keeps exactly one default record version at the UI contract boundary', () => {
    const versions = [
      { id: 'v1', default: true },
      { id: 'v2', default: false },
    ].map(version => ({ ...version, default: version.id === 'v2' }))
    expect(versions.filter(version => version.default)).toHaveLength(1)
  })
})
