export interface ProductCatalogRecord {
  productId: string;
  productName: string;
  supplierName: string;
  familyName: string;
  modelName: string;
  price: number;
  rating: number;
  reviewCount: number;
  availableQuantity: number;
  isActive: boolean;
}



export interface ProductsByCategoryParameters {
  categoryPath: string;
}
