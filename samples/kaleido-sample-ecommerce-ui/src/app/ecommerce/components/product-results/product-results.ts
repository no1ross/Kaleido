import {
  Component,
  Input,
  inject,
  Output,
  EventEmitter
} from '@angular/core';

import { CurrencyPipe } from '@angular/common';

import { ProductCatalogView } from '../../models/product-catalog-view';

import { QueryableService } from '../../../kaleido/services/queryable-service';
import { QueryResponse } from '../../models/catalog-state';
import { QueryRequest } from '../../../kaleido/models/queryable-request';

import { ExecuteStepRequest } from '../../../kaleido/models/participant-process-request';
import { AddItemToCartStep, AddItemToCartResponse } from '../../models/steps/add-item-to-cart';

import { ProcessService } from '../../../kaleido/services/process-service';

import { ECommerceStateService } from '../../services/ecommerce-state-service';
import { RequestContextService } from '../../../kaleido/services/request-context-service';
import { ProductsByCategoryParameters } from '../../models/product-catalog-view';

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
    
    @Output()
    productsLoaded =
        new EventEmitter<QueryResponse>();

    private readonly ecommerceState =
        inject(ECommerceStateService);        
    
    private readonly queryableService =
        inject(QueryableService);

    private readonly processService =
        inject(ProcessService);

    private readonly requestContext =
        inject(RequestContextService);
        
    products: ProductCatalogView[] = [];

    isLoading = false;

    errorMessage?: string;

    ngOnChanges() {
       this.loadProducts();
    }

  private get categoryPath(): string | undefined {

      const parameters =
          this.productQuery.parameters as
              ProductsByCategoryParameters | undefined;

      return parameters?.categoryPath;
  }

private loadProducts(): void {

  this.isLoading = true;

  this.errorMessage = undefined;

  const categoryPath =
    this.categoryPath;

  const viewName =
      categoryPath
          ? 'product-by-category'
          : 'product-list';

  const request: QueryRequest<any> =
    this.categoryPath
      ? {
          ...this.productQuery,
          parameters: {
            categoryPath: this.categoryPath
          }
        }
      : this.productQuery;

  this.queryableService
    .query<ProductCatalogView>(
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
  }

addToCart(
    product: ProductCatalogView): void {

    this.requestContext.beginAction();

    const request: ExecuteStepRequest<AddItemToCartStep> = {

        participantProcessId:
            this.ecommerceState.participantProcessId,

        processStep:
        {
            itemId:
                product.productId,

            quantity:
                1
        }
    };

    this.processService
        .executeStep<
        AddItemToCartStep,
        AddItemToCartResponse>(
            'AddItemToCart',
            request)
          .subscribe(result => {

            console.log('PROCESS RETURNED');

              this.ecommerceState.participantProcessId =
                  result.participantProcessId;

                  console.log('ABOUT TO NOTIFY');

              this.ecommerceState.notifyCartChanged();

          });
  }
}