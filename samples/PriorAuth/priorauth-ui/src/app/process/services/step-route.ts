export function getRouteForStep(
    stepName: string | undefined
): string | undefined {
    switch (stepName) {
        case 'CaptureRequestedService':
            return 'requested-service';
        case 'CaptureMriInfo':
            return 'capture-mri-info';
        case 'ConfirmCtInsteadOfMri':
            return 'confirm-ct-instead-of-mri';
        case 'RequestedServices':
            return 'requested-services';
        case 'CaptureServicingProvider':
            return 'servicing-provider';
        default:
            return undefined;
    }
}
