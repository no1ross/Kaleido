import { InformationRequest } from './information-request';

export interface ParticipantProcessResult {
    processId: string;
    state: string;
    requiredStep?: string;
    availableSteps: string[];
    steps: ParticipantStepResult[];
}

export interface ParticipantStepResult {
    stepName: string;
    response: any;
    executionStatus?: string;
    businessMessages: ProcessMessage[];
    frameworkMessages: ProcessMessage[];
}

export interface ProcessExecutionResponse<TResponse> {
    processId: string;
    stepName: string;
    outcome: StepExecutionOutcome;
    result: TResponse;
    requiredStep?: ProcessRequiredStep;
    targetProcessorName?: string;
    availableSteps: ProcessStepSummary[];
    businessMessages: ProcessMessage[];
    frameworkMessages: ProcessMessage[];
}

export interface ProcessStepSummary {
    name: string;
    version: string;
    displayName?: string;
    description?: string;
    repeatable: boolean;
    isInformationStep?: boolean;
    executeUrl: string;
}

/** The step a process requires next; carries the pending questions for an information step. */
export interface ProcessRequiredStep extends ProcessStepSummary {
    informationRequest?: InformationRequest;
}

export interface ProcessMessage {
    type: string;
    message: string;
    code: string;
}

export type StepExecutionOutcome =
    'Pending'
    | 'Completed'
    | 'Failed'
    | 'Blocked'
    | 'Cancelled';
