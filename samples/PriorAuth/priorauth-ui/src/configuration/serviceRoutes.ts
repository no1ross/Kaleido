import { PriorAuthServiceRouteConfig } from './urlConfig';

type ServiceRouteOverride = Pick<PriorAuthServiceRouteConfig, 'baseUrl'> & Partial<Pick<PriorAuthServiceRouteConfig, 'registryPath'>>;

const serviceRouteTemplates = {
    member:        { key: 'member'        },
    referenceData: { key: 'referencedata' },
    provider:      { key: 'provider'      },
    codeSet:       { key: 'codeset'       },
    configuration: { key: 'configuration' },
    intake:        { key: 'intake'        },
    radiology:     { key: 'radiology'     },
    history:       { key: 'history'       },
    router:        { key: 'router', registryPath: '/router/registry' }
} satisfies Record<string, Omit<PriorAuthServiceRouteConfig, 'baseUrl'>>;

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
        readonly router: ServiceRouteOverride;
    }
): readonly PriorAuthServiceRouteConfig[] {
    return [
        { ...serviceRouteTemplates.member,        ...overrides.member        },
        { ...serviceRouteTemplates.referenceData,  ...overrides.referenceData },
        { ...serviceRouteTemplates.provider,       ...overrides.provider      },
        { ...serviceRouteTemplates.codeSet,        ...overrides.codeSet       },
        { ...serviceRouteTemplates.configuration,  ...overrides.configuration },
        { ...serviceRouteTemplates.intake,         ...overrides.intake        },
        { ...serviceRouteTemplates.radiology,      ...overrides.radiology     },
        { ...serviceRouteTemplates.history,        ...overrides.history       },
        { ...serviceRouteTemplates.router,         ...overrides.router        }
    ];
}
