import {
  Component,
  OnInit,
} from '@angular/core';

import { QueryRequest } from '../../kaleido/models/queryable-request';
import { QueryableService } from '../../kaleido/services/queryable-service';
import { QueryablePager } from '../../kaleido/queryable-pager/queryable-pager';
import { QueryableSorting } from '../../kaleido/queryable-sorting/queryable-sorting';
import { QueryableFiltering } from '../../kaleido/queryable-filtering/queryable-filtering';
import { QueryableSearch } from '../../kaleido/queryable-search/queryable-search';
import { ProductResults } from '../components/product-results/product-results';
import { CategoryList } from '../components/category-list/category-list';
import { CatalogState, QueryResponse } from '../models/catalog-state';

@Component({
  selector: 'ecommerce-product-catalog',
  imports: [
    QueryablePager,
    QueryableSorting,
    QueryableFiltering,
    QueryableSearch,
    ProductResults,
    CategoryList
  ],
  templateUrl: './product-catalog.html',
  styleUrl: './product-catalog.scss',
})
export class ProductCatalog {

  catalogState: CatalogState = { 
    productQuery: {}
  };

  private setProductQuery(
      productQuery: QueryRequest): void {

      this.catalogState =
      {
          ...this.catalogState,
          productQuery
      };
  }


  productQueryChange(
      productQuery: QueryRequest): void {

      this.setProductQuery(
          productQuery);
  }

  queryCategoryChanged(
      productQuery: QueryRequest): void {

      this.setProductQuery(
          productQuery);
  }

  productsLoaded(
      productResult: QueryResponse): void {

      this.catalogState = {
          ...this.catalogState,
          productResult
      };
  }
}