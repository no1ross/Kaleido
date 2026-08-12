import {
  Component,
  OnInit,
} from '@angular/core';

import { QueryRequest } from '../../kaleido/models/queryable-request';
import { QueryableService } from '../../kaleido/services/queryable.service';
import { QueryablePager } from '../../kaleido/queryable-pager/queryable-pager';
import { QueryableSorting } from '../../kaleido/queryable-sorting/queryable-sorting';
import { QueryableFiltering } from '../../kaleido/queryable-filtering/queryable-filtering';
import { QueryableSearch } from '../../kaleido/queryable-search/queryable-search';
import { ProductResults } from '../product-results/product-results';
import { CategoryList } from '../category-list/category-list';
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

  catalogState: CatalogState =
  {
    productQuery: {
      query: {
        page: {
          offset: 0,
          size: 25
        },
        sort: []
      }
    },
    productResult: {
      totalCount: 0,
      offset: 0,
      pageSize: 0
    }
  };

  private resetPaging(
      request: QueryRequest): QueryRequest {

      return {
          ...request,
          query:
          {
              ...request.query,
              page:
              {
                  ...request.query.page,
                  offset: 0
              }
          }
      };
  }

  private setProductQuery(
      productQuery: QueryRequest): void {

      this.catalogState =
      {
          ...this.catalogState,
          productQuery
      };
  }

  productQueryChanged(
      productQuery: QueryRequest): void {

      this.setProductQuery(
          productQuery);
  }

  categorySelected(
      categoryPath: string): void {

      this.catalogState =
      {
          ...this.catalogState,
          selectedCategory: categoryPath,
          productQuery:
              this.resetPaging(
                  this.catalogState.productQuery)
      };
  }

  productsLoaded(
      productResult: QueryResponse): void {

      this.catalogState = {
          ...this.catalogState,
          productResult
      };
  }
}