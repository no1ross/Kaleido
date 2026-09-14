import { PriorAuthServiceRouteConfig } from './urlConfig';

type ServiceRouteOverride =
    Pick<PriorAuthServiceRouteConfig, 'baseUrl'> & Partial<Pick<PriorAuthServiceRouteConfig, 'registryPath'>>;

const serviceRouteTemplates = {
    member: {
        key: 'member',
        displayName: 'Member Service'
    },
    referenceData: {
        key: 'referencedata',
        displayName: 'Reference Data'
    },
    provider: {
        key: 'provider',
        displayName: 'Provider Search'
    },
    codeSet: {
        key: 'codeset',
        displayName: 'Code Set'
    },
    configuration: {
        key: 'configuration',
        displayName: 'Configuration'
    },
    intake: {
        key: 'intake',
        displayName: 'Intake'
    },
    radiology: {
        key: 'radiology',
        displayName: 'Radiology'
    },
    history: {
        key: 'history',
        displayName: 'History'
    },
    router: {
        key: 'router',
        displayName: 'Router',
        registryPath: '/kaleido/registry'
    }
} as const satisfies Record<string, Omit<PriorAuthServiceRouteConfig, 'baseUrl'>>;

export function createServiceRoutes(
    overrides: {
        readonly member: ServiceRouteOverride;
        readonly referenceData: ServiceRouteOverride;
        readonly provider: ServiceRouteOverride;
        readonly codeSet: ServiceRouteOverride;
        readonly configuration: ServiceRouteOverride;
        readonly intake: ServiceRouteOverride;
        readonly radiology: ServiceRouteOverride;
        readonly history: ServiceRouteOverride;
        readonly router: ServiceRouteOverride
    }
): readonly PriorAuthServiceRouteConfig[] {
    return [
        {
            ...serviceRouteTemplates.member,
            ...overrides.member
        },
        {
            ...serviceRouteTemplates.referenceData,
            ...overrides.referenceData
        },
        {
            ...serviceRouteTemplates.provider,
            ...overrides.provider
        },
        {
            ...serviceRouteTemplates.codeSet,
            ...overrides.codeSet
        },
        {
            ...serviceRouteTemplates.configuration,
            ...overrides.configuration
        },
        {
            ...serviceRouteTemplates.intake,
            ...overrides.intake
        },
        {
            ...serviceRouteTemplates.radiology,
            ...overrides.radiology
        },
        {
            ...serviceRouteTemplates.history,
            ...overrides.history
        },
        {
            ...serviceRouteTemplates.router,
            ...overrides.router
        }
    ];
}
