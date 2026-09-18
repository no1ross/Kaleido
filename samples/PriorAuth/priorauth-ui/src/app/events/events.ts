import { Component, inject, signal } from '@angular/core';
import { of } from 'rxjs';
import { catchError, take } from 'rxjs/operators';

import { EventCollectorService } from './services/event-collector-service';
import { ProcessSummary } from './models/event-summary';
import { EventDetail } from './models/event-detail';

@Component({
    selector: 'priorauth-events',
    standalone: true,
    templateUrl: './events.html',
    styleUrl: './events.scss'
})
export class Events {
    private readonly eventCollectorService =
        inject(EventCollectorService);

    readonly processes =
        signal<ProcessSummary[]>([]);
    readonly selectedProcessId =
        signal<string | undefined>(undefined);
    readonly events =
        signal<EventDetail[]>([]);
    readonly isLoadingProcesses =
        signal(false);
    readonly isLoadingEvents =
        signal(false);
    readonly processesError =
        signal<string | undefined>(undefined);
    readonly eventsError =
        signal<string | undefined>(undefined);
    readonly expandedEventIds =
        signal<Set<number>>(new Set());

    constructor() {
        this.loadProcesses();
    }

    loadProcesses(): void {
        this.processesError.set(undefined);
        this.isLoadingProcesses.set(true);

        this.eventCollectorService.getProcesses()
            .pipe(take(1))
            .subscribe({
                next: processes => {
                    this.processes.set(processes);
                    this.isLoadingProcesses.set(false);
                },
                error: error => {
                    this.processes.set([]);
                    this.isLoadingProcesses.set(false);
                    this.processesError.set(this.formatError(error));
                }
            });
    }

    selectProcess(processId: string): void {
        this.selectedProcessId.set(processId);
        this.eventsError.set(undefined);
        this.isLoadingEvents.set(true);
        this.events.set([]);
        this.expandedEventIds.set(new Set());

        this.eventCollectorService.getProcessEvents(processId)
            .pipe(take(1))
            .subscribe({
                next: events => {
                    this.events.set(events);
                    this.isLoadingEvents.set(false);
                },
                error: error => {
                    this.events.set([]);
                    this.isLoadingEvents.set(false);
                    this.eventsError.set(this.formatError(error));
                }
            });
    }

    toggleEventExpansion(eventId: number): void {
        const current = this.expandedEventIds();
        const expanded = new Set(current);

        if (expanded.has(eventId)) {
            expanded.delete(eventId);
        } else {
            expanded.add(eventId);
        }

        this.expandedEventIds.set(expanded);
    }

    formatTimestamp(isoString: string): string {
        const date = new Date(isoString);
        return date.toLocaleString();
    }

    private formatError(error: unknown): string {
        if (typeof error === 'object' && error !== null && 'message' in error) {
            const message = error.message;
            if (typeof message === 'string' && message.length > 0) {
                return message;
            }
        }
        return 'Unable to load events.';
    }
}
