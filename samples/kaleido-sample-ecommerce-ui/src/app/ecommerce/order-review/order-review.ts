import {
    Component,
    inject,
    ChangeDetectorRef,
    OnInit,
    OnDestroy
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

    private readonly changeDetector =
        inject(ChangeDetectorRef);

    private readonly router =
        inject(Router);

    orderSubscription?: Subscription;

    items: OrderReviewView[] = [];

    errorMessage?: string;

    isLoading = false;

    get totalItems(): number {

        return this.items
            .reduce(
                (total, item) =>
                    total + item.quantity,
                0);
    }

    get orderTotal(): number {

        return this.items
            .reduce(
                (total, item) =>
                    total + item.extendedPrice,
                0);
    }

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

        const request:
            QueryRequest<OrderReviewViewParameters> =
        {
            parameters:
            {
                participantProcessId:
                    this.ecommerceState.state.participantProcessId,

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

                    this.items =
                        result.records;

                    this.errorMessage =
                        undefined;

                    this.changeDetector.detectChanges();
                },

                error: error => {

                    console.error(error);

                    this.errorMessage =
                        'Failed to load order review.';
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