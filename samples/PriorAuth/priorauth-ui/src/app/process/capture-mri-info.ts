import { computed, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { InformationResponse, InformationResponseItem } from '../kaleido/models/information-request';
import { QueryErrorResponse } from '../kaleido/models/query-error-response';
import { ProcessService, ProcessErrorResponse } from '../kaleido/services/process-service';
import { InformationRequestForm } from './information-request-form';
import { buildProcessRoute } from './services/process-navigation';
import { ProcessStateService } from './services/process-state-service';

const StepName = 'CaptureMriInfoStep';

@Component({
    selector: 'priorauth-capture-mri-info',
    standalone: true,
    imports: [InformationRequestForm],
    templateUrl: './capture-mri-info.html',
    styleUrl: './capture-mri-info.scss'
})
export class CaptureMriInfo {
    private readonly processService =
        inject(ProcessService);

    private readonly processState =
        inject(ProcessStateService);

    private readonly router =
        inject(Router);

    readonly isSubmitting =
        signal(false);
    readonly errorMessage =
        signal<string | undefined>(undefined);

    /** The pending questions, presented with the required step (requiredStep.informationRequest). */
    readonly informationRequest =
        computed(() =>
            this.processState.state().informationStepName === StepName
                ? this.processState.state().informationRequest
                : undefined);

    submit(items: InformationResponseItem[]): void {
        const request = this.informationRequest();

        if (!this.processState.state().processId || !request || this.isSubmitting()) {
            return;
        }

        this.isSubmitting.set(true);
        this.errorMessage.set(undefined);

        this.processService
            .executeStep<InformationResponse, object>(StepName, {
                processId: this.processState.state().processId,
                processStep: {
                    informationRequestId: request.informationRequestId,
                    items
                }
            })
            .subscribe({
                next: () => {
                    this.isSubmitting.set(false);
                    void this.router.navigate(
                        buildProcessRoute(
                            this.processState.state().processId,
                            'requested-services'));
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

        return 'Unable to capture MRI information.';
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
