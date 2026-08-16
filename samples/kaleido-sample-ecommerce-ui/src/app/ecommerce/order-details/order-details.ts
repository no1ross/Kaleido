import {
    Component,
    inject,
    ChangeDetectorRef,
    OnInit,
    OnDestroy
} from '@angular/core';

import {
    Router
} from '@angular/router';

import {
    CurrencyPipe,
    DatePipe
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
    OrderDetailsView,
    OrderDetailsViewParameters
} from '../models/order-models';

import { RequestContextService } from '../../kaleido/services/request-context-service';
import { ExecuteStepRequest } from '../models/participant-process-request';
import { SubmitOrderStep, SubmitOrderResponse } from '../models/steps/submit-order';
import { ProcessService, ProcessErrorResponse } from '../../kaleido/services/process-service';

@Component({
    selector: 'ecommerce-order-details',
    standalone: true,

    imports: [
        CurrencyPipe,
        DatePipe
    ],

    templateUrl: './order-details.html',
    styleUrl: './order-details.scss'
})
export class OrderDetails
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

    private readonly requestContext =
        inject(RequestContextService);

    private readonly processService =
        inject(ProcessService);

    orderSubscription?: Subscription;

    items: OrderDetailsView[] = [];

    errorMessage?: string;

    isLoading = false;

    get order(): OrderDetailsView | undefined {

        return this.items.length > 0
            ? this.items[0]
            : undefined;
    }

    get totalItems(): number {

        return this.items.reduce(
            (total, item) =>
                total + item.quantity,
            0);
    }

    get orderTotal(): number {

        return this.items.reduce(
            (total, item) =>
                total + item.extendedPrice,
            0);
    }

    get isStarted(): boolean {

        return this.order?.status === 'Started';
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

    private handleError(
        error: ProcessErrorResponse): void {

        this.errorMessage =
            error.messages
                .map(x => x.message)
                .join('\n');

        this.isLoading = false;

        this.changeDetector.detectChanges();
    }

    private loadOrder(): void {

        const request:
            QueryRequest<OrderDetailsViewParameters> =
        {
            parameters:
            {
                participantProcessId:
                    this.ecommerceState.state
                        .participantProcessId,

                customerId:
                    this.ecommerceState.state
                        .customerId
            }
        };

        this.queryableService
            .query<
                OrderDetailsView,
                OrderDetailsViewParameters>(
                    'order-details',
                    request)
            .subscribe({

                next: result => {

                    this.items =
                        result.records;

                    this.errorMessage =
                        undefined;

                    this.changeDetector
                        .detectChanges();
                },

                error: error => {

                    console.error(error);

                    this.errorMessage =
                        'Failed to load order details.';
                }
            });
    }

    returnToCart(): void {

        this.router.navigate(
            ['/ecommerce/shopping-cart']);
    }

    submitOrder(): void {

        if (!this.order)
        {
            return;
        }

        this.requestContext.beginAction();

        const request:
            ExecuteStepRequest<SubmitOrderStep> =
        {
            participantProcessId:
                this.ecommerceState.state
                    .participantProcessId!,

            processStep:
            {
                participantProcessId:
                    this.ecommerceState.state
                        .participantProcessId!,

                customerId:
                    this.ecommerceState.state
                        .customerId!,

                orderId:
                    this.order.orderId
            }
        };

        this.processService
            .executeStep<
                SubmitOrderStep,
                SubmitOrderResponse>(
                    'submit-order',
                    request)
            .subscribe({

                next: () => {

                    this.errorMessage =
                        undefined;

                    this.loadOrder();

                    this.ecommerceState.notifyChanged();
                },

                error: (error: unknown) => {

                    if (ProcessErrorResponse.is(error))
                    {
                        this.handleError(
                            error);

                        return;
                    }

                    this.errorMessage =
                        'An unexpected error occurred while submitting the order.';
                }
            });
    }
}