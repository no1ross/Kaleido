export interface ProductCatalogRecord {
  productId: string;
  productName: string;
  supplierName: string;
  categoryName: string;
  categoryPath: string;
  price: number;
  rating: number;
  reviewCount: number;
  availableQuantity: number;
  isActive: boolean;
}