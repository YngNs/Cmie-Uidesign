import { describe, expect, it } from 'vitest'
import { TEST_TREE } from './data'
import { TEST_ITEM_MAP, TEST_ITEMS, flattenTestItems, getGroupProgress } from './testItems'
import type { ProjectProgress, TestItemNode } from './types'

function allNodes(nodes: readonly TestItemNode[]): TestItemNode[] {
  return nodes.flatMap(node => node.kind === 'group' ? [node, ...allNodes(node.children)] : [node])
}

describe('test item tree contract', () => {
  it('contains five groups, 21 selectable leaves, and 26 unique ids', () => {
    const nodes = allNodes(TEST_TREE)
    const groups = nodes.filter(node => node.kind === 'group')

    const topLevelLeaves = TEST_TREE.filter(node => node.kind !== 'group')
    const descendantLeaves = TEST_TREE
      .filter(node => node.kind === 'group')
      .flatMap(node => flattenTestItems(node.children))

    expect(groups).toHaveLength(5)
    expect(TEST_ITEMS).toHaveLength(21)
    expect(topLevelLeaves).toHaveLength(9)
    expect(descendantLeaves).toHaveLength(12)
    expect(new Set(nodes.map(node => node.id))).toHaveLength(26)
    expect(new Set(TEST_ITEMS.map(item => item.id))).toHaveLength(21)
    for (const id of ['eff', 'temprise', 'temprise-rated', 'temprise-lf', 'locked']) {
      expect(TEST_ITEM_MAP.has(id)).toBe(false)
    }
  })

  it('keeps the confirmed depth-first leaf order and paths', () => {
    expect(flattenTestItems(TEST_TREE).map(item => item.id)).toEqual([
      'resistance', 'noload', 'load',
      'eff-a', 'eff-b', 'eff-e', 'eff-circle',
      'temprise-rated-rec', 'temprise-rated-stator', 'temprise-rated-rotor',
      'temprise-lf-rec', 'temprise-lf-stator', 'temprise-lf-rotor',
      'locked-50', 'locked-lf',
      'withstand', 'impulse', 'inertia', 'vibration', 'tn', 'maxtq',
    ])
    expect(TEST_ITEM_MAP.get('temprise-rated-stator')).toMatchObject({
      path: ['温升试验', '额定工况负载温升试验', '额定工况负载温升定子热电阻分析'],
      groupIds: ['temprise', 'temprise-rated'],
    })
  })

  it('derives directory status only from descendant leaves', () => {
    const progress: ProjectProgress = Object.fromEntries(TEST_ITEMS.map(item => [item.id, {
      status: 'not-started',
      updatedAt: '2026-07-16T09:00:00.000Z',
    }]))
    const efficiency = TEST_TREE.find(node => node.id === 'eff')
    if (!efficiency || efficiency.kind !== 'group') throw new Error('Missing efficiency group')

    expect(getGroupProgress(efficiency, progress)).toBe('not-started')
    progress['eff-a'] = { status: 'completed', updatedAt: '2026-07-16T09:01:00.000Z' }
    expect(getGroupProgress(efficiency, progress)).toBe('in-progress')
    for (const id of ['eff-a', 'eff-b', 'eff-e', 'eff-circle']) {
      progress[id] = { status: 'completed', updatedAt: '2026-07-16T09:02:00.000Z' }
    }
    expect(getGroupProgress(efficiency, progress)).toBe('completed')
  })
})
