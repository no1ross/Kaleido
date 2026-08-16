import { Component, inject, OnDestroy, OnInit, ChangeDetectorRef } from '@angular/core';

import { Subscription } from 'rxjs';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { QueryableService } from '../../../kaleido/services/queryable-service';
import { QueryRequest } from '../../../kaleido/models/queryable-request';

import { ShoppingCartContextStateService } from '../../services/shoppingcart-context-state-service';
import { ECommerceStateService } from '../../services/ecommerce-state-service';
import { ShoppingCartSummaryView, ShoppingCartViewParameters } from '../../models/shopping-cart-models';

import { ProcessService } from '../../../kaleido/services/process-service';
import { ExecuteStepRequest } from '../../models/participant-process-request';
import { ReconcileCartOwnershipStep, ReconcileCartOwnershipResponse } from '../../models/steps/reconcile-cart-ownership';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'ecommerce-nav-bar',
    imports: [
        RouterLink,
        RouterLinkActive,
        CommonModule
    ],
    templateUrl: './nav-bar.html',
    styleUrl: './nav-bar.scss'
})
export class NavBar
    implements OnInit, OnDestroy {

    itemCount = 0;

    private readonly queryableService =
        inject(QueryableService);

    private readonly changeDetector =
        inject(ChangeDetectorRef);
        
    private readonly cartState =
        inject(ShoppingCartContextStateService); 

    private readonly ecommerceState =
        inject(ECommerceStateService); 

    private readonly processService =
        inject(ProcessService);
        
    private cartSubscription?: Subscription;

    customers: CustomerPersonaView[] = [];

    activeCustomerId?: string;

    ngOnInit(): void {
        
        this.loadCartSummary();

        this.loadCustomers();

        this.cartSubscription =
            this.ecommerceState.changed
                .subscribe(() => {

                    this.loadCartSummary();
                });
    }

    ngOnDestroy(): void {

        this.cartSubscription?.unsubscribe();
    }

    loadCustomers(): void {
        const request =
            this.cartState.state.request as
                QueryRequest<CustomerPersonaParameters>;

        request.parameters ??= {};

        this.queryableService
            .query<
                CustomerPersonaView,
                CustomerPersonaParameters>(
                    'customer-context',
                    request)
            .subscribe({

                next: result => {

                    this.customers =
                        result.records;

                    this.changeDetector.detectChanges();
                }
            });
    }

    loadCartSummary(): void {

        const request = this.cartState.state.request as
            QueryRequest<ShoppingCartViewParameters>;

        request.parameters ??= {};

        request.parameters.participantProcessId =
            this.ecommerceState.state.participantProcessId;
        
        request.parameters.customerId =
            this.ecommerceState.state.customerId;

        this.queryableService
            .query<
                ShoppingCartSummaryView,
                ShoppingCartViewParameters>(
                    'shopping-cart-summary',
                    request)
            .subscribe({
                next: result => {

                    this.itemCount =
                        result.records.length > 0
                            ? result.records[0].itemCount
                            : 0;

                    const recoveredProcessId =
                        result.records[0].participantProcessId;

                    if (
                        recoveredProcessId &&
                        recoveredProcessId !==
                            this.ecommerceState.state.participantProcessId)
                    {
                        this.ecommerceState.state.participantProcessId =
                            recoveredProcessId;

                        this.ecommerceState.notifyChanged();
                    }
                    
                    this.changeDetector.detectChanges();
                },

                error: error => {

                    console.error(
                        'Failed to load cart summary.',
                        error);

                    this.itemCount = 0;
                }
            });
    }


    onCustomerChanged(
        event: Event): void {

        const newCustomerId =
            (event.target as HTMLSelectElement)
                .value || undefined;

        const previousCustomerId =
            this.ecommerceState.state.customerId;

        const hasCurrentProcess =
            !!this.ecommerceState.state.participantProcessId;    
    
        //
        // Scenario:
        // Customer -> Anonymous
        //
        if (previousCustomerId && !newCustomerId)
        {
            this.activeCustomerId =
                undefined;

            this.ecommerceState.state.customerId =
                undefined;

            this.ecommerceState.state.participantProcessId =
                undefined;

            this.ecommerceState.notifyChanged();

            return;
        }

        //
        // Scenario:
        // Customer -> Customer
        //
        if (previousCustomerId &&
            newCustomerId &&
            previousCustomerId !== newCustomerId)
        {
            this.activeCustomerId =
                newCustomerId;

            this.ecommerceState.state.customerId =
                newCustomerId;

            this.ecommerceState.state.participantProcessId =
                undefined;

            this.ecommerceState.notifyChanged();

            return;
        }

        //
        // Scenario:
        // Anonymous -> Customer
        //
        if (!previousCustomerId &&
            newCustomerId)
        {
                if (!hasCurrentProcess)
                {
                    //
                    // No anonymous process.
                    // Nothing to reconcile.
                    // Just switch personas and let Queryable
                    // recover the cart/process.
                    //
                    this.activeCustomerId =
                        newCustomerId;

                    this.ecommerceState.state.customerId =
                        newCustomerId;

                    this.ecommerceState.state.participantProcessId =
                        undefined;

                    this.ecommerceState.notifyChanged();

                    return;
                }

                //
                // We have an anonymous process.
                // Execute ReconcileCartOwnership.
                //
                const request:
                ExecuteStepRequest<ReconcileCartOwnershipStep> =
            {
                participantProcessId:
                    this.ecommerceState.state.participantProcessId,

                processStep:
                {
                    customerId:
                        newCustomerId
                }
            };

            this.processService
                .executeStep<
                    ReconcileCartOwnershipStep,
                    ReconcileCartOwnershipResponse>(
                        'reconcile-cart',
                        request)
                .subscribe({

                    next: () => {

                        this.activeCustomerId =
                            newCustomerId;

                        this.ecommerceState.state.customerId =
                            newCustomerId;

                        //
                        // Leave the process id alone.
                        // ReconcileCartOwnership owns the
                        // current anonymous process.
                        //

                        this.ecommerceState.notifyChanged();
                    },

                    error: error => {

                        console.error(
                            'Failed to reconcile cart ownership',
                            error);
                    }
                });

            return;
        }
    }
}

export interface CustomerPersonaView {

    customerId: string;

    displayName: string;

    email: string;
}

export interface CustomerPersonaParameters {

    customerId?: string;
}