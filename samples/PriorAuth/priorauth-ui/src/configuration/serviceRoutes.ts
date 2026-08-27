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
        key: 'reference-data',
        displayName: 'Reference Data',
        queryableRegistryPath: '/reference-data/queryable/registry'
    },
    provider: {
        key: 'provider',
        displayName: 'Provider Search',
        queryableRegistryPath: '/provider/queryable/registry'
    },
    codeSet: {
        key: 'code-set',
        displayName: 'Code Set',
        queryableRegistryPath: '/code-set/queryable/registry'
    },
    intake: {
        key: 'intake',
        displayName: 'Intake',
        processRegistryPath: '/intake/processes/registry'
    }
} as const satisfies Record<string, Omit<PriorAuthServiceRouteConfig, 'baseUrl'>>;

export function createServiceRoutes(
    overrides: {
        readonly member: ServiceRouteOverride;
        readonly referenceData: ServiceRouteOverride;
        readonly provider: ServiceRouteOverride;
        readonly codeSet: ServiceRouteOverride;
        readonly intake: ServiceRouteOverride;
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
            ...serviceRouteTemplates.intake,
            ...overrides.intake
        }
    ];
}
