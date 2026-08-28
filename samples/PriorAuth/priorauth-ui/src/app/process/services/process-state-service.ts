import { Injectable, signal } from '@angular/core';

import {
    ProcessMessage,
    ProcessStepSummary
} from '../../kaleido/models/participant-process-result';
import { QuestionnaireDefinition } from '../../kaleido/models/questionnaire';

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
    questionnaireStepName?: string;
    questionnaire?: QuestionnaireDefinition;
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

    setProcessFlow(
        requiredStep: string | undefined,
        availableSteps: ProcessStepSummary[]
    ): void {
        this.state.update(state => ({
            ...state,
            requiredStep,
            availableSteps
        }));
    }

    setQuestionnaire(
        stepName: string | undefined,
        questionnaire: QuestionnaireDefinition | undefined
    ): void {
        this.state.update(state => ({
            ...state,
            questionnaireStepName: stepName,
            questionnaire
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
