export interface ParticipantProcessResult {
    participantProcessId: string;

    state: string;

    requiredStep?: string;

    availableSteps: string[];

    steps: ParticipantStepResult[];
}

export interface ParticipantStepResult {
    stepName: string;

    response: any;

    executionStatus?: string;

    messages: ProcessMessage[];
}

export interface ProcessExecutionResponse<TResponse> {

    participantProcessId: string;

    stepName: string;

    outcome: StepExecutionOutcome;

    result: TResponse;

    requiredStep?: string;

    availableSteps: ProcessStepSummary[];

    messages: ProcessMessage[];
}

export interface ProcessStepSummary {

    name: string;

    version: string;

    displayName?: string;

    description?: string;

    repeatable: boolean;

    executeUrl: string;

    metadataUrl: string;
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
