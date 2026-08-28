import { Component, inject, signal } from '@angular/core';
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

    searchText = '';
    readonly results =
        signal<ProcedureCodeSearchResult[]>([]);
    readonly selectedRecord =
        signal<ProcedureCodeSearchResult | undefined>(undefined);
    readonly isLoading =
        signal(false);
    readonly isSubmitting =
        signal(false);
    readonly errorMessage =
        signal<string | undefined>(undefined);

    search(): void {
        this.errorMessage.set(undefined);
        this.isLoading.set(true);

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
                    this.results.set(result.records);
                    this.isLoading.set(false);
                },
                error: () => {
                    this.results.set([]);
                    this.isLoading.set(false);
                    this.errorMessage.set('Unable to search procedure codes.');
                }
            });
    }

    selectRecord(record: ProcedureCodeSearchResult): void {
        this.selectedRecord.set(record);
    }

    submit(): void {
        if (!this.selectedRecord() || !this.processState.state().processId || this.isSubmitting()) {
            return;
        }

        this.isSubmitting.set(true);
        this.errorMessage.set(undefined);

        this.processService
            .executeStep<CaptureRequestedServiceStep, object>('CaptureRequestedService', {
                processId: this.processState.state().processId,
                processStep: {
                    codeValue: this.selectedRecord()!.codeValue,
                    codeSystem: this.selectedRecord()!.codeSystem
                }
            })
            .subscribe({
                next: () => {
                    this.isSubmitting.set(false);
                },
                error: () => {
                    this.isSubmitting.set(false);
                    this.errorMessage.set('Unable to capture the requested service.');
                }
            });
    }
}
