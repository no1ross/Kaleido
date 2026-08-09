import {
  ChangeDetectorRef,
  Component,
  OnInit,
  inject
} from '@angular/core';

import {
  CurrencyPipe
} from '@angular/common';

import { ProductCatalogRecord } from '../models/product-catalog-record';
import { QueryRequest } from '../../kaleido/models/queryable-request';
import { QueryableService } from '../../kaleido/services/queryable.service';
import { QueryablePager } from '../../kaleido/queryable-pager/queryable-pager';
import { QueryableSorting } from '../../kaleido/queryable-sorting/queryable-sorting';
import { QueryableSortField } from '../../kaleido/models/queryable-sort-field';
import { QuerySort, QueryFilterNode } from '../../kaleido/models/queryable-request';
import { QueryableFiltering } from '../../kaleido/queryable-filtering/queryable-filtering';
import { LogicalOperator } from '../../kaleido/models/logical-operator';
import { QueryableFilterField } from '../../kaleido/queryable-filtering/queryable-filter-field';

@Component({
  selector: 'ecommerce-product-catalog',
  imports: [ 
    QueryablePager, 
    QueryableSorting,
    QueryableFiltering,
    CurrencyPipe
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

  queryRequest: QueryRequest = {
    query: {
      page: {
        size: 25,
        offset: 0
      },
      sort: []
    }
  };

  ngOnInit(): void {

    this.loadProducts();
  }

  queryRequestChanged(
    queryRequest: QueryRequest): void {

    this.queryRequest =
      queryRequest;

    this.loadProducts();
  }

  private loadProducts(): void {

    this.isLoading = true;

    this.errorMessage = '';

    this.queryableService
      .query<ProductCatalogRecord>("products", this.queryRequest)
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