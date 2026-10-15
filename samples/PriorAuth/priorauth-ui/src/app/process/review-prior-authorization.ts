import { computed, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { InformationResponse, InformationResponseItem } from '../kaleido/models/information-request';
import { QueryErrorResponse } from '../kaleido/models/query-error-response';
import { ProcessErrorResponse, ProcessService } from '../kaleido/services/process-service';
import { InformationRequestForm } from './information-request-form';
import { PriorAuthorizationReview, ReviewedProvider } from './models/prior-authorization-review';
import { ProcessStateService } from './services/process-state-service';

const ReviewStepName = 'ReviewPriorAuthorizationStep';
const SubmitStepName = 'SubmitPriorAuthorizationStep';

/**
 * Review and submit on one screen, as an agent does it: Review returns the whole request for
 * read-back and presents the configured attestation; answering it submits the request.
 * Review runs on entry when the process requires it (or offers it); once the attestation is
 * pending only Submit can run, so a resumed process shows the attestation without the read-back.
 */
@Component({
    selector: 'priorauth-review-prior-authorization',
    standalone: true,
    imports: [InformationRequestForm],
    templateUrl: './review-prior-authorization.html',
    styleUrl: './review-prior-authorization.scss'
})
export class ReviewPriorAuthorization {
    private readonly processService =
        inject(ProcessService);

    readonly processState =
        inject(ProcessStateService);

    private readonly router =
        inject(Router);

    readonly isReviewing =
        signal(false);
    readonly isSubmitting =
        signal(false);
    readonly isSubmitted =
        signal(false);
    readonly errorMessage =
        signal<string | undefined>(undefined);

    readonly review =
        computed(() => this.processState.state().review);

    /** The configured attestation, presented with the required Submit step. */
    readonly attestation =
        computed(() =>
            this.processState.state().informationStepName === SubmitStepName
                ? this.processState.state().informationRequest
                : undefined);

    constructor() {
        const state = this.processState.state();
        const canReview =
            state.requiredStep === ReviewStepName ||
            (!state.requiredStep && state.availableSteps.some(step => step.name === ReviewStepName));

        if (canReview) {
            this.runReview();
        }
    }

    runReview(): void {
        const processId = this.processState.state().processId;

        if (!processId || this.isReviewing()) {
            return;
        }

        this.isReviewing.set(true);
        this.errorMessage.set(undefined);

        this.processService
            .executeStep<object, PriorAuthorizationReview>(ReviewStepName, {
                processId,
                processStep: {}
            })
            .subscribe({
                next: result => {
                    this.isReviewing.set(false);
                    this.processState.setReview(result.result);
                },
                error: error => {
                    this.isReviewing.set(false);
                    this.errorMessage.set(this.getErrorMessage(error, 'Unable to review the prior authorization.'));
                }
            });
    }

    submit(items: InformationResponseItem[]): void {
        const request = this.attestation();
        const processId = this.processState.state().processId;

        if (!processId || !request || this.isSubmitting()) {
            return;
        }

        this.isSubmitting.set(true);
        this.errorMessage.set(undefined);

        this.processService
            .executeStep<InformationResponse, object>(SubmitStepName, {
                processId,
                processStep: {
                    informationRequestId: request.informationRequestId,
                    items
                }
            })
            .subscribe({
                next: () => {
                    this.isSubmitting.set(false);
                    this.isSubmitted.set(true);
                },
                error: error => {
                    this.isSubmitting.set(false);
                    this.errorMessage.set(this.getErrorMessage(error, 'Unable to submit the prior authorization.'));
                }
            });
    }

    viewHistory(): void {
        this.processState.reset();
        void this.router.navigate(['/history']);
    }

    startNew(): void {
        this.processState.reset();
        void this.router.navigate(['/']);
    }

    describeProvider(
        provider: ReviewedProvider | undefined
    ): string {
        if (!provider) {
            return 'Not captured';
        }

        const network =
            provider.isInNetwork === undefined || provider.isInNetwork === null
                ? ''
                : provider.isInNetwork ? ' · In Network' : ' · Out of Network';

        return `${provider.providerName}${provider.locationName ? ` · ${provider.locationName}` : ''}${network}`;
    }

    private getErrorMessage(
        error: unknown,
        fallback: string
    ): string {
        if (ProcessErrorResponse.is(error) && error.messages.length > 0) {
            return error.messages
                .map(message => message.message)
                .join(' ');
        }

        if (this.isQueryErrorResponse(error)) {
            return error.errors
                .map(e => e.message)
                .join(' ');
        }

        return fallback;
    }

    private isQueryErrorResponse(
        error: unknown
    ): error is QueryErrorResponse {
        return typeof error === 'object'
            && error !== null
            && 'errors' in error
            && Array.isArray((error as QueryErrorResponse).errors);
    }
}
