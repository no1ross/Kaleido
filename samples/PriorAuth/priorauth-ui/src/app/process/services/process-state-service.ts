import { Injectable } from '@angular/core';

import {
    ProcessMessage,
    ProcessStepSummary
} from '../../kaleido/models/participant-process-result';

export interface ProcessSelectedMemberSummary {
    memberId: string;
    memberEnrollmentId: string;
    displayName: string;
    memberNumber: string;
    dateOfBirth: string;
    lineOfBusiness: string;
    planName: string;
    effectiveDate: string;
    terminationDate?: string;
}

export interface ProcessState {
    processId?: string;
    dateOfService: string;
    isDateOfServiceLocked: boolean;
    selectedMember?: ProcessSelectedMemberSummary;
    processMessages: ProcessMessage[];
    requiredStep?: string;
    availableSteps: ProcessStepSummary[];
}

@Injectable({
    providedIn: 'root'
})
export class ProcessStateService {
    readonly state: ProcessState = {
        dateOfService: this.getTodayDate(),
        isDateOfServiceLocked: false,
        processMessages: [],
        availableSteps: []
    };

    setSelectedMember(
        member: ProcessSelectedMemberSummary
    ): void {
        this.state.selectedMember = member;
        this.state.isDateOfServiceLocked = true;
    }

    clearSelectedMember(): void {
        this.state.selectedMember = undefined;
        this.state.isDateOfServiceLocked = false;
    }

    setProcessId(
        processId: string | undefined
    ): void {
        this.state.processId = processId;
    }

    setProcessMessages(
        messages: ProcessMessage[]
    ): void {
        this.state.processMessages = messages;
    }

    clearProcessMessages(): void {
        this.state.processMessages = [];
    }

    setProcessFlow(
        requiredStep: string | undefined,
        availableSteps: ProcessStepSummary[]
    ): void {
        this.state.requiredStep = requiredStep;
        this.state.availableSteps = availableSteps;
    }

    setDateOfService(
        dateOfService: string
    ): void {
        if (this.state.isDateOfServiceLocked) {
            return;
        }

        this.state.dateOfService = dateOfService || this.getTodayDate();
    }

    private getTodayDate(): string {
        return new Date().toISOString().slice(0, 10);
    }
}
