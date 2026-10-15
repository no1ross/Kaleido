import { getResumeRoute, getRouteForStep } from './step-route';

describe('getRouteForStep', () => {
  it.each([
    ['CaptureRequestingProviderStep', 'requesting-provider'],
    ['CaptureServicingProviderStep', 'servicing-provider'],
    ['CaptureClinicalInfoStep', 'clinical-info'],
    ['ReviewPriorAuthorizationStep', 'review'],
    ['SubmitPriorAuthorizationStep', 'review'],
    ['RemoveRequestedServiceStep', 'requested-services']
  ])('maps %s to %s', (step, route) => {
    expect(getRouteForStep(step)).toBe(route);
  });

  it('has no screen for unknown or internal steps', () => {
    expect(getRouteForStep('StartRadiologyIntakeStep')).toBeUndefined();
    expect(getRouteForStep(undefined)).toBeUndefined();
  });
});

describe('getResumeRoute', () => {
  const steps = (...names: string[]) => names.map(name => ({ name }));

  it('lands on the furthest available step', () => {
    expect(getResumeRoute(steps('CaptureRequestedServiceStep', 'RemoveRequestedServiceStep', 'CaptureRequestingProviderStep')))
      .toBe('requested-services');
    expect(getResumeRoute(steps('CaptureServicingProviderStep', 'ReviewPriorAuthorizationStep'))).toBe('review');
  });

  it('matches step names case-insensitively', () => {
    expect(getResumeRoute(steps('captureservicingproviderstep'))).toBe('servicing-provider');
  });

  it('sends member capture to member search first', () => {
    expect(getResumeRoute(steps('CaptureMemberStep'))).toBe('member-search');
  });

  it('has nowhere to go when nothing is available', () => {
    expect(getResumeRoute([])).toBeUndefined();
  });
});
