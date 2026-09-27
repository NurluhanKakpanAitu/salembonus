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

export interface ProductListItem {
  id: string
  name: string
  article: string | null
  barcode: string | null
  imageUrl: string | null
  nodeId: string | null
  pathNames: string[]
  brandId: string | null
  brandName: string | null
  unitId: string
  unitShortName: string | null
  status: CatalogStatus
  updatedAt: string
}

export interface ProductPage {
  items: ProductListItem[]
  total: number
  page: number
  pageSize: number
}

export interface ProductBarcode { barcode: string; isPrimary: boolean }
export interface ProductImage { url: string; isPrimary: boolean }
export interface ProductCharacteristicValue { definitionId: string; value: string }
export interface ProductStock { warehouseId: string; warehouseName: string; storeName: string; quantity: number }

export interface Product {
  id: string
  name: string
  article: string | null
  unitId: string
  brandId: string | null
  nodeId: string | null
  pathNames: string[]
  status: CatalogStatus
  description: string | null
  supplier: string | null
  manufacturer: string | null
  country: string | null
  warrantyMonths: number | null
  shelfLifeDays: number | null
  vatRate: number | null
  isMarked: boolean
  notes: string | null
  barcodes: ProductBarcode[]
  images: ProductImage[]
  characteristics: ProductCharacteristicValue[]
  stock: ProductStock[]
  salePrice: number | null
  purchasePrice: number | null
  createdAt: string
  updatedAt: string
}

export interface OpeningStock {
  warehouseId: string | null
  quantity: number | null
  purchasePrice: number | null
  salePrice: number | null
}

export interface SaveProduct {
  name: string
  article: string | null
  unitId: string | null
  brandId: string | null
  nodeId: string | null
  description: string | null
  supplier: string | null
  manufacturer: string | null
  country: string | null
  warrantyMonths: number | null
  shelfLifeDays: number | null
  vatRate: number | null
  isMarked: boolean
  notes: string | null
  barcodes: ProductBarcode[]
  images: ProductImage[]
  characteristics: ProductCharacteristicValue[]
  opening: OpeningStock | null
}

export interface ProductQuery {
  search?: string
  status?: CatalogStatus | ''
  nodeId?: string
  brandId?: string
  unitId?: string
  page: number
  pageSize: number
}

export interface Warehouse { id: string; name: string; isDefault: boolean }
