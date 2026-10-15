import { computed, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { InformationResponse, InformationResponseItem } from '../kaleido/models/information-request';
import { QueryErrorResponse } from '../kaleido/models/query-error-response';
import { ProcessService, ProcessErrorResponse } from '../kaleido/services/process-service';
import { InformationRequestForm } from './information-request-form';
import { buildProcessRoute } from './services/process-navigation';
import { ProcessStateService } from './services/process-state-service';
import { getRouteForStep } from './services/step-route';

/** Route data for an information step screen. */
export interface InformationStepRouteData {
    /** The information step the questions belong to. */
    stepName: string;
    eyebrow: string;
    /** Heading when the request has no title. */
    title: string;
    submitLabel: string;
    /** Shown when no questions are pending for this step. */
    emptyMessage: string;
    /** Shown when the step fails without messages. */
    failureMessage: string;
    /** Where to go when the step requires nothing next (otherwise the required step's screen). */
    nextRoute?: string;
}

/**
 * Presents the pending information request of one information step (MRI questions, CT
 * confirmation, clinical rounds) and submits the answers. A new round of the same step replaces
 * the questions in place.
 */
@Component({
    selector: 'priorauth-information-step',
    standalone: true,
    imports: [InformationRequestForm],
    templateUrl: './information-step.html',
    styleUrl: './information-step.scss'
})
export class InformationStep {
    private readonly processService =
        inject(ProcessService);

    private readonly processState =
        inject(ProcessStateService);

    private readonly router =
        inject(Router);

    readonly config =
        inject(ActivatedRoute).snapshot.data as InformationStepRouteData;

    readonly isSubmitting =
        signal(false);
    readonly errorMessage =
        signal<string | undefined>(undefined);

    /** The pending questions, presented with the required step (requiredStep.informationRequest). */
    readonly informationRequest =
        computed(() =>
            this.processState.state().informationStepName === this.config.stepName
                ? this.processState.state().informationRequest
                : undefined);

    submit(items: InformationResponseItem[]): void {
        const request = this.informationRequest();
        const processId = this.processState.state().processId;

        if (!processId || !request || this.isSubmitting()) {
            return;
        }

        this.isSubmitting.set(true);
        this.errorMessage.set(undefined);

        this.processService
            .executeStep<InformationResponse, object>(this.config.stepName, {
                processId,
                processStep: {
                    informationRequestId: request.informationRequestId,
                    items
                }
            })
            .subscribe({
                next: result => {
                    this.isSubmitting.set(false);

                    if (!getRouteForStep(result.requiredStep?.name) && this.config.nextRoute) {
                        void this.router.navigate(
                            buildProcessRoute(processId, this.config.nextRoute));
                    }
                },
                error: error => {
                    this.isSubmitting.set(false);
                    this.errorMessage.set(this.getErrorMessage(error));
                }
            });
    }

    private getErrorMessage(
        error: unknown
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

        return this.config.failureMessage;
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
