import { computed, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, take } from 'rxjs/operators';

import { FilterOperator, LogicalOperator } from '../../kaleido/models/enumerations';
import { QueryErrorResponse } from '../../kaleido/models/query-error-response';
import {
    QueryFilterNode,
    QueryRequest
} from '../../kaleido/models/queryable-request';
import {
    QueryableEnumValue,
    QueryableField,
    QueryableRecord,
    ServiceQueryableViewRegistration
} from '../../kaleido/models/queryable-registry';
import { QueryableRegistry } from '../../kaleido/services/queryable-registry';
import { QueryableRequestValidationError } from '../../kaleido/services/queryable-request-validator';
import { ProcessErrorResponse, ProcessService } from '../../kaleido/services/process-service';
import { QueryableService } from '../../kaleido/services/queryable-service';
import { ProcessStateService } from '../../process/services/process-state-service';
import { RegistryCatalog } from '../../registries/registry-catalog';
import { MemberDetailsParameters } from '../models/member-details-parameters';
import { CaptureMemberStep } from '../models/capture-member-step';
import { MemberDetailsResult } from '../models/member-details-result';
import { MemberSearchResult } from '../models/member-search-result';
import { StateOption } from '../models/state-option';

@Component({
    selector: 'priorauth-member-search',
    standalone: true,
    imports: [FormsModule],
    templateUrl: './member-search.html',
    styleUrl: './member-search.scss'
})
export class MemberSearch {
    constructor() {
        this.loadStateOptions();
    }

    private readonly queryableService =
        inject(QueryableService);

    private readonly processService =
        inject(ProcessService);

    private readonly queryableRegistry =
        inject(QueryableRegistry);

    private readonly processState =
        inject(ProcessStateService);

    private readonly registryCatalog =
        inject(RegistryCatalog);

    private readonly router =
        inject(Router);

    readonly searchViewName =
        'member-search';

    readonly detailsViewName =
        'member-details';

    readonly request: QueryRequest = {
        query: {
            searchText: '',
            page: {
                size: 27,
                offset: 0
            }
        }
    };

    readonly results =
        signal<MemberSearchResult[]>([]);
    readonly selectedRecord =
        signal<MemberSearchResult | undefined>(undefined);
    readonly selectedMemberDetails =
        signal<MemberDetailsResult | undefined>(undefined);
    dateOfBirth = '';
    issuanceState = '';
    lineOfBusiness = '';
    readonly stateOptions =
        signal<StateOption[]>([]);
    readonly stateOptionsError =
        signal<string | undefined>(undefined);
    readonly isLoading =
        signal(false);
    readonly isLoadingStates =
        signal(false);
    readonly isLoadingDetails =
        signal(false);
    readonly isNavigatingToRequestedService =
        signal(false);
    readonly errorMessage =
        signal<string | undefined>(undefined);
    readonly detailsError =
        signal<string | undefined>(undefined);
    readonly viewMode =
        signal<'results' | 'details'>('results');

    get registration(): ServiceQueryableViewRegistration | undefined {
        return this.queryableRegistry.tryGetViewRegistration(this.searchViewName);
    }

    get context(): QueryableRecord | undefined {
        return this.registration?.context;
    }

    readonly lineOfBusinessOptions =
        computed(() => {
            const field =
                this.getMemberField('LineOfBusiness');

            return field?.dataType.enumValues?.filter(
                option => option.name !== 'Unknown') ?? [];
        });

    search(): void {
        this.errorMessage.set(undefined);
        this.isLoading.set(true);
        this.selectedRecord.set(undefined);
        this.selectedMemberDetails.set(undefined);
        this.detailsError.set(undefined);
        this.viewMode.set('results');
        this.request.query ??= {};
        this.request.query.filter = this.buildFilter();
        this.request.query.page ??= {
            size: 27,
            offset: 0
        };
        this.request.query.page.offset = 0;

        this.queryableService
            .queryView<MemberSearchResult>(this.searchViewName, this.request)
            .subscribe({
                next: result => {
                    this.results.set(result.records);
                    this.isLoading.set(false);
                },
                error: error => {
                    this.results.set([]);
                    this.isLoading.set(false);
                    this.errorMessage.set(this.formatError(error));
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
        this.results.set([]);
        this.selectedRecord.set(undefined);
        this.selectedMemberDetails.set(undefined);
        this.errorMessage.set(undefined);
        this.detailsError.set(undefined);
        this.viewMode.set('results');
        this.processState.clearSelectedMember();
        this.processState.clearProcessMessages();
    }

    selectRecord(
        record: MemberSearchResult
    ): void {
        this.selectedRecord.set(record);
        this.selectedMemberDetails.set(undefined);
        this.detailsError.set(undefined);
        this.isLoadingDetails.set(true);
        this.viewMode.set('details');
        this.processState.clearProcessMessages();

        this.registryCatalog.loadState()
            .pipe(take(1))
            .subscribe({
                next: () => {
                    this.processState.setSelectedMember({
                        memberId: record.memberId,
                        memberEnrollmentId: record.memberEnrollmentId,
                        displayName: this.getRecordSummary(record),
                        memberNumber: record.memberNumber,
                        dateOfBirth: record.dateOfBirth,
                        lineOfBusiness: record.lineOfBusiness,
                        planName: record.planName,
                        effectiveDate: record.effectiveDate,
                        terminationDate: record.terminationDate
                    });

                    const detailsRequest: QueryRequest<MemberDetailsParameters> = {
                        parameters: {
                            MemberId: record.memberId,
                            MemberEnrollmentId: record.memberEnrollmentId
                        }
                    };

                    const captureRequest = {
                        processId: this.processState.state().processId,
                        processStep: {
                            memberId: record.memberId,
                            memberEnrollmentId: record.memberEnrollmentId,
                            dateOfService: this.processState.state().dateOfService
                        } satisfies CaptureMemberStep
                    };

                    forkJoin({
                        details: this.queryableService
                            .queryView<MemberDetailsResult, MemberDetailsParameters>(
                                this.detailsViewName,
                                detailsRequest),
                        capture: this.processService
                            .executeStep<CaptureMemberStep, object>('CaptureMember', captureRequest)
                            .pipe(
                                catchError(error => {
                                    if (ProcessErrorResponse.is(error)) {
                                        return of(null);
                                    }

                                    throw error;
                                }))
                    }).subscribe({
                        next: result => {
                            this.selectedMemberDetails.set(result.details.records[0]);
                            this.isLoadingDetails.set(false);
                        },
                        error: error => {
                            this.selectedMemberDetails.set(undefined);
                            this.isLoadingDetails.set(false);
                            this.detailsError.set(this.formatError(error));
                        }
                    });
                },
                error: error => {
                    this.isLoadingDetails.set(false);
                    this.detailsError.set(this.formatError(error));
                }
            });
    }

    backToResults(): void {
        this.viewMode.set('results');
    }

    goToRequestedService(): void {
        if (!this.selectedRecord() || this.isNavigatingToRequestedService()) {
            return;
        }

        this.isNavigatingToRequestedService.set(true);
        this.detailsError.set(undefined);

        void this.router.navigate(['/process', 'requested-service'])
            .finally(() => {
                this.isNavigatingToRequestedService.set(false);
            });
    }

    loadStateOptions(): void {
        this.isLoadingStates.set(true);
        this.stateOptionsError.set(undefined);

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
                    this.stateOptions.set(
                        result.records
                            .filter(record => record.isActive)
                            .sort((left, right) => left.name.localeCompare(right.name)));
                    this.isLoadingStates.set(false);
                },
                error: error => {
                    this.stateOptions.set([]);
                    this.isLoadingStates.set(false);
                    this.stateOptionsError.set(this.formatError(error));
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
            ['Effective', record.effectiveDate],
            ['Termination', record.terminationDate ?? 'Open-ended'],
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

    getDetailSections(): Array<{ title: string; entries: [string, unknown][] }> {
        if (!this.selectedMemberDetails()) {
            return [];
        }

        const details = this.selectedMemberDetails()!;
        const sections: Array<{ title: string; entries: [string, unknown][] }> = [
            {
                title: 'Identity',
                entries: [
                    ['Name', details.displayName],
                    ['Member Number', details.memberNumber],
                    ['Date of Birth', details.dateOfBirth],
                    ['Gender', details.gender]
                ]
            },
            {
                title: 'Enrollment',
                entries: [
                    ['Plan', details.planName],
                    ['Line of Business', details.lineOfBusiness],
                    ['Effective Date', details.effectiveDate],
                    ['Termination Date', details.terminationDate ?? 'Open-ended'],
                    ['Relationship', details.relationshipToSubscriber],
                    ['Issuance State', details.issuanceState]
                ]
            },
            {
                title: 'Contact',
                entries: [
                    ['Email', details.emailAddress],
                    ['Phone', details.phoneNumber]
                ]
            },
            {
                title: 'Address',
                entries: [
                    ['Address 1', details.addressLine1],
                    ['Address 2', details.addressLine2],
                    ['City', details.city],
                    ['State', details.addressState],
                    ['Postal Code', details.postalCode]
                ]
            }
        ];

        return sections.map(section => ({
            ...section,
            entries: section.entries.filter(([, value]) =>
                value !== undefined &&
                value !== null &&
                `${value}`.length > 0)
        }));
    }

    getCoverageClass(
        effectiveDate: string,
        terminationDate?: string
    ): string {
        const dateOfService = this.processState.state().dateOfService;

        if (!dateOfService) {
            return 'member-coverage--unknown';
        }

        if (dateOfService < effectiveDate) {
            return 'member-coverage--upcoming';
        }

        if (terminationDate && dateOfService > terminationDate) {
            return 'member-coverage--expired';
        }

        return 'member-coverage--active';
    }

    getCoverageMessage(
        effectiveDate: string,
        terminationDate?: string
    ): string {
        const dateOfService = this.processState.state().dateOfService;

        if (dateOfService < effectiveDate) {
            return `Coverage starts after the current date of service (${dateOfService}).`;
        }

        if (terminationDate && dateOfService > terminationDate) {
            return `Coverage ended before the current date of service (${dateOfService}).`;
        }

        return `Coverage includes the current date of service (${dateOfService}).`;
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

        return 'Unable to load member information.';
    }

    private isQueryErrorResponse(
        error: unknown
    ): error is QueryErrorResponse {
        return typeof error === 'object'
            && error !== null
            && 'errors' in error;
    }
}
