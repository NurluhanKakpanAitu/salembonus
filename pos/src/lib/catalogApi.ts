import { api } from './api'
import type { Brand, CatalogNode, Characteristic, CharacteristicType, CatalogStatus, SaveNode, Unit } from './catalogTypes'

const base = '/pos/v1/catalog'
const json = (method: string, body?: unknown): RequestInit => ({ method, body: body === undefined ? undefined : JSON.stringify(body) })

/** Каталог API: классификация және анықтамалықтар (бүкіл бизнеске ортақ). */
export const catalogApi = {
  nodes: () => api<CatalogNode[]>(`${base}/nodes`),
  createNode: (body: SaveNode) => api<CatalogNode>(`${base}/nodes`, json('POST', body)),
  updateNode: (id: string, body: SaveNode) => api<CatalogNode>(`${base}/nodes/${id}`, json('PUT', body)),
  reorderNode: (id: string, direction: 'up' | 'down') => api<void>(`${base}/nodes/${id}/reorder`, json('POST', { direction })),
  archiveNode: (id: string) => api<void>(`${base}/nodes/${id}/archive`, json('POST')),
  restoreNode: (id: string) => api<void>(`${base}/nodes/${id}/restore`, json('POST')),
  deleteNode: (id: string) => api<void>(`${base}/nodes/${id}`, json('DELETE')),
  moveContent: (id: string, targetId: string) => api<void>(`${base}/nodes/${id}/move-content`, json('POST', { targetId })),

  brands: () => api<Brand[]>(`${base}/brands`),
  createBrand: (body: SaveBrand) => api<Brand>(`${base}/brands`, json('POST', body)),
  updateBrand: (id: string, body: SaveBrand) => api<Brand>(`${base}/brands/${id}`, json('PUT', body)),
  reorderBrand: (id: string, direction: 'up' | 'down') => api<void>(`${base}/brands/${id}/reorder`, json('POST', { direction })),

  units: () => api<Unit[]>(`${base}/units`),
  createUnit: (body: { name: string; shortName: string; status: CatalogStatus }) => api<Unit>(`${base}/units`, json('POST', body)),
  updateUnit: (id: string, body: { name: string; shortName: string; status: CatalogStatus }) =>
    api<Unit>(`${base}/units/${id}`, json('PUT', body)),

  characteristics: () => api<Characteristic[]>(`${base}/characteristics`),
  createCharacteristic: (body: SaveCharacteristic) => api<Characteristic>(`${base}/characteristics`, json('POST', body)),
  updateCharacteristic: (id: string, body: SaveCharacteristic) =>
    api<Characteristic>(`${base}/characteristics/${id}`, json('PUT', body)),
  reorderCharacteristic: (id: string, direction: 'up' | 'down') =>
    api<void>(`${base}/characteristics/${id}/reorder`, json('POST', { direction })),
}

export interface SaveCharacteristic {
  name: string
  type: CharacteristicType
  isRequired: boolean
  options: string[]
  status: CatalogStatus
}

export interface SaveBrand {
  name: string
  logoUrl: string | null
  status: CatalogStatus
}
