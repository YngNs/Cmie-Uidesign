import { describe, expect, it } from 'vitest'
import { CHANNEL_MAP, DEFAULT_CURVE_KEYS, RECORD_COLUMNS } from './data'
import { MEASUREMENT_GROUPS, STAT_METRIC_KEYS, getTrendAxisLayout, getWindowAverage } from './metrics'

describe('monitor metric contract', () => {
  it('uses the required measurement group and channel order', () => {
    const measurementKeys = MEASUREMENT_GROUPS.flatMap(group => [...group.keys])
    expect(MEASUREMENT_GROUPS.map(group => group.label)).toEqual([
      '电气测量', '定子及转子温度', '机械测量', '其他部件温度',
    ])
    expect(MEASUREMENT_GROUPS[0].keys).toEqual(['Uab', 'Ubc', 'Uca', 'f', 'Ia', 'Ib', 'Ic'])
    expect(MEASUREMENT_GROUPS[1].keys).toEqual([
      'T_w1', 'T_w2', 'T_w3', 'T_stator_avg', 'T_w4', 'T_w5', 'T_w6', 'T_rotor_avg',
    ])
    expect(measurementKeys).toHaveLength(24)
    expect(new Set(measurementKeys)).toHaveLength(24)
    expect(STAT_METRIC_KEYS).toHaveLength(6)
    for (const key of [...measurementKeys, ...STAT_METRIC_KEYS]) expect(CHANNEL_MAP[key]).toBeDefined()
  })

  it('defines the required trend controls and record columns', () => {
    expect(DEFAULT_CURVE_KEYS).toEqual([
      'n', 'T', 'Uavg', 'Iavg', 'P', 'Q', 'PF', 'T_stator_avg', 'T_rotor_avg',
    ])
    expect(RECORD_COLUMNS.map(column => column.key)).toEqual([
      'Uab', 'Ubc', 'Uca', 'Ia', 'Ib', 'Ic', 'P', 'Q', 'Pshaft',
    ])
    for (const key of DEFAULT_CURVE_KEYS) expect(CHANNEL_MAP[key]).toBeDefined()
    for (const column of RECORD_COLUMNS) expect(CHANNEL_MAP[column.key]).toBeDefined()
  })

  it('groups trend axes by unit and assigns each channel to the matching scale', () => {
    const layout = getTrendAxisLayout([...DEFAULT_CURVE_KEYS, 'Pshaft'])

    expect(layout.groups.map(group => group.name)).toEqual([
      'rpm', 'N·m', 'V', 'A', 'kW', 'kvar', '功率因数', '℃',
    ])
    expect(layout.axisIndexByKey.Pshaft).toBe(layout.axisIndexByKey.P)
    expect(layout.axisIndexByKey.T_stator_avg).toBe(layout.axisIndexByKey.T_rotor_avg)
    expect(layout.axisIndexByKey.Q).not.toBe(layout.axisIndexByKey.P)
    expect(layout.axisIndexByKey.PF).not.toBe(layout.axisIndexByKey.Q)
    expect(layout.groups[layout.axisIndexByKey.P]).toMatchObject({ min: 0, max: 160 })
    expect(layout.groups.map(group => [group.position, group.offset])).toEqual([
      ['left', 0], ['right', 0], ['left', 52], ['right', 52],
      ['left', 104], ['right', 104], ['left', 156], ['right', 156],
    ])
    expect(layout).toMatchObject({ left: 214, right: 214, minWidth: 768 })
  })

  it('returns a compact fallback layout when no trend channel is selected', () => {
    expect(getTrendAxisLayout([])).toEqual({
      groups: [],
      axisIndexByKey: {},
      left: 34,
      right: 24,
      minWidth: undefined,
    })
    expect(getTrendAxisLayout(['missing-channel'])).toEqual(getTrendAxisLayout([]))
  })

  it('uses only the most recent 80 finite values and keeps zero', () => {
    const samples = Array.from({ length: 82 }, (_, index) => ({ values: { P: index } }))
    samples[80].values.P = Number.NaN
    samples[81].values.P = Number.POSITIVE_INFINITY

    expect(getWindowAverage(samples, 'P')).toBe(40.5)
    expect(getWindowAverage([{ values: { P: 0 } }, { values: { P: 2 } }], 'P')).toBe(1)
  })

  it('returns no average for an empty or non-finite window', () => {
    expect(getWindowAverage([], 'P')).toBeUndefined()
    expect(getWindowAverage([{ values: { P: Number.NaN } }], 'P')).toBeUndefined()
    expect(getWindowAverage([{ values: { P: 1 } }], 'P', 0)).toBeUndefined()
  })
})
