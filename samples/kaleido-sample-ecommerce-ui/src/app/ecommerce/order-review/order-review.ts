import {
    Component,
    computed,
    inject,
    OnInit,
    OnDestroy,
    signal
} from '@angular/core';

import { Router } from '@angular/router';

import {
    CurrencyPipe
} from '@angular/common';

import {
    Subscription
} from 'rxjs';

import {
    QueryableService
} from '../../kaleido/services/queryable-service';

import {
    QueryRequest
} from '../../kaleido/models/queryable-request';

import {
    ECommerceStateService
} from '../services/ecommerce-state-service';

import {
    OrderReviewView,
    OrderReviewViewParameters
} from '../models/order-models';

@Component({
    selector: 'ecommerce-order-review',
    standalone: true,

    imports: [
        CurrencyPipe
    ],

    templateUrl: './order-review.html',
    styleUrl: './order-review.scss'
})
export class OrderReview
    implements OnInit, OnDestroy
{
    private readonly queryableService =
        inject(QueryableService);

    private readonly ecommerceState =
        inject(ECommerceStateService);

    private readonly router =
        inject(Router);

    orderSubscription?: Subscription;

    readonly items =
        signal<OrderReviewView[]>([]);

    readonly errorMessage =
        signal<string | undefined>(undefined);

    readonly isLoading =
        signal(false);

    readonly totalItems =
        computed(() =>
            this.items()
                .reduce(
                    (total, item) =>
                        total + item.quantity,
                    0));

    readonly orderTotal =
        computed(() =>
            this.items()
                .reduce(
                    (total, item) =>
                        total + item.extendedPrice,
                    0));

    ngOnInit(): void {

        this.loadOrder();

        this.orderSubscription =
            this.ecommerceState.changed
                .subscribe(() => {

                    this.loadOrder();
                });
    }

    ngOnDestroy(): void {

        this.orderSubscription?.unsubscribe();
    }

    private loadOrder(): void {

        this.isLoading.set(true);
        this.errorMessage.set(undefined);

        const request:
            QueryRequest<OrderReviewViewParameters> =
        {
            parameters:
            {
                processId:
                    this.ecommerceState.state.processId,

                customerId:
                    this.ecommerceState.state.customerId
            }
        };

        this.queryableService
            .query<
                OrderReviewView,
                OrderReviewViewParameters>(
                    'order-review',
                    request)
            .subscribe({

                next: result => {

                    this.items.set(
                        result.results);

                    this.errorMessage.set(
                        undefined);

                    this.isLoading.set(false);
                },

                error: error => {

                    console.error(error);

                    this.errorMessage.set(
                        'Failed to load order review.');

                    this.isLoading.set(false);
                }
            });
    }

    returnToCart(): void {

        this.router.navigate(
            ['/ecommerce/shopping-cart']);
    }

    submitOrder(): void {

        //
        // TODO:
        // Execute SubmitOrder step.
        //
    }
}
