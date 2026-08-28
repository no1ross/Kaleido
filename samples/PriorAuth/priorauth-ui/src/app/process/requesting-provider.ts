import { computed, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { FilterOperator, LogicalOperator } from '../kaleido/models/enumerations';
import { QueryErrorResponse } from '../kaleido/models/query-error-response';
import { QueryFilterNode, QueryRequest } from '../kaleido/models/queryable-request';
import { QueryableField, QueryableRecord, ServiceQueryableViewRegistration } from '../kaleido/models/queryable-registry';
import { QueryableRegistry } from '../kaleido/services/queryable-registry';
import { QueryableRequestValidationError } from '../kaleido/services/queryable-request-validator';
import { ProcessErrorResponse, ProcessService } from '../kaleido/services/process-service';
import { QueryableService } from '../kaleido/services/queryable-service';
import { StateOption } from '../member/models/state-option';
import { RegistryCatalog } from '../registries/registry-catalog';
import { CaptureRequestingProviderStep } from './models/capture-requesting-provider-step';
import { ProviderSearchResult } from './models/provider-search-result';
import { ProviderSpecialtyOption } from './models/provider-specialty-option';
import { ProcessStateService } from './services/process-state-service';

@Component({
    selector: 'priorauth-requesting-provider',
    standalone: true,
    imports: [FormsModule],
    templateUrl: './requesting-provider.html',
    styleUrl: './requesting-provider.scss'
})
export class RequestingProvider {
    constructor() {
        this.loadStateOptions();
        this.loadSpecialtyOptions();
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
        'provider-search';

    readonly request: QueryRequest = {
        query: {
            searchText: '',
            page: {
                size: 25,
                offset: 0
            }
        }
    };

    readonly results =
        signal<ProviderSearchResult[]>([]);
    readonly selectedRecord =
        signal<ProviderSearchResult | undefined>(undefined);
    stateCode = '';
    specialtyId = '';
    readonly stateOptions =
        signal<StateOption[]>([]);
    readonly specialtyOptions =
        signal<ProviderSpecialtyOption[]>([]);
    readonly stateOptionsError =
        signal<string | undefined>(undefined);
    readonly specialtyOptionsError =
        signal<string | undefined>(undefined);
    readonly isLoading =
        signal(false);
    readonly isLoadingStates =
        signal(false);
    readonly isLoadingSpecialties =
        signal(false);
    readonly isSubmitting =
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

    readonly selectedRecordSummary =
        computed(() => this.selectedRecord()?.providerName ?? 'Provider result');

    search(): void {
        this.errorMessage.set(undefined);
        this.detailsError.set(undefined);
        this.isLoading.set(true);
        this.selectedRecord.set(undefined);
        this.viewMode.set('results');
        this.request.query ??= {};
        this.request.query.filter = this.buildFilter();
        this.request.query.page ??= {
            size: 25,
            offset: 0
        };
        this.request.query.page.offset = 0;

        this.queryableService
            .queryView<ProviderSearchResult>(this.searchViewName, this.request)
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
            size: 25,
            offset: 0
        };
        this.request.query.page.offset = 0;
        this.stateCode = '';
        this.specialtyId = '';
        this.results.set([]);
        this.selectedRecord.set(undefined);
        this.errorMessage.set(undefined);
        this.detailsError.set(undefined);
        this.viewMode.set('results');
        this.processState.clearProcessMessages();
    }

    selectRecord(
        record: ProviderSearchResult
    ): void {
        this.selectedRecord.set(record);
        this.detailsError.set(undefined);
        this.viewMode.set('details');
        this.processState.clearProcessMessages();
    }

    backToResults(): void {
        this.viewMode.set('results');
    }

    confirmSelection(): void {
        const selectedRecord = this.selectedRecord();
        if (!selectedRecord || this.isSubmitting()) {
            return;
        }

        this.isSubmitting.set(true);
        this.detailsError.set(undefined);

        this.registryCatalog.loadState().subscribe({
            next: () => {
                this.processService
                    .executeStep<CaptureRequestingProviderStep, object>('CaptureRequestingProvider', {
                        processId: this.processState.state().processId,
                        processStep: {
                            providerId: selectedRecord.providerId,
                            providerLocationId: selectedRecord.providerLocationId,
                            providerName: selectedRecord.providerName,
                            locationName: selectedRecord.locationName
                        }
                    })
                    .subscribe({
                        next: result => {
                            this.isSubmitting.set(false);

                            if (result.requiredStep !== 'CaptureServicingProvider') {
                                void this.router.navigate(['/process', 'servicing-provider']);
                            }
                        },
                        error: error => {
                            this.isSubmitting.set(false);
                            this.detailsError.set(this.getProcessErrorMessage(error) ?? 'Unable to capture requesting provider.');
                        }
                    });
            },
            error: error => {
                this.isSubmitting.set(false);
                this.detailsError.set(this.formatError(error));
            }
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

    loadSpecialtyOptions(): void {
        const field =
            this.getProviderField('PrimaryMedicalSpecialtyId');

        const enumValues =
            field?.dataType.enumValues ?? [];

        this.specialtyOptions.set(
            enumValues.map(option => ({
                medicalSpecialtyId: `${option.value}`,
                specialtyCode: option.description ?? option.name,
                name: option.name
            })));
    }

    trackResult(
        index: number,
        record: ProviderSearchResult
    ): string | number {
        if (record.providerLocationId) {
            return record.providerLocationId;
        }

        if (record.providerId) {
            return record.providerId;
        }

        return index;
    }

    getRecordEntries(
        record: ProviderSearchResult
    ): [string, unknown][] {
        const entries: [string, unknown][] = [
            ['Location', record.locationName],
            ['City', record.city],
            ['State', record.stateCode],
            ['ZIP', record.postalCode],
            ['Specialty', record.primaryMedicalSpecialtyName],
            ['NPI', record.primaryNpi],
            ['TIN', record.primaryTin],
            ['Phone', record.phoneNumber]
        ];

        return entries.filter(([, value]) =>
            value !== undefined &&
            value !== null &&
            `${value}`.length > 0);
    }

    getCompactEntries(
        record: ProviderSearchResult
    ): [string, unknown][] {
        return this.getRecordEntries(record)
            .slice(0, 4);
    }

    private buildFilter(): QueryFilterNode | undefined {
        const filters: QueryFilterNode[] = [];

        if (this.stateCode) {
            filters.push({
                condition: {
                    field: 'StateCode',
                    operator: FilterOperator.Equals,
                    values: [this.stateCode]
                }
            });
        }

        if (this.specialtyId) {
            filters.push({
                condition: {
                    field: 'PrimaryMedicalSpecialtyId',
                    operator: FilterOperator.Equals,
                    values: [this.specialtyId]
                }
            });
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

    private getProviderField(
        fieldName: string
    ): QueryableField | undefined {
        return this.context?.fields.find(
            field => field.name === fieldName);
    }

    private getProcessErrorMessage(
        error: unknown
    ): string | undefined {
        if (ProcessErrorResponse.is(error) && error.messages.length > 0) {
            return error.messages
                .map(message => message.message)
                .join(' ');
        }

        return undefined;
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

        return 'Unable to load provider information.';
    }

    private isQueryErrorResponse(
        error: unknown
    ): error is QueryErrorResponse {
        return typeof error === 'object'
            && error !== null
            && 'errors' in error;
    }
}
