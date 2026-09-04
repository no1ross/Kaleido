export interface ExecuteStepRequest<TStep> {
    processId?: string;
    processStep: TStep;
}
