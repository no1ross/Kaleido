import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, EMPTY, Subject, switchMap, tap, catchError } from 'rxjs';

import { ProcedureCodeSystem } from '../shared/procedure-code-system';
import { ProcessService } from '../kaleido/services/process-service';
import { QueryableService } from '../kaleido/services/queryable-service';
import { ProcessStateService } from './services/process-state-service';
import { QueryRequest } from '../kaleido/models/queryable-request';
import { QueryErrorResponse } from '../kaleido/models/query-error-response';
import { CaptureRequestedServiceResponse } from '../kaleido/models/questionnaire';

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
    constructor() {
        this.searchTextChanges
            .pipe(
                debounceTime(200),
                distinctUntilChanged(),
                switchMap(searchText => this.searchProcedureCodes(searchText)))
            .subscribe();
    }

    private readonly queryableService =
        inject(QueryableService);

    private readonly processService =
        inject(ProcessService);

    private readonly processState =
        inject(ProcessStateService);

    private readonly searchTextChanges =
        new Subject<string>();

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

    onSearchTextChange(value: string): void {
        this.searchText = value;
        this.errorMessage.set(undefined);
        this.selectedRecord.set(undefined);
        this.searchTextChanges.next(value.trim());
    }

    selectRecord(record: ProcedureCodeSearchResult): void {
        this.selectedRecord.set(record);
        this.searchText = `${record.codeValue} ${record.shortDescription}`;
        this.results.set([]);
        this.submit(record);
    }

    submit(
        record: ProcedureCodeSearchResult = this.selectedRecord()!
    ): void {
        if (!record || this.isSubmitting()) {
            return;
        }

        this.isSubmitting.set(true);
        this.errorMessage.set(undefined);

        this.processService
            .executeStep<CaptureRequestedServiceStep, CaptureRequestedServiceResponse>('CaptureRequestedService', {
                processId: this.processState.state().processId,
                processStep: {
                    codeValue: record.codeValue,
                    codeSystem: record.codeSystem
                }
            })
            .subscribe({
                next: () => {
                    this.isSubmitting.set(false);
                },
                error: error => {
                    this.isSubmitting.set(false);
                    this.errorMessage.set(this.getErrorMessage(error, 'Unable to capture the requested service.'));
                }
            });
    }

    private searchProcedureCodes(searchText: string) {
        if (searchText.length < 2) {
            this.results.set([]);
            this.isLoading.set(false);
            return EMPTY;
        }

        this.isLoading.set(true);

        const request: QueryRequest = {
            query: {
                searchText,
                page: {
                    size: 8,
                    offset: 0
                }
            }
        };

        return this.queryableService
            .queryContext<ProcedureCodeSearchResult>('procedure-codes', request)
            .pipe(
                tap(result => {
                    this.results.set(result.results);
                    this.isLoading.set(false);
                }),
                catchError(error => {
                    this.results.set([]);
                    this.isLoading.set(false);
                    this.errorMessage.set(this.getErrorMessage(error, 'Unable to search procedure codes.'));
                    return EMPTY;
                }));
    }

    private getErrorMessage(
        error: unknown,
        fallback: string
    ): string {
        if (this.isQueryErrorResponse(error)) {
            return error.errors
                .map(message => message.message)
                .join(' ');
        }

        return fallback;
    }

    private isQueryErrorResponse(
        error: unknown
    ): error is QueryErrorResponse {
        return typeof error === 'object'
            && error !== null
            && 'errors' in error
            && Array.isArray((error as QueryErrorResponse).errors);
    }
}
