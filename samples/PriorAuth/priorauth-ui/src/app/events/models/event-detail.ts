export interface EventDetail {
    id: number;
    processId: string;
    occurredOn: string;
    receivedOn: string;
    eventType: string;
    payloadJson: string;
}
