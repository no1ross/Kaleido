import { PriorAuthServiceRouteConfig } from './urlConfig';

type ServiceRouteOverride =
    Pick<PriorAuthServiceRouteConfig, 'baseUrl'>;

const serviceRouteTemplates = {
    member: {
        key: 'member',
        displayName: 'Member Service',
        processRegistryPath: '/member/processes/registry',
        queryableRegistryPath: '/member/queryable/registry'
    },
    referenceData: {
        key: 'referencedata',
        displayName: 'Reference Data',
        queryableRegistryPath: '/referencedata/queryable/registry'
    },
    provider: {
        key: 'provider',
        displayName: 'Provider Search',
        queryableRegistryPath: '/provider/queryable/registry'
    },
    codeSet: {
        key: 'codeset',
        displayName: 'Code Set',
        queryableRegistryPath: '/codeset/queryable/registry'
    },
    configuration: {
        key: 'configuration',
        displayName: 'Configuration',
        queryableRegistryPath: '/configuration/queryable/registry'
    },
    intake: {
        key: 'intake',
        displayName: 'Intake',
        registryPath: '/intake/kaleido/registry',
        isEntryProcessor: true
    },
    radiology: {
        key: 'radiology',
        displayName: 'Radiology',
        queryableRegistryPath: '/radiology/queryable/registry'
    },
    history: {
        key: 'history',
        displayName: 'History',
        queryableRegistryPath: '/history/queryable/registry'
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
        }
    ];
}
