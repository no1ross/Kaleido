import { JsonPipe } from '@angular/common';
import { Component, inject, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { FilterOperator, LogicalOperator } from '../kaleido/models/enumerations';
import { QueryErrorResponse } from '../kaleido/models/query-error-response';
import {
    QueryFilterNode,
    QueryRequest
} from '../kaleido/models/queryable-request';
import {
    QueryableEnumValue,
    QueryableField,
    QueryableRecord,
    ServiceQueryableViewRegistration
} from '../kaleido/models/queryable-registry';
import { QueryableRegistry } from '../kaleido/services/queryable-registry';
import { QueryableRequestValidationError } from '../kaleido/services/queryable-request-validator';
import { QueryableService } from '../kaleido/services/queryable-service';
import { MemberSearchResult } from './models/member-search-result';
import { StateOption } from './models/state-option';

@Component({
    selector: 'priorauth-member-search',
    standalone: true,
    imports: [FormsModule, JsonPipe],
    templateUrl: './member-search.html',
    styleUrl: './member-search.scss'
})
export class MemberSearch {
    constructor() {
        this.loadStateOptions();
    }

    private readonly queryableService =
        inject(QueryableService);
    
    private readonly changeDetector =
        inject(ChangeDetectorRef);

    private readonly queryableRegistry =
        inject(QueryableRegistry);

    readonly viewName =
        'member-search';

    readonly request: QueryRequest = {
        query: {
            searchText: '',
            page: {
                size: 27,
                offset: 0
            }
        }
    };

    results: MemberSearchResult[] = [];
    selectedRecord?: MemberSearchResult;
    dateOfBirth = '';
    issuanceState = '';
    lineOfBusiness = '';
    stateOptions: StateOption[] = [];
    stateOptionsError?: string;
    isLoading = false;
    isLoadingStates = false;
    errorMessage?: string;

    get registration(): ServiceQueryableViewRegistration | undefined {
        return this.queryableRegistry.tryGetViewRegistration(this.viewName);
    }

    get context(): QueryableRecord | undefined {
        return this.registration?.context;
    }

    get lineOfBusinessOptions(): readonly QueryableEnumValue[] {
        const field = this.getMemberField('LineOfBusiness');

        return field?.dataType.enumValues?.filter(option => option.name !== 'Unknown') ?? [];
    }

    search(): void {
        this.errorMessage = undefined;
        this.isLoading = true;
        this.selectedRecord = undefined;
        this.request.query ??= {};
        this.request.query.filter = this.buildFilter();
        this.request.query.page ??= {
            size: 27,
            offset: 0
        };
        this.request.query.page.offset = 0;

        this.queryableService
            .query<MemberSearchResult>(this.viewName, this.request)
            .subscribe({
                next: result => {
                    this.results = result.records;
                    this.isLoading = false;

                    this.changeDetector.detectChanges();
                },
                error: error => {
                    this.results = [];
                    this.isLoading = false;
                    this.errorMessage = this.formatError(error);
                }
            });
    }

    clear(): void {
        this.request.query ??= {};
        this.request.query.searchText = '';
        this.request.query.filter = undefined;
        this.request.query.page ??= {
            size: 27,
            offset: 0
        };
        this.request.query.page.offset = 0;
        this.dateOfBirth = '';
        this.issuanceState = '';
        this.lineOfBusiness = '';
        this.results = [];
        this.selectedRecord = undefined;
        this.errorMessage = undefined;
    }

    selectRecord(
        record: MemberSearchResult
    ): void {
        this.selectedRecord = record;
    }

    loadStateOptions(): void {
        this.isLoadingStates = true;
        this.stateOptionsError = undefined;

        this.queryableService
            .queryContext<StateOption>('states', {
                query: {
                    page: {
                        size: 100,
                        offset: 0
                    }
                }
            })
            .subscribe({
                next: result => {
                    this.stateOptions = result.records
                        .filter(record => record.isActive)
                        .sort((left, right) => left.name.localeCompare(right.name));
                    this.isLoadingStates = false;
                    this.changeDetector.detectChanges();
                },
                error: error => {
                    this.stateOptions = [];
                    this.isLoadingStates = false;
                    this.stateOptionsError = this.formatError(error);
                }
            });
    }

    trackResult(
        index: number,
        record: MemberSearchResult
    ): string | number {
        if (record.memberId && record.memberEnrollmentId) {
            return `${record.memberId}:${record.memberEnrollmentId}`;
        }

        if (record.memberEnrollmentId) {
            return record.memberEnrollmentId;
        }

        if (record.memberId) {
            return record.memberId;
        }

        return index;
    }

    getRecordEntries(
        record: MemberSearchResult
    ): [string, unknown][] {
        const entries: [string, unknown][] = [
            ['Member Number', record.memberNumber],
            ['Date of Birth', record.dateOfBirth],
            ['State', record.issuanceState],
            ['Line of Business', record.lineOfBusiness],
            ['Plan', record.planName]
        ];

        return entries.filter(([, value]) =>
            value !== undefined &&
            value !== null &&
            `${value}`.length > 0);
    }

    getRecordSummary(
        record: MemberSearchResult
    ): string {
        if (record.displayName?.trim().length > 0) {
            return record.displayName;
        }

        const fullName =
            `${record.firstName ?? ''} ${record.lastName ?? ''}`
                .trim();

        if (fullName.length > 0) {
            return fullName;
        }

        return 'Member result';
    }

    getCompactEntries(
        record: MemberSearchResult
    ): [string, unknown][] {
        return this.getRecordEntries(record)
            .slice(0, 4);
    }

    private buildFilter(): QueryFilterNode | undefined {
        const filters: QueryFilterNode[] = [];

        if (this.dateOfBirth) {
            filters.push({
                condition: {
                    field: 'DateOfBirth',
                    operator: FilterOperator.Equals,
                    values: [this.dateOfBirth]
                }
            });
        }

        if (this.issuanceState) {
            filters.push({
                condition: {
                    field: 'IssuanceState',
                    operator: FilterOperator.Equals,
                    values: [this.issuanceState]
                }
            });
        }

        if (this.lineOfBusiness) {
            filters.push({
                condition: {
                    field: 'LineOfBusiness',
                    operator: FilterOperator.Equals,
                    values: [this.lineOfBusiness]
                }
            });
        }

        if (filters.length === 0) {
            return undefined;
        }

        if (filters.length === 1) {
            return filters[0];
        }

        return {
            group: {
                operator: LogicalOperator.And,
                filters
            }
        };
    }

    private getMemberField(
        fieldName: string
    ): QueryableField | undefined {
        return this.context?.fields.find(
            field => field.name === fieldName);
    }

    private formatError(
        error: unknown
    ): string {
        if (error instanceof QueryableRequestValidationError) {
            return error.messages
                .map(message => message.message)
                .join('; ');
        }

        if (this.isQueryErrorResponse(error)) {
            return error.errors
                .map(message => message.message)
                .join('; ');
        }

        if (typeof error === 'object' && error !== null && 'message' in error) {
            const message = error.message;

            if (typeof message === 'string' && message.length > 0) {
                return message;
            }
        }

        return 'Unable to search members.';
    }

    private isQueryErrorResponse(
        error: unknown
    ): error is QueryErrorResponse {
        return typeof error === 'object'
            && error !== null
            && 'errors' in error;
    }
}
