import { Injectable } from '@angular/core';

import { ProcessMessage } from '../../kaleido/models/participant-process-result';

export interface ProcessSelectedMemberSummary {
    memberId: string;
    memberEnrollmentId: string;
    displayName: string;
    memberNumber: string;
    dateOfBirth: string;
    lineOfBusiness: string;
    planName: string;
}

export interface ProcessState {
    processId?: string;
    selectedMember?: ProcessSelectedMemberSummary;
    processMessages: ProcessMessage[];
}

@Injectable({
    providedIn: 'root'
})
export class ProcessStateService {
    readonly state: ProcessState = {
        processMessages: []
    };

    setSelectedMember(
        member: ProcessSelectedMemberSummary
    ): void {
        this.state.selectedMember = member;
    }

    clearSelectedMember(): void {
        this.state.selectedMember = undefined;
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
}
