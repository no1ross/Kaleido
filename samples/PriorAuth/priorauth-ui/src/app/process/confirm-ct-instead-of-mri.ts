import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';

import { ProcessStateService } from './services/process-state-service';
import { buildProcessRoute } from './services/process-navigation';

@Component({
    selector: 'priorauth-confirm-ct-instead-of-mri',
    standalone: true,
    templateUrl: './confirm-ct-instead-of-mri.html',
    styleUrl: './confirm-ct-instead-of-mri.scss'
})
export class ConfirmCtInsteadOfMri {
    private readonly router =
        inject(Router);

    private readonly processState =
        inject(ProcessStateService);

    continueWithCt(): void {
        void this.router.navigate(
            buildProcessRoute(
                this.processState.state().processId,
                'requested-services'));
    }

    switchToMri(): void {
        void this.router.navigate(
            buildProcessRoute(
                this.processState.state().processId,
                'capture-mri-info'));
    }
}
