export interface ExecuteStepRequest<TStep> {
    processId?: string;
    requestId?: string;
    processStep: TStep;
}
