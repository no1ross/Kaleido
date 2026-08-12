import {
  Component,
  Input,
  inject,
  ChangeDetectorRef,
  Output,
  EventEmitter
} from '@angular/core';

import { CurrencyPipe } from '@angular/common';

import { ProductCatalogRecord } from '../models/product-catalog-record';

import { QueryableService } from '../../kaleido/services/queryable.service';
import { QueryResponse } from '../models/catalog-state';
import { QueryRequest, QueryBody } from '../../kaleido/models/queryable-request';
import { ProductsByCategoryParameters } from '../models/product-catalog-record';

@Component({
  selector: 'ecommerce-product-results',
  standalone: true,
  imports: [
    CurrencyPipe
  ],
  templateUrl: './product-results.html',
  styleUrl: './product-results.scss'
})
export class ProductResults {

    @Input({ required: true })
    productQuery!: QueryRequest;

    @Input()
    selectedCategory?: string;
    
    @Output()
    productsLoaded =
        new EventEmitter<QueryResponse>();
    
    private readonly queryableService =
        inject(QueryableService);

    private readonly changeDetector =
        inject(ChangeDetectorRef);

    products: ProductCatalogRecord[] = [];

    isLoading = false;

    errorMessage?: string;

    ngOnChanges()
    {
       this.loadProducts();
    }

private loadProducts(): void {

  this.isLoading = true;

  this.errorMessage = undefined;

  const viewName =
    this.selectedCategory
      ? 'product-by-category'
      : 'product-list';

  const request: QueryRequest<any> =
    this.selectedCategory
      ? {
          ...this.productQuery,
          parameters: {
            categoryPath: this.selectedCategory
          }
        }
      : this.productQuery;

  this.queryableService
    .query<ProductCatalogRecord>(
      'products/' + viewName,
      request)
    .subscribe({
      next: result => {

        this.products =
          result.records;

        this.isLoading =
          false;

        this.errorMessage =
          undefined;

        this.productsLoaded.emit({
          totalCount: result.totalCount,
          offset: result.offset,
          pageSize: result.pageSize
        });

        // this.changeDetector.detectChanges();
      },

      error: error => {

        this.handleError(error);
      }
    });
}

  private handleError(
    error: any): void {

    console.error(error);

    if (error.errors?.length > 0) {

      this.errorMessage =
        error.errors[0].message;

    } else {

      this.errorMessage =
        'An unexpected error occurred.';
    }

    this.isLoading =
      false;

    //this.changeDetector.detectChanges();
  }
}