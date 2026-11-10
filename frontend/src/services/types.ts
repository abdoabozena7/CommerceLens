export interface Product {
  id: number
  slug: string
  name: string
  category: string
  description: string
  price: number
  stock: number
  visual: string
  accent: string
  featured: boolean
  sourceType: string
}

export interface Order {
  id: number
  createdAt: string
  sourceType: string
  total: number
  items: { productId: number; name: string; category: string; quantity: number; unitPrice: number }[]
}

export interface ImportError { row: number; column: string; message: string }
export interface ImportResult {
  id: number; fileName: string; status: string; rowCount: number; clickCount: number
  alreadyImported: boolean; importedAt: string; errors: ImportError[]
}

export interface GroupCtr { name: string; observations: number; clicks: number; ctr: number }
export interface MissingValue { column: string; missing: number; total: number; present: number; missingRate: number }
export interface AdAnalytics {
  totalObservations: number; totalClicks: number; ctr: number; distinctUsers: number; rowsWithMissingValues: number
  byDevice: GroupCtr[]; byPosition: GroupCtr[]; byTimeOfDay: GroupCtr[]; byBrowsingHistory: GroupCtr[]
  missingValues: MissingValue[]
}
export interface FilterOptions {
  deviceTypes: string[]; adPositions: string[]; browsingHistories: string[]; timesOfDay: string[]; imports: ImportResult[]
}
export interface ScrapeSummary {
  id: number; status: string; startedAt: string; completedAt: string | null; recordCount: number; error: string | null
}
export interface Overview {
  totalObservations: number; totalHistoricalClicks: number; historicalCtr: number; totalProducts: number
  totalOrders: number; revenue: number; totalEvents: number; liveAdClicks: number; productViews: number
  liveAdImpressions: number; liveCtr: number | null
  totalScrapedRecords: number; lastImportedAt: string | null; lastScrape: ScrapeSummary | null
  latestModel: { trainedAt: string; status: string; isStale: boolean } | null
}
export interface AdRow {
  id: number; originalUserId: number; age: number | null; gender: string | null; deviceType: string; adPosition: string
  browsingHistory: string; timeOfDay: string; click: boolean; importBatchId: number; sourceType: string; importedAt: string
}
export interface Page<T> { items: T[]; total: number; page: number; pageSize: number }
export interface SaleGroup { name: string; units: number; revenue: number; revenueCents: number }
export interface CommerceAnalytics {
  totalOrders: number; revenue: number; averageOrderValue: number; unitsSold: number; sourceType: string
  topProducts: SaleGroup[]; byCategory: SaleGroup[]
  dailySales: { date: string; orders: number; revenue: number }[]; recentOrders: Order[]
}
export interface ProductComparison {
  productId: number; name: string; collectedName: string | null; storefrontPrice: number | null
  collectedPrice: number | null; storefrontCategory: string | null; collectedCategory: string | null
  status: string; differences: string[]; adText: string | null; sourceUrl: string | null; extractedAt: string | null
  catalogAvailability: string | null; collectedAvailability: string | null; priceDifference: number | null
}
export interface ScrapeDetail {
  id: number; status: string; sourceType: string; sourceUrl: string; startedAt: string; completedAt: string | null
  error: string | null; recordCount: number; matchedCount: number; mismatchedCount: number; missingCount: number
  extraCount: number; priceMismatchCount: number; comparisons: ProductComparison[]
}
export interface ModelMetrics {
  trainedAt: string; algorithm: string; datasetFingerprint: string; artifactFileName: string
  trainRows: number; testRows: number; trainUsers: number; testUsers: number; userOverlap: number
  historicalBaselineCtr: number; baselineAccuracy: number; accuracy: number; precision: number; recall: number
  rocAuc: number; f1Score: number; logLoss: number; trainingSeconds: number; features: string[]
  truePositives: number; falsePositives: number; trueNegatives: number; falseNegatives: number
}
export interface ModelStatus { status: string; isStale: boolean; metrics: ModelMetrics | null; currentHistoricalCtr: number }
export interface Prediction {
  probability: number; predictedClick: boolean; historicalBaselineCtr: number; modelDatasetBaselineCtr: number
  modelTrainedAt: string; modelStale: boolean; sourceType: string; engine: string
}
