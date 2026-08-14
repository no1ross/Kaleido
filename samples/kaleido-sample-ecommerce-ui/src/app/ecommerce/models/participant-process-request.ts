export interface ExecuteStepRequest<TStep> {
    participantProcessId?: string;

    requestId?: string;

    processStep: TStep;
}