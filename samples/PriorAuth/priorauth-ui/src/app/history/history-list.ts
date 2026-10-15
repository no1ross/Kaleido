import { Component, inject, OnInit, signal } from '@angular/core';
import { LowerCasePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';

import { SortDirection } from '../kaleido/models/enumerations';
import { ProcessService } from '../kaleido/services/process-service';
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

    private readonly processService =
        inject(ProcessService);

    readonly records =
        signal<PriorAuthRecordView[]>([]);

    readonly isLoading =
        signal(true);

    readonly errorMessage =
        signal<string | undefined>(undefined);

    readonly resumingProcessId =
        signal<string | undefined>(undefined);

    readonly resumeError =
        signal<string | undefined>(undefined);

    ngOnInit(): void {
        this.loadRecords();
    }

    loadRecords(): void {
        this.isLoading.set(true);
        this.errorMessage.set(undefined);

        this.queryableService
            .queryView<PriorAuthRecordView>('PriorAuthRecordListViewSource', {
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

    /**
     * Continues the process wherever it is, whoever started it (this UI or an agent through the
     * MCP adapter): History supplies the member and date of service it knows; the process's own
     * state supplies the processor, the next step and any pending questions.
     */
    resumeProcess(record: PriorAuthRecordView): void {
        if (this.resumingProcessId()) {
            return;
        }

        this.resumingProcessId.set(record.processId);
        this.resumeError.set(undefined);

        this.processState.resume(
            record.processId,
            record.memberNumber || record.memberDisplayName
                ? {
                    memberId: '',
                    memberEnrollmentId: '',
                    displayName: record.memberDisplayName,
                    memberNumber: record.memberNumber,
                    dateOfBirth: '',
                    lineOfBusiness: '',
                    planName: '',
                    effectiveDate: ''
                }
                : undefined,
            record.dateOfService);

        this.processService
            .resumeProcess(record.processorName, record.processId)
            .subscribe({
                next: state => {
                    this.resumingProcessId.set(undefined);

                    if (!state.requiredStep && state.availableSteps.length === 0) {
                        this.processState.reset();
                        this.resumeError.set(`This request has no steps left to take (${this.formatStatus(record.status)}).`);
                    }
                },
                error: error => {
                    this.resumingProcessId.set(undefined);
                    this.processState.reset();
                    this.resumeError.set(
                        error instanceof HttpErrorResponse && (error.status === 401 || error.status === 403)
                            ? 'You are not allowed to continue this request. Sign in as a user with access to its processor.'
                            : 'Unable to load this request to continue it.');
                }
            });
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
