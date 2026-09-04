import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { ProcessService } from '../kaleido/services/process-service';
import { ProcessStateService } from '../process/services/process-state-service';

@Component({
    selector: 'priorauth-home',
    standalone: true,
    imports: [],
    templateUrl: './home.html',
    styleUrl: './home.scss'
})
export class PriorAuthHome {
    private readonly processService =
        inject(ProcessService);

    private readonly processState =
        inject(ProcessStateService);

    private readonly router =
        inject(Router);

    readonly processId =
        computed(() => this.processState.state().processId);

    readonly isStarting =
        signal(false);

    readonly startError =
        signal<string | undefined>(undefined);

    startIntake(): void {
        if (this.isStarting()) {
            return;
        }

        this.isStarting.set(true);
        this.startError.set(undefined);

        this.processService
            .executeStep<object, object>('StartIntake', {
                processId: undefined,
                processStep: {}
            })
            .subscribe({
                next: () => {
                    this.isStarting.set(false);
                },
                error: () => {
                    this.isStarting.set(false);
                    this.startError.set('Failed to start intake session. Please try again.');
                }
            });
    }

    goToMemberSearch(): void {
        const id = this.processId();
        void this.router.navigate(
            id
                ? ['/process', id, 'member-search']
                : ['/process', 'new', 'member-search']);
    }

    goToRequestedService(): void {
        const id = this.processId();
        void this.router.navigate(
            id
                ? ['/process', id, 'requested-service']
                : ['/process', 'new', 'requested-service']);
    }
}
