// Only the ids of the chosen search result: the service checks them and reads the rest of the row.
export interface CaptureRequestingProviderStep {
    providerId: string;
    providerLocationId: string;
}
