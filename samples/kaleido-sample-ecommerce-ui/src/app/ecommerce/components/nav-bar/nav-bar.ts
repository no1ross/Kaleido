import { Component, inject, OnDestroy, OnInit, ChangeDetectorRef } from '@angular/core';

import { Subscription } from 'rxjs';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { QueryableService } from '../../../kaleido/services/queryable-service';
import { QueryRequest } from '../../../kaleido/models/queryable-request';

import { ShoppingCartContextStateService } from '../../services/shoppingcart-context-state-service';
import { ECommerceStateService } from '../../services/ecommerce-state-service';
import { ShoppingCartSummaryView, ShoppingCartViewParameters } from '../../models/shopping-cart-models';

@Component({
    selector: 'ecommerce-nav-bar',
    imports: [
        RouterLink,
        RouterLinkActive
    ],
    templateUrl: './nav-bar.html',
    styleUrl: './nav-bar.scss'
})
export class NavBar
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
            QueryRequest<ShoppingCartViewParameters>;

        request.parameters ??= {};

        request.parameters.participantProcessId =
            this.ecommerceState.state.participantProcessId;

        this.queryableService
            .query<
                ShoppingCartSummaryView,
                ShoppingCartViewParameters>(
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