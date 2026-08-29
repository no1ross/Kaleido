import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterOutlet } from '@angular/router';

import { ProcessMessages } from './process-messages';
import { ProcessStateService } from './services/process-state-service';
import { getRouteForStep } from './services/step-route';

@Component({
    selector: 'priorauth-process-shell',
    standalone: true,
    imports: [FormsModule, RouterOutlet, ProcessMessages],
    templateUrl: './process-shell.html',
    styleUrl: './process-shell.scss'
})
export class ProcessShell {
    constructor() {
        const routeProcessId = this.activatedRoute.snapshot.paramMap.get('processId');
        const stateProcessId = this.processState.state().processId;

        if (routeProcessId === 'new') {
            return;
        }

        if (!stateProcessId || routeProcessId !== stateProcessId) {
            this.processState.reset();
            void this.router.navigate(['/']);
        }
    }

    readonly processState =
        inject(ProcessStateService);

    private readonly router =
        inject(Router);

    private readonly activatedRoute =
        inject(ActivatedRoute);

    getStepRoute(
        stepName: string
    ): string | undefined {
        return getRouteForStep(stepName);
    }

    updateDateOfService(
        value: string
    ): void {
        this.processState.setDateOfService(value);
    }

    exitProcess(): void {
        this.processState.reset();
        void this.router.navigate(['/']);
    }
}
