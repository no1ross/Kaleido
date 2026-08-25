import { Injectable } from '@angular/core';

export interface WorkflowSelectedMemberSummary {
    memberId: string;
    memberEnrollmentId: string;
    displayName: string;
    memberNumber: string;
    dateOfBirth: string;
    lineOfBusiness: string;
    planName: string;
}

export interface WorkflowState {
    processId?: string;
    selectedMember?: WorkflowSelectedMemberSummary;
}

@Injectable({
    providedIn: 'root'
})
export class WorkflowStateService {
    readonly state: WorkflowState = {};

    setSelectedMember(
        member: WorkflowSelectedMemberSummary
    ): void {
        this.state.selectedMember = member;
    }

    clearSelectedMember(): void {
        this.state.selectedMember = undefined;
    }
}
