import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ProcedureCodeSystem } from '../shared/procedure-code-system';
import { ProcessService } from '../kaleido/services/process-service';
import { QueryableService } from '../kaleido/services/queryable-service';
import { ProcessStateService } from './services/process-state-service';
import { QueryRequest } from '../kaleido/models/queryable-request';

interface ProcedureCodeSearchResult {
    procedureCodeId: string;
    codeValue: string;
    codeSystem: ProcedureCodeSystem;
    shortDescription: string;
    longDescription?: string;
}

interface CaptureRequestedServiceStep {
    codeValue: string;
    codeSystem: ProcedureCodeSystem;
}

@Component({
    selector: 'priorauth-requested-service',
    standalone: true,
    imports: [FormsModule],
    templateUrl: './requested-service.html',
    styleUrl: './requested-service.scss'
})
export class RequestedService {
    private readonly queryableService =
        inject(QueryableService);

    private readonly processService =
        inject(ProcessService);

    private readonly processState =
        inject(ProcessStateService);

    private readonly changeDetector =
        inject(ChangeDetectorRef);

    searchText = '';
    results: ProcedureCodeSearchResult[] = [];
    selectedRecord?: ProcedureCodeSearchResult;
    isLoading = false;
    isSubmitting = false;
    errorMessage?: string;

    search(): void {
        this.errorMessage = undefined;
        this.isLoading = true;

        const request: QueryRequest = {
            query: {
                searchText: this.searchText,
                page: {
                    size: 10,
                    offset: 0
                }
            }
        };

        this.queryableService
            .queryContext<ProcedureCodeSearchResult>('procedure-codes', request)
            .subscribe({
                next: result => {
                    this.results = result.records;
                    this.isLoading = false;
                    this.changeDetector.detectChanges();
                },
                error: () => {
                    this.results = [];
                    this.isLoading = false;
                    this.errorMessage = 'Unable to search procedure codes.';
                }
            });
    }

    selectRecord(record: ProcedureCodeSearchResult): void {
        this.selectedRecord = record;
    }

    submit(): void {
        if (!this.selectedRecord || !this.processState.state.processId || this.isSubmitting) {
            return;
        }

        this.isSubmitting = true;
        this.errorMessage = undefined;

        this.processService
            .executeStep<CaptureRequestedServiceStep, object>('CaptureRequestedService', {
                participantProcessId: this.processState.state.processId,
                processStep: {
                    codeValue: this.selectedRecord.codeValue,
                    codeSystem: this.selectedRecord.codeSystem
                }
            })
            .subscribe({
                next: () => {
                    this.isSubmitting = false;
                },
                error: () => {
                    this.isSubmitting = false;
                    this.errorMessage = 'Unable to capture the requested service.';
                }
            });
    }
}
