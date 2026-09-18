export interface ProcessSummary {
    processId: string;
    eventCount: number;
    mostRecentOccurredOn: string;  // ISO 8601 string
    mostRecentReceivedOn: string;  // ISO 8601 string
}
