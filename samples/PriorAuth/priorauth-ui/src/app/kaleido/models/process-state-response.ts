import { ProcessRequiredStep, ProcessStepSummary } from './processor-process-result';

export interface ProcessStateResponse {
    processId: string;
    state: string;
    requiredStep?: ProcessRequiredStep;
    targetProcessorName?: string;
    availableSteps: ProcessStepSummary[];
}
