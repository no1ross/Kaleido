export function getRouteForStep(
    stepName: string | undefined
): string | undefined {
    switch (stepName) {
        case 'CaptureMriInfo':
            return 'capture-mri-info';
        case 'ConfirmCtInsteadOfMri':
            return 'confirm-ct-instead-of-mri';
        case 'RequestedServices':
            return 'requested-services';
        case 'CaptureServicingProvider':
            return 'servicing-provider';
        case 'ValidateMember':
            return 'member-search';
        case 'CaptureMember':
            return 'capture-member';
        default:
            return undefined;
    }
}
