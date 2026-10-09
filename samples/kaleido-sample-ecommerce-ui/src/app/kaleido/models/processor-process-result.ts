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

/** The step a process requires next (the same shape as an available step, plus any pending questions). */
export interface ProcessRequiredStep extends ProcessStepSummary {

    informationRequest?: unknown;
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
