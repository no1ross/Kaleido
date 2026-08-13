import { Component, inject, OnDestroy, OnInit, ChangeDetectorRef } from '@angular/core';

import {
    RouterLink,
    RouterLinkActive,
    RouterOutlet
} from '@angular/router';

import { Subscription } from 'rxjs';

import { QueryableService }
    from '../kaleido/services/queryable-service';

import { QueryRequest }
    from '../kaleido/models/queryable-request';

import { ECommerceStateService }
    from './services/ecommerce-state-service';

@Component({
    selector: 'app-ecommerce',
    imports: [
        RouterLink,
        RouterLinkActive,
        RouterOutlet
    ],
    templateUrl: './ecommerce.html',
    styleUrl: './ecommerce.scss'
})
export class ECommerce
    implements OnInit, OnDestroy {

    totalItems = 0;

    private readonly queryableService =
        inject(QueryableService);

    private readonly ecommerceState =
        inject(ECommerceStateService);

    private readonly changeDetector =
        inject(ChangeDetectorRef);
        
    private cartSubscription?: Subscription;

    ngOnInit(): void {
        console.log('subscribing to cart');
        this.cartSubscription =
            this.ecommerceState.cartChanged
                .subscribe(() => {

                    console.log('cart changed received');

                    this.loadCartSummary();
                });
    }

    ngOnDestroy(): void {

        this.cartSubscription?.unsubscribe();
    }

    loadCartSummary(): void {

        console.log('loading cart summary');

        const participantProcessId =
            this.ecommerceState.participantProcessId;
        console.log(
            'participantProcessId',
            participantProcessId);

        if (!participantProcessId) {
            this.totalItems = 0;
            return;
        }

        const request: QueryRequest<ShoppingCartSummaryViewParameters> = {
            parameters: {
                participantProcessId
            }
        };

        this.queryableService
            .query<
                ShoppingCartSummaryView,
                ShoppingCartSummaryViewParameters>(
                    'shopping-carts/shopping-cart-summary',
                    request)
            .subscribe({
                next: result => {

                    console.log('cart summary result', result),

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

    itemCount: number;
}

export interface ShoppingCartSummaryViewParameters {

    participantProcessId: string;
}