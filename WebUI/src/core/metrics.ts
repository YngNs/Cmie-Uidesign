import { CHANNEL_MAP } from './data'
import type { Sample } from './types'

export const MEASUREMENT_GROUPS = [
  { label: '电气测量', keys: ['Uab', 'Ubc', 'Uca', 'f', 'Ia', 'Ib', 'Ic'] },
  { label: '定子及转子温度', keys: ['T_w1', 'T_w2', 'T_w3', 'T_stator_avg', 'T_w4', 'T_w5', 'T_w6', 'T_rotor_avg'] },
  { label: '机械测量', keys: ['n', 'T'] },
  { label: '其他部件温度', keys: ['T_de', 'T_nde', 'T_in', 'T_out', 'T_core', 'T_surf', 'T_amb'] },
] as const

export const STAT_METRIC_KEYS = ['Uavg', 'Iavg', 'P', 'Q', 'Pshaft', 'PF'] as const

export interface TrendAxisGroup {
  unitKey: string
  name: string
  color: string
  min: number
  max: number
  position: 'left' | 'right'
  offset: number
}

export interface TrendAxisLayout {
  groups: TrendAxisGroup[]
  axisIndexByKey: Record<string, number>
  left: number
  right: number
  minWidth?: number
}

const POWER_FACTOR_UNIT_KEY = '__power-factor__'

export function getTrendAxisLayout(keys: readonly string[]): TrendAxisLayout {
  const groups: TrendAxisGroup[] = []
  const axisIndexByKey: Record<string, number> = {}

  for (const key of keys) {
    const channel = CHANNEL_MAP[key]
    if (!channel) continue
    const unitKey = channel.unit || POWER_FACTOR_UNIT_KEY
    let axisIndex = groups.findIndex(group => group.unitKey === unitKey)
    if (axisIndex < 0) {
      axisIndex = groups.length
      groups.push({
        unitKey,
        name: channel.unit || '功率因数',
        color: channel.color,
        min: channel.min,
        max: channel.max,
        position: axisIndex % 2 === 0 ? 'left' : 'right',
        offset: Math.floor(axisIndex / 2) * 52,
      })
    } else {
      groups[axisIndex].min = Math.min(groups[axisIndex].min, channel.min)
      groups[axisIndex].max = Math.max(groups[axisIndex].max, channel.max)
    }
    axisIndexByKey[key] = axisIndex
  }

  const leftAxisCount = Math.ceil(groups.length / 2)
  const rightAxisCount = Math.floor(groups.length / 2)
  const left = leftAxisCount > 0 ? 58 + (leftAxisCount - 1) * 52 : 34
  const right = rightAxisCount > 0 ? 58 + (rightAxisCount - 1) * 52 : 24

  return {
    groups,
    axisIndexByKey,
    left,
    right,
    minWidth: groups.length > 4 ? Math.max(680, left + right + 340) : undefined,
  }
}

export function getWindowAverage(
  samples: readonly Pick<Sample, 'values'>[],
  key: string,
  size = 80,
): number | undefined {
  if (size <= 0) return undefined
  const values = samples
    .slice(-size)
    .map(sample => sample.values[key])
    .filter((value): value is number => Number.isFinite(value))
  if (values.length === 0) return undefined
  return values.reduce((sum, value) => sum + value, 0) / values.length
}
