export function getRouteForStep(
    stepName: string | undefined
): string | undefined {
    switch (stepName) {
        case 'CaptureMriInfoStep':
            return 'capture-mri-info';
        case 'ConfirmCtInsteadOfMriStep':
            return 'confirm-ct-instead-of-mri';
        case 'RequestedServices':
            return 'requested-services';
        case 'CaptureServicingProviderStep':
            return 'servicing-provider';
        case 'ValidateMemberStep':
            return 'member-search';
        case 'CaptureMemberStep':
            return 'capture-member';
        default:
            return undefined;
    }
}
