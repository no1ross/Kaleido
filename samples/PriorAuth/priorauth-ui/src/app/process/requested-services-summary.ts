import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { FilterOperator } from '../kaleido/models/enumerations';
import { QueryErrorResponse } from '../kaleido/models/query-error-response';
import { QueryRequest } from '../kaleido/models/queryable-request';
import { ProcessErrorResponse, ProcessService } from '../kaleido/services/process-service';
import { QueryableService } from '../kaleido/services/queryable-service';
import { buildProcessRoute } from './services/process-navigation';
import { ProcessStateService } from './services/process-state-service';

interface RequestedServiceSummaryResult {
    priorAuthorizationRequestedServiceId: string;
    processId: string;
    userEnteredCodeValue: string;
    userEnteredCodeSystem: string;
    resolvedCodeValue: string;
    resolvedCodeSystem: string;
    description: string;
}

interface RemoveRequestedServiceStep {
    priorAuthorizationRequestedServiceId: string;
}

@Component({
    selector: 'priorauth-requested-services-summary',
    standalone: true,
    templateUrl: './requested-services-summary.html',
    styleUrl: './requested-services-summary.scss'
})
export class RequestedServicesSummary {
    constructor() {
        this.loadRequestedServices();
    }

    private readonly queryableService =
        inject(QueryableService);

    private readonly processService =
        inject(ProcessService);

    readonly processState =
        inject(ProcessStateService);

    private readonly router =
        inject(Router);

    readonly results =
        signal<RequestedServiceSummaryResult[]>([]);
    readonly isLoading =
        signal(false);
    readonly removingServiceId =
        signal<string | undefined>(undefined);
    readonly errorMessage =
        signal<string | undefined>(undefined);

    addAnotherService(): void {
        void this.router.navigate(
            buildProcessRoute(
                this.processState.state().processId,
                'requested-service'));
    }

    selectOrderingProvider(): void {
        void this.router.navigate(
            buildProcessRoute(
                this.processState.state().processId,
                'requesting-provider'));
    }

    removeService(
        requestedServiceId: string
    ): void {
        const processId = this.processState.state().processId;

        if (!processId || this.removingServiceId()) {
            return;
        }

        this.removingServiceId.set(requestedServiceId);
        this.errorMessage.set(undefined);

        this.processService
            .executeStep<RemoveRequestedServiceStep, object>('RemoveRequestedService', {
                processId,
                processStep: {
                    priorAuthorizationRequestedServiceId: requestedServiceId
                }
            })
            .subscribe({
                next: result => {
                    this.removingServiceId.set(undefined);

                    if (result.requiredStep !== 'CaptureRequestedService') {
                        this.loadRequestedServices();
                    }
                },
                error: error => {
                    this.removingServiceId.set(undefined);
                    this.errorMessage.set(this.getProcessErrorMessage(error) ?? 'Unable to remove requested service.');
                }
            });
    }

    isRemoving(
        requestedServiceId: string
    ): boolean {
        return this.removingServiceId() === requestedServiceId;
    }

    private loadRequestedServices(): void {
        const processId = this.processState.state().processId;

        if (!processId) {
            return;
        }

        this.isLoading.set(true);
        this.errorMessage.set(undefined);

        const request: QueryRequest = {
            query: {
                filter: {
                    condition: {
                        field: 'ProcessId',
                        operator: FilterOperator.Equals,
                        values: [processId]
                    }
                },
                page: {
                    size: 50,
                    offset: 0
                }
            }
        };

        this.queryableService
            .queryContext<RequestedServiceSummaryResult>('requested-services', request)
            .subscribe({
                next: result => {
                    this.results.set(result.records);
                    this.isLoading.set(false);
                },
                error: error => {
                    this.results.set([]);
                    this.isLoading.set(false);
                    this.errorMessage.set(this.getErrorMessage(error));
                }
            });
    }

    private getErrorMessage(
        error: unknown
    ): string {
        if (this.isQueryErrorResponse(error)) {
            return error.errors
                .map(message => message.message)
                .join(' ');
        }

        return 'Unable to load requested services.';
    }

    private isQueryErrorResponse(
        error: unknown
    ): error is QueryErrorResponse {
        return typeof error === 'object'
            && error !== null
            && 'errors' in error
            && Array.isArray((error as QueryErrorResponse).errors);
    }

    private getProcessErrorMessage(
        error: unknown
    ): string | undefined {
        if (ProcessErrorResponse.is(error) && error.messages.length > 0) {
            return error.messages
                .map(message => message.message)
                .join(' ');
        }

        return undefined;
    }
}
