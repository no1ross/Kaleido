// Only the ids of the chosen search result: the service checks them and reads the rest of the row.
// Shared by CaptureRequestingProviderStep and CaptureServicingProviderStep.
export interface CaptureProviderStep {
    providerId: string;
    providerLocationId: string;
}
