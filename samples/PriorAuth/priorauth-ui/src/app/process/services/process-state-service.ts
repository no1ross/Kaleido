import { Injectable, signal } from '@angular/core';

import {
    ProcessMessage,
    ProcessStepSummary
} from '../../kaleido/models/processor-process-result';
import { InformationRequest } from '../../kaleido/models/information-request';

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
    currentProcessorName?: string;
    dateOfService: string;
    isDateOfServiceLocked: boolean;
    selectedMember?: ProcessSelectedMemberSummary;
    processMessages: ProcessMessage[];
    requiredStep?: string;
    availableSteps: ProcessStepSummary[];
    /** The step the pending information request belongs to (the required step). */
    informationStepName?: string;
    informationRequest?: InformationRequest;
}

@Injectable({
    providedIn: 'root'
})
export class ProcessStateService {
    readonly state =
        signal<ProcessState>(this.createInitialState());

    setSelectedMember(
        member: ProcessSelectedMemberSummary
    ): void {
        this.state.update(state => ({
            ...state,
            selectedMember: member,
            isDateOfServiceLocked: true
        }));
    }

    clearSelectedMember(): void {
        this.state.update(state => ({
            ...state,
            selectedMember: undefined,
            isDateOfServiceLocked: false
        }));
    }

    setProcessId(
        processId: string | undefined
    ): void {
        this.state.update(state => ({
            ...state,
            processId
        }));
    }

    setProcessMessages(
        messages: ProcessMessage[]
    ): void {
        this.state.update(state => ({
            ...state,
            processMessages: messages
        }));
    }

    clearProcessMessages(): void {
        this.state.update(state => ({
            ...state,
            processMessages: []
        }));
    }

    setCurrentProcessor(processorName: string): void {
        this.state.update(state => ({
            ...state,
            currentProcessorName: processorName
        }));
    }

    setProcessFlow(
        processorName: string,
        requiredStep: string | undefined,
        availableSteps: ProcessStepSummary[]
    ): void {
        this.state.update(state => ({
            ...state,
            currentProcessorName: processorName,
            requiredStep,
            availableSteps
        }));
    }

    setInformationRequest(
        stepName: string | undefined,
        informationRequest: InformationRequest | undefined
    ): void {
        this.state.update(state => ({
            ...state,
            informationStepName: stepName,
            informationRequest
        }));
    }

    setDateOfService(
        dateOfService: string
    ): void {
        if (this.state().isDateOfServiceLocked) {
            return;
        }

        this.state.update(state => ({
            ...state,
            dateOfService: dateOfService || this.getTodayDate()
        }));
    }

    reset(): void {
        this.state.set(this.createInitialState());
    }

    private createInitialState(): ProcessState {
        return {
            dateOfService: this.getTodayDate(),
            isDateOfServiceLocked: false,
            processMessages: [],
            availableSteps: []
        };
    }

    private getTodayDate(): string {
        return new Date().toISOString().slice(0, 10);
    }
}
