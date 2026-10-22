// One History record per process: the latest information for that request.
export interface PriorAuthRecordView {
    processId: string;
    processorName: string;
    /** The product processor's prior authorization; absent while the request is in Intake. */
    priorAuthorizationId?: string;
    status: string;
    memberNumber: string;
    memberDisplayName: string;
    dateOfService?: string;
    primaryProcedureCode: string;
    primaryProcedureDescription: string;
    createdUtc: string;
    updatedUtc: string;
}
