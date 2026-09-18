import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { catchError, take } from 'rxjs/operators';

import { QueryErrorResponse } from '../../kaleido/models/query-error-response';
import {
    QueryRequest
} from '../../kaleido/models/queryable-request';
import { QueryableService } from '../../kaleido/services/queryable-service';
import { ProcessErrorResponse, ProcessService } from '../../kaleido/services/process-service';
import { buildProcessRoute } from '../../process/services/process-navigation';
import { ProcessStateService } from '../../process/services/process-state-service';
import { RegistryCatalog } from '../../registries/registry-catalog';
import { CaptureMemberStep } from '../models/capture-member-step';
import { MemberDetailsParameters } from '../models/member-details-parameters';
import { MemberDetailsResult } from '../models/member-details-result';

@Component({
    selector: 'priorauth-capture-member',
    standalone: true,
    templateUrl: './capture-member.html',
    styleUrl: './capture-member.scss'
})
export class CaptureMember {
    private readonly queryableService =
        inject(QueryableService);

    private readonly processService =
        inject(ProcessService);

    private readonly processState =
        inject(ProcessStateService);

    private readonly registryCatalog =
        inject(RegistryCatalog);

    private readonly router =
        inject(Router);

    readonly detailsViewName =
        'member-details';

    readonly selectedMemberDetails =
        signal<MemberDetailsResult | undefined>(undefined);
    readonly isLoadingDetails =
        signal(false);
    readonly isNavigating =
        signal(false);
    readonly errorMessage =
        signal<string | undefined>(undefined);

    constructor() {
        this.loadMemberDetails();
    }

    private loadMemberDetails(): void {
        const selectedMember = this.processState.state().selectedMember;
        if (!selectedMember) {
            this.errorMessage.set('No member selected. Please return to member search.');
            return;
        }

        this.errorMessage.set(undefined);
        this.isLoadingDetails.set(true);
        this.selectedMemberDetails.set(undefined);

        this.registryCatalog.loadState()
            .pipe(take(1))
            .subscribe({
                next: () => {
                    const detailsRequest: QueryRequest<MemberDetailsParameters> = {
                        parameters: {
                            MemberId: selectedMember.memberId,
                            MemberEnrollmentId: selectedMember.memberEnrollmentId
                        }
                    };

                    this.queryableService
                        .queryView<MemberDetailsResult, MemberDetailsParameters>(
                            this.detailsViewName,
                            detailsRequest)
                        .subscribe({
                            next: result => {
                                this.selectedMemberDetails.set(result.results[0]);
                                this.isLoadingDetails.set(false);
                            },
                            error: error => {
                                this.selectedMemberDetails.set(undefined);
                                this.isLoadingDetails.set(false);
                                this.errorMessage.set(this.formatError(error));
                            }
                        });
                },
                error: error => {
                    this.isLoadingDetails.set(false);
                    this.errorMessage.set(this.formatError(error));
                }
            });
    }

    confirmMember(): void {
        const selectedMember = this.processState.state().selectedMember;
        if (!selectedMember) {
            this.errorMessage.set('No member selected. Please return to member search.');
            return;
        }

        if (this.isNavigating()) {
            return;
        }

        this.isNavigating.set(true);
        this.errorMessage.set(undefined);

        const captureRequest = {
            processId: this.processState.state().processId,
            processStep: {
                memberId: selectedMember.memberId,
                memberEnrollmentId: selectedMember.memberEnrollmentId,
                dateOfService: this.processState.state().dateOfService
            } satisfies CaptureMemberStep
        };

        this.processService
            .executeStep<CaptureMemberStep, object>('CaptureMember', captureRequest)
            .subscribe({
                next: () => {
                    this.isNavigating.set(false);
                },
                error: (error: unknown) => {
                    this.isNavigating.set(false);
                    this.errorMessage.set(this.formatError(error));
                }
            });
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

    backToResults(): void {
        void this.router.navigate(
            buildProcessRoute(
                this.processState.state().processId,
                'member-search'));
    }

    private formatError(
        error: unknown
    ): string {
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
