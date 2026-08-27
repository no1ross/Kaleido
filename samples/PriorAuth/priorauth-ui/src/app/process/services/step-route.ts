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
        default:
            return undefined;
    }
}
