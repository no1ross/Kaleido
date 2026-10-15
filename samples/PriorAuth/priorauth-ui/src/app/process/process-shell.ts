import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { ProcessMessages } from './process-messages';
import { ProcessStateService } from './services/process-state-service';
import { buildProcessRoute } from './services/process-navigation';
import { getRouteForStep } from './services/step-route';

// "CaptureClinicalInfoStep" → "Capture Clinical Info"
function humanize(stepName: string): string {
    return stepName
        .replace(/Step$/, '')
        .replace(/([a-z])([A-Z])/g, '$1 $2');
}

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

        this.currentChildRoute.set(this.getChildRoute());
        this.router.events
            .pipe(filter(e => e instanceof NavigationEnd))
            .subscribe(() => this.currentChildRoute.set(this.getChildRoute()));
    }

    readonly processState =
        inject(ProcessStateService);

    private readonly router =
        inject(Router);

    private readonly activatedRoute =
        inject(ActivatedRoute);

    private readonly currentChildRoute =
        signal<string>('');

    /** Hide the summary card on the requested-service route when no member is selected yet */
    readonly showSummaryCard =
        computed(() =>
            this.processState.state().selectedMember !== undefined ||
            this.currentChildRoute() !== 'requested-service');

    /**
     * What the process allows next, as the server reported it: the required step (only that one
     * can run) or else the available steps, limited to those this UI has a screen for.
     */
    readonly nextSteps =
        computed(() => {
            const state = this.processState.state();
            const required = state.availableSteps.find(step => step.name === state.requiredStep);
            const steps = state.requiredStep
                ? [required ?? { name: state.requiredStep, displayName: undefined }]
                : state.availableSteps;

            return steps
                .map(step => ({ label: step.displayName ?? humanize(step.name), route: getRouteForStep(step.name) }))
                .filter((step): step is { label: string; route: string } => step.route !== undefined)
                .filter((step, index, all) => all.findIndex(other => other.route === step.route) === index);
        });

    readonly isStepRequired =
        computed(() => this.processState.state().requiredStep !== undefined);

    isCurrentRoute(
        route: string
    ): boolean {
        return this.currentChildRoute() === route;
    }

    goToStep(
        route: string
    ): void {
        void this.router.navigate(
            buildProcessRoute(this.processState.state().processId, route));
    }

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

    private getChildRoute(): string {
        const segments = this.router.url.split('/');
        return segments[segments.length - 1]?.split('?')[0] ?? '';
    }
}
