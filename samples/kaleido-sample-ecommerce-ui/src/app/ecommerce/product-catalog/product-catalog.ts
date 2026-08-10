import {
  ChangeDetectorRef,
  Component,
  OnInit,
  inject
} from '@angular/core';

import { ProductCatalogRecord } from '../models/product-catalog-record';
import { QueryRequest } from '../../kaleido/models/queryable-request';
import { QueryableService } from '../../kaleido/services/queryable.service';
import { QueryablePager } from '../../kaleido/queryable-pager/queryable-pager';
import { QueryableSorting } from '../../kaleido/queryable-sorting/queryable-sorting';
import { QueryableFiltering } from '../../kaleido/queryable-filtering/queryable-filtering';
import { QueryableSearch } from '../../kaleido/queryable-search/queryable-search';
import { ProductResults } from '../product-results/product-results';
import { CatalogState } from '../models/catalog-state';


@Component({
  selector: 'ecommerce-product-catalog',
  imports: [ 
    QueryablePager, 
    QueryableSorting,
    QueryableFiltering,
    QueryableSearch,
    ProductResults
  ],
  templateUrl: './product-catalog.html',
  styleUrl: './product-catalog.scss',
})
export class ProductCatalog implements OnInit {

  private readonly queryableService =
    inject(QueryableService);

  private readonly changeDetector =
    inject(ChangeDetectorRef);

  products: ProductCatalogRecord[] = [];

  totalCount = 0;

  isLoading = false;

  errorMessage?: string;

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
    }
  };

  ngOnInit(): void {

    this.loadProducts();
  }

  productQueryChanged(
    productQuery: QueryRequest): void {

    this.catalogState.productQuery =
      productQuery;

    this.loadProducts();
  }

  private loadProducts(): void {

    this.isLoading = true;

    this.errorMessage = '';

    this.queryableService
      .query<ProductCatalogRecord>("products", this.catalogState.productQuery)
      .subscribe({
        next: result => {

          this.products =
            result.records;

          this.totalCount =
            result.totalCount;

          this.isLoading = false;
          
          this.errorMessage = undefined;
          
          this.changeDetector.detectChanges();
        },

        error: error => {

          console.error(error);

          if (error.errors?.length > 0)
          {
            this.errorMessage = error.errors[0].message;
          }
          else
          {
            this.errorMessage = 'An unexpected error occurred.'
          }

          this.isLoading = false;
          
          this.changeDetector.detectChanges();
        }
      });
  }
}