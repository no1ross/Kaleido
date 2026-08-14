import {
  Component,
  inject,
  OnInit,
  ChangeDetectorRef,
  OnDestroy
} from '@angular/core';

import { CurrencyPipe } from '@angular/common';
import { Subscription } from 'rxjs';
import { ProductCatalogView } from '../../models/product-catalog-view';
import { QueryableService } from '../../../kaleido/services/queryable-service';
import { ProcessService } from '../../../kaleido/services/process-service';
import { RequestContextService } from '../../../kaleido/services/request-context-service';
import { QueryResultStateService } from '../../../kaleido/services/query-state-service';
import { ProductContextStateService } from '../../services/product-context-state-service';
import { AddItemToCartStep } from '../../models/steps/add-item-to-cart';
import { ExecuteStepRequest } from '../../models/participant-process-request';
import { ECommerceStateService } from '../../services/ecommerce-state-service';
import { AddItemToCartResponse } from '../../models/steps/add-item-to-cart';
import { ProductsByCategoryParameters } from '../../models/product-catalog-view';
import { ProcessErrorResponse } from '../../../kaleido/services/process-service';

@Component({
  selector: 'ecommerce-product-results',
  standalone: true,
  imports: [
    CurrencyPipe
  ],
  templateUrl: './product-results.html',
  styleUrl: './product-results.scss'
})
export class ProductResults implements OnInit, OnDestroy {
    
    private readonly queryableService =
        inject(QueryableService);

    private readonly processService =
        inject(ProcessService);

    private readonly requestContext =
        inject(RequestContextService);

    private readonly queryState =
        inject(ProductContextStateService);

    private readonly resultState =
        inject(QueryResultStateService);

    private readonly changeDetector =
        inject(ChangeDetectorRef);

    private readonly ecommerceState =
        inject(ECommerceStateService); 

    private querySubscription?: Subscription;
        
    products: ProductCatalogView[] = [];

    isLoading = false;

    errorMessage?: string;

    ngOnInit(): void {

        this.querySubscription =
            this.queryState.changed
                .subscribe(() => {

                    console.log(
                        'PRODUCT RESULTS received query changed');

                    console.log(
                        'PRODUCT RESULTS page state',
                        this.queryState.state.request.query?.page);

                    this.loadProducts();
                });

        this.loadProducts();
    }


    ngOnDestroy(): void {
        this.querySubscription?.unsubscribe();
    }

    private get viewName(): string {

        const parameters =
            this.queryState.state.request.parameters as
                ProductsByCategoryParameters | undefined;

        return parameters?.categoryPath
            ? 'product-by-category'
            : 'product-list';
    }

    private loadProducts(): void {

        this.isLoading = true;

        this.errorMessage = undefined;
    
        const context = 'products';
        const viewName = this.viewName;
        const request = this.queryState.state.request;

        this.queryableService
            .query<ProductCatalogView>(
                context,
                viewName,
                request)
            .subscribe({
                next: result => {

                    this.products =
                        result.records;

                    this.isLoading =
                        false;

                    this.errorMessage =
                        undefined;

                    this.resultState.replace({
                        totalCount: result.totalCount,
                        pageSize: result.pageSize,
                        offset: result.offset
                    });      
                    
                    this.changeDetector.detectChanges();
                },

                error: error => {
                    this.handleError(error);
                }
            });
    }

  private handleError(
    error: ProcessErrorResponse): void {

    this.errorMessage =
        error.messages
            .map(x => x.message)
            .join('\n');

    this.isLoading = false;

    this.changeDetector.detectChanges();
  }

addToCart(
    product: ProductCatalogView): void {

    this.requestContext.beginAction();

    const request: ExecuteStepRequest<AddItemToCartStep> = {

        participantProcessId:
            this.ecommerceState.state.participantProcessId,

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
        .subscribe({
            next: result => {

                this.ecommerceState.state.participantProcessId =
                    result.participantProcessId;

                this.ecommerceState.notifyChanged();
          },
            error: (error: unknown) => {
                if (ProcessErrorResponse.is(error)) {
                    this.handleError(error);
                    return;
                }
                
                this.errorMessage =
                    'An unexpected error occurred.';
            }
        });
  }
}