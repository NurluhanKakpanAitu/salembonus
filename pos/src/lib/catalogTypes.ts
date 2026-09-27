export type CatalogStatus = 'Active' | 'Archived'

export interface CatalogNode {
  id: string
  parentId: string | null
  name: string
  icon: string | null
  imageUrl: string | null
  description: string | null
  status: CatalogStatus
  sortOrder: number
  depth: number
  pathNames: string[]
  childCount: number
  descendantCount: number
  productCount: number
  updatedAt: string
}

export interface SaveNode {
  parentId: string | null
  name: string
  icon: string | null
  description: string | null
  status: CatalogStatus
  sortOrder?: number | null
}

export interface Brand {
  id: string
  name: string
  logoUrl: string | null
  status: CatalogStatus
  sortOrder: number
  productCount: number
}

export interface Unit {
  id: string
  name: string
  shortName: string
  status: CatalogStatus
  productCount: number
}

export type CharacteristicType = 'Text' | 'Number' | 'List' | 'Boolean' | 'Date' | 'Range'

export interface Characteristic {
  id: string
  name: string
  type: CharacteristicType
  isRequired: boolean
  status: CatalogStatus
  sortOrder: number
  options: string[]
  productCount: number
}
