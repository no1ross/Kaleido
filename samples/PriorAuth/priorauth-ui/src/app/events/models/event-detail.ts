export interface EventDetail {
    id: number;
    eventType: string;
    requestId: string;
    serviceName: string;
    processId: string | null;
    stepName: string | null;
    occurredOn: string;
    receivedOn: string;
    contextJson: string;
    eventJson: string;
}
