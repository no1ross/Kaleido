import { computed, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { InformationResponse, InformationResponseItem } from '../kaleido/models/information-request';
import { ProcessService, ProcessErrorResponse } from '../kaleido/services/process-service';
import { InformationRequestForm } from './information-request-form';
import { buildProcessRoute } from './services/process-navigation';
import { ProcessStateService } from './services/process-state-service';

const StepName = 'ConfirmCtInsteadOfMriStep';

@Component({
    selector: 'priorauth-confirm-ct-instead-of-mri',
    standalone: true,
    imports: [InformationRequestForm],
    templateUrl: './confirm-ct-instead-of-mri.html',
    styleUrl: './confirm-ct-instead-of-mri.scss'
})
export class ConfirmCtInsteadOfMri {
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
                    this.errorMessage.set(
                        ProcessErrorResponse.is(error) && error.messages.length > 0
                            ? error.messages.map(message => message.message).join(' ')
                            : 'Unable to confirm the CT request.');
                }
            });
    }
}
