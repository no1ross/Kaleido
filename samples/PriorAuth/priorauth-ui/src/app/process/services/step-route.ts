// When a resumed process requires nothing, land on the furthest step it can take next.
const ResumePreference = [
    'ReviewPriorAuthorizationStep',
    'CaptureServicingProviderStep',
    'RemoveRequestedServiceStep',
    'CaptureRequestingProviderStep',
    'CaptureRequestedServiceStep',
    'ValidateMemberStep',
    'CaptureMemberStep'
];

export function getResumeRoute(
    availableSteps: readonly { name: string }[]
): string | undefined {
    const available = new Set(availableSteps.map(step => step.name.toLowerCase()));
    const step = ResumePreference.find(name => available.has(name.toLowerCase()));

    // Capturing a member needs a member picked in search first.
    return step === 'CaptureMemberStep'
        ? 'member-search'
        : getRouteForStep(step);
}

export function getRouteForStep(
    stepName: string | undefined
): string | undefined {
    switch (stepName) {
        case 'CaptureMriInfoStep':
            return 'capture-mri-info';
        case 'ConfirmCtInsteadOfMriStep':
            return 'confirm-ct-instead-of-mri';
        case 'CaptureRequestedServiceStep':
            return 'requested-service';
        case 'RemoveRequestedServiceStep':
            return 'requested-services';
        case 'CaptureRequestingProviderStep':
            return 'requesting-provider';
        case 'CaptureServicingProviderStep':
            return 'servicing-provider';
        case 'CaptureClinicalInfoStep':
            return 'clinical-info';
        case 'ReviewPriorAuthorizationStep':
        case 'SubmitPriorAuthorizationStep':
            return 'review';
        case 'ValidateMemberStep':
            return 'member-search';
        case 'CaptureMemberStep':
            return 'capture-member';
        default:
            return undefined;
    }
}
