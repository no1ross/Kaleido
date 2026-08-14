import { Component, inject, OnDestroy, OnInit, ChangeDetectorRef } from '@angular/core';

import { Subscription } from 'rxjs';

import { QueryableService } from '../../../kaleido/services/queryable-service';
import { QueryRequest } from '../../../kaleido/models/queryable-request';

import { ShoppingCartContextStateService } from '../../services/shoppingcart-context-state-service';
import { ECommerceStateService } from '../../services/ecommerce-state-service';

@Component({
    selector: 'ecommerce-cart-summary',
    templateUrl: './shopping-cart-summary.html',
    styleUrl: './shopping-cart-summary.scss'
})
export class ShoppingCartSummary
    implements OnInit, OnDestroy {

    totalItems = 0;

    private readonly queryableService =
        inject(QueryableService);

    private readonly changeDetector =
        inject(ChangeDetectorRef);
        
    private readonly cartState =
        inject(ShoppingCartContextStateService); 

    private readonly ecommerceState =
        inject(ECommerceStateService); 

    private cartSubscription?: Subscription;

    ngOnInit(): void {
        
        this.loadCartSummary();

        this.cartSubscription =
            this.ecommerceState.changed
                .subscribe(() => {

                    this.loadCartSummary();
                });
    }

    ngOnDestroy(): void {

        this.cartSubscription?.unsubscribe();
    }

    loadCartSummary(): void {

        const request = this.cartState.state.request as
            QueryRequest<ShoppingCartSummaryViewParameters>;

        request.parameters ??= {};

        request.parameters.participantProcessId =
            this.ecommerceState.state.participantProcessId;

        this.queryableService
            .query<
                ShoppingCartSummaryView,
                ShoppingCartSummaryViewParameters>(
                    'shopping-carts',
                    'shopping-cart-summary',
                    request)
            .subscribe({
                next: result => {

                    this.totalItems =
                        result.records.length > 0
                            ? result.records[0].itemCount
                            : 0;

                    this.changeDetector.detectChanges();
                },

                error: error => {

                    console.error(
                        'Failed to load cart summary.',
                        error);

                    this.totalItems = 0;
                }
            });
    }
}

export interface ShoppingCartSummaryView {

    participantProcessId?: string;

    shoppingCartId?: string;

    itemCount: number;

    totalPrice: number;
}

export interface ShoppingCartSummaryViewParameters {

    participantProcessId?: string;

    customerId?: string;
}