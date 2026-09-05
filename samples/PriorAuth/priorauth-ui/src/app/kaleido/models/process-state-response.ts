import { ProcessStepSummary } from './processor-process-result';

export interface ProcessStateResponse {
    processId: string;
    state: string;
    requiredStep?: string;
    targetProcessorName?: string;
    availableSteps: ProcessStepSummary[];
}
