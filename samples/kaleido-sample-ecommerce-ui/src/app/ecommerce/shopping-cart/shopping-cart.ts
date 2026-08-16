import { Component, inject, ChangeDetectorRef, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';

import { CurrencyPipe } from "@angular/common";
import { Subscription } from 'rxjs';
import { QueryableService } from '../../kaleido/services/queryable-service';
import { QueryRequest } from '../../kaleido/models/queryable-request';
import { ShoppingCartContextStateService } from '../services/shoppingcart-context-state-service';
import { ECommerceStateService } from '../services/ecommerce-state-service';
import { ShoppingCartDetailView, ShoppingCartViewParameters } from '../models/shopping-cart-models';
import { RequestContextService } from '../../kaleido/services/request-context-service';
import { ExecuteStepRequest } from '../models/participant-process-request';
import { ProcessService, ProcessErrorResponse } from '../../kaleido/services/process-service';
import { RemoveCartItemStep, RemoveCartItemResponse } from '../models/steps/remove-item-from-cart';
import { UpdateCartItemStep, UpdateCartItemResponse } from '../models/steps/update-cart-item';
import { ProcessCartStep, ProcessCartResponse } from '../models/steps/process-cart';

@Component({
    selector: 'ecommerce-shopping-cart',
    standalone: true,

    imports: [
        CurrencyPipe
    ],

    templateUrl: './shopping-cart.html',
    styleUrl: './shopping-cart.scss',

    providers: [
        ShoppingCartContextStateService
    ]
})
export class ShoppingCart
    implements OnInit, OnDestroy
{
    private readonly queryableService =
        inject(QueryableService);

    private readonly ecommerceState =
        inject(ECommerceStateService);

    private readonly cartState =
        inject(ShoppingCartContextStateService);

    private readonly changeDetector =
        inject(ChangeDetectorRef);

    private readonly requestContext =
        inject(RequestContextService);

    private readonly processService =
        inject(ProcessService);

    private readonly router =
        inject(Router);

    items: ShoppingCartDetailView[] = [];

    cartSubscription?: Subscription;

    errorMessage?: string;

    shoppingCartItemId?: string;

    isLoading = false;

    ngOnInit(): void {

        this.loadCart();

        this.cartSubscription =
        this.ecommerceState.changed
            .subscribe(() => {
                this.loadCart();
            });
    }


    ngOnDestroy(): void {

        this.cartSubscription?.unsubscribe();
    }

    get totalItems(): number {

        return this.items
            .reduce(
                (total, item) =>
                    total + item.quantity,
                0);
    }
        
    get cartTotal(): number {

        return this.items
            .reduce(
                (total, item) =>
                    total + item.extendedPrice,
                0);
    }

    get canCheckout(): boolean {

        return !!this.ecommerceState.state.customerId &&
            this.items.length > 0;
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

    private loadCart(): void {

        const request =
            this.cartState.state.request as
                QueryRequest<ShoppingCartViewParameters>;

        request.parameters ??= {};

        request.parameters.participantProcessId =
            this.ecommerceState.state.participantProcessId;

        request.parameters.customerId =
            this.ecommerceState.state.customerId;

        this.queryableService
            .query<
                ShoppingCartDetailView,
                ShoppingCartViewParameters>(
                    'shopping-cart-detail',
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
                        'Failed to load shopping cart.';
                }
            });
    }

    removeItem(
        item: ShoppingCartDetailView): void {

        this.requestContext.beginAction();

        const request: ExecuteStepRequest<RemoveCartItemStep> = {
            participantProcessId:
                this.ecommerceState.state.participantProcessId,
            processStep: {
                shoppingCartId:
                    item.shoppingCartId,
                shoppingCartItemId:
                    item.shoppingCartItemId
            }
        };

        this.processService
            .executeStep<
            RemoveCartItemStep,
            RemoveCartItemResponse>(
                'remove-cart-item',
                request)
            .subscribe({
                next: result => {

                    this.loadCart();

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
    
    increaseQuantity(
        item: ShoppingCartDetailView): void {

        this.updateQuantity(
            item,
            item.quantity + 1);
    }

    decreaseQuantity(
        item: ShoppingCartDetailView): void {

        if (item.quantity <= 1) {

            this.removeItem(item);
            return;
        }

        this.updateQuantity(
            item,
            item.quantity - 1);
    }

    private updateQuantity(
        item: ShoppingCartDetailView,
        quantity: number): void {

        const request: ExecuteStepRequest<UpdateCartItemStep> =
        {
            participantProcessId:
                this.ecommerceState.state.participantProcessId,

            processStep:
            {
                shoppingCartId:
                    item.shoppingCartId,

                shoppingCartItemId:
                    item.shoppingCartItemId,

                quantity
            }
        };

        this.processService
            .executeStep<
            UpdateCartItemStep,
            UpdateCartItemResponse>(
                'update-cart-item',
                request)
            .subscribe({
                next: result => {

                    this.loadCart();

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

    checkout(): void {

        const request: ExecuteStepRequest<ProcessCartStep> =
        {
            participantProcessId:
                this.ecommerceState.state.participantProcessId,

            processStep:
            {
                shoppingCartId:
                    this.items[0].shoppingCartId,

                customerId:
                    this.ecommerceState.state.customerId!
            }
        };

        this.processService
            .executeStep<
                ProcessCartStep,
                ProcessCartResponse>(
                    'process-cart',
                    request)
            .subscribe({

                next: () => {

                    this.router.navigate(
                        ['/ecommerce/order-details']);
                },

                error: error => {
                        console.error(
                            'Failed to redirect to order review',
                            error);                }
            });
    }
}
