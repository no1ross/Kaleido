import { Component, inject, OnInit, signal } from '@angular/core';
import { LowerCasePipe } from '@angular/common';
import { Router } from '@angular/router';

import { SortDirection } from '../kaleido/models/enumerations';
import { QueryableService } from '../kaleido/services/queryable-service';
import { ProcessStateService } from '../process/services/process-state-service';
import { PriorAuthRecordView } from './prior-auth-record-view';

@Component({
    selector: 'priorauth-history-list',
    standalone: true,
    imports: [LowerCasePipe],
    templateUrl: './history-list.html',
    styleUrl: './history-list.scss'
})
export class HistoryList implements OnInit {
    private readonly queryableService =
        inject(QueryableService);

    private readonly processState =
        inject(ProcessStateService);

    private readonly router =
        inject(Router);

    readonly records =
        signal<PriorAuthRecordView[]>([]);

    readonly isLoading =
        signal(true);

    readonly errorMessage =
        signal<string | undefined>(undefined);

    ngOnInit(): void {
        this.loadRecords();
    }

    loadRecords(): void {
        this.isLoading.set(true);
        this.errorMessage.set(undefined);

        this.queryableService
            .queryView<PriorAuthRecordView>('prior-auth-records', {
                query: {
                    sort: [
                        {
                            field: 'LastUpdatedUtc',
                            direction: SortDirection.Descending,
                            sequence: 0
                        }
                    ],
                    page: {
                        size: 50,
                        offset: 0
                    }
                }
            })
            .subscribe({
                next: result => {
                    this.records.set(result.results);
                    this.isLoading.set(false);
                },
                error: () => {
                    this.isLoading.set(false);
                    this.errorMessage.set('Failed to load prior authorization history. Please try again.');
                }
            });
    }

    resumeProcess(record: PriorAuthRecordView): void {
        this.processState.setProcessId(record.processId);
        void this.router.navigate(['/process', record.processId, 'member-search']);
    }

    goHome(): void {
        void this.router.navigate(['/']);
    }

    formatDate(value: string): string {
        if (!value) {
            return '—';
        }

        try {
            return new Date(value).toLocaleDateString();
        } catch {
            return value;
        }
    }

    formatStatus(status: string): string {
        if (!status) {
            return '—';
        }

        // Convert PascalCase enum values like "InProgress" → "In Progress"
        return status.replace(/([A-Z])/g, ' $1').trim();
    }
}
