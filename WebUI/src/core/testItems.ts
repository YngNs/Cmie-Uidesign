import { TEST_TREE } from './data'
import type {
  FlatTestItem,
  ProjectProgress,
  TestItemGroup,
  TestItemNode,
  TestItemStatus,
} from './types'

export function flattenTestItems(
  nodes: readonly TestItemNode[],
  parentPath: readonly string[] = [],
  parentGroupIds: readonly string[] = [],
): FlatTestItem[] {
  return nodes.flatMap(node => {
    const path = [...parentPath, node.label]
    if (node.kind === 'group') {
      return flattenTestItems(node.children, path, [...parentGroupIds, node.id])
    }
    return [{ ...node, path, groupIds: [...parentGroupIds] }]
  })
}

export const TEST_ITEMS = Object.freeze(flattenTestItems(TEST_TREE))
export const TEST_ITEM_MAP: ReadonlyMap<string, FlatTestItem> = new Map(
  TEST_ITEMS.map(item => [item.id, item]),
)

export function createProjectProgress(now = new Date().toISOString()): ProjectProgress {
  return Object.fromEntries(TEST_ITEMS.map(item => [item.id, { status: 'not-started', updatedAt: now }]))
}

export function getGroupProgress(group: TestItemGroup, progress: ProjectProgress): TestItemStatus {
  const statuses = flattenTestItems(group.children).map(item => progress[item.id]?.status ?? 'not-started')
  if (statuses.every(status => status === 'completed')) return 'completed'
  if (statuses.every(status => status === 'not-started')) return 'not-started'
  return 'in-progress'
}

export function countCompletedItems(progress: ProjectProgress): number {
  return TEST_ITEMS.filter(item => progress[item.id]?.status === 'completed').length
}
