import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from './api'
import { activeStore, can, useAuth } from './auth'
import { catalogApi } from './catalogApi'
import type { CatalogNode } from './catalogTypes'
import { toast } from '../components/ui/Toast'

export const catalogKeys = {
  nodes: ['catalog', 'nodes'] as const,
  brands: ['catalog', 'brands'] as const,
  units: ['catalog', 'units'] as const,
  characteristics: ['catalog', 'characteristics'] as const,
}

export const useCatalogNodes = () => useQuery({ queryKey: catalogKeys.nodes, queryFn: catalogApi.nodes })

/** Каталогқа қатысты рұқсаттар (фронт тек жасырады, соңғы тексеріс — серверде). */
export function useCatalogPermissions() {
  const store = useAuth(activeStore)
  return {
    structure: can(store, 'catalog.structure'),
    lifecycle: can(store, 'catalog.lifecycle'),
    dictionaries: can(store, 'catalog.dictionaries'),
  }
}

/** Әрекетті орындап, тізімді жаңартып, нәтижесін қысқа хабармен көрсетеді. */
export function useCatalogAction(key: readonly unknown[]) {
  const qc = useQueryClient()
  return async (action: () => Promise<unknown>, success: string) => {
    try {
      await action()
      await qc.invalidateQueries({ queryKey: key })
      toast(success)
      return true
    } catch (err) {
      toast(err instanceof ApiError ? err.message : String(err), 'error')
      return false
    }
  }
}

/** Түйін мен оның барлық ұрпағының идентификаторлары (өзін не ұрпағын ата-ана етпеу үшін). */
export function subtreeIds(nodes: CatalogNode[], rootId: string): Set<string> {
  const ids = new Set([rootId])
  let added = true
  while (added) {
    added = false
    for (const n of nodes)
      if (n.parentId && ids.has(n.parentId) && !ids.has(n.id)) {
        ids.add(n.id)
        added = true
      }
  }
  return ids
}

/** Тізімді ағаш ретімен сұрыптау: ата-ана, сосын оның балалары. */
export function treeOrder(nodes: CatalogNode[]): CatalogNode[] {
  const byParent = new Map<string | null, CatalogNode[]>()
  for (const n of nodes) byParent.set(n.parentId, [...(byParent.get(n.parentId) ?? []), n])
  for (const list of byParent.values()) list.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
  const out: CatalogNode[] = []
  const walk = (parent: string | null) => (byParent.get(parent) ?? []).forEach((n) => { out.push(n); walk(n.id) })
  walk(null)
  return out
}
